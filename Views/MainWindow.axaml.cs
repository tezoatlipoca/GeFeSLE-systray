using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using GeFeSLE.Services;
using GeFeSLE.ViewModels;
using GeFeSLE.Models;
using Avalonia.LogicalTree;
using System.Linq;

namespace GeFeSLE.Views;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;
    private bool _isInitialized = false;

    public MainWindow(MainWindowViewModel viewModel, SettingsService settingsService, HotkeyService hotkeyService)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        
        AvaloniaXamlLoader.Load(this);
        DataContext = viewModel;
        
        // Set up scroll position preservation
        viewModel.ItemExpansionChanged += OnItemExpansionChanged;
        
        // Set up hotkey service
        _hotkeyService.SetToggleWindowAction(ToggleWindowVisibility);
        
        // Set up event handlers
        Closing += OnClosing;
        Opened += OnOpened;
        PropertyChanged += OnPropertyChanged;
        
        // Apply initial window settings
        ApplyWindowSettings();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        _isInitialized = true;
        
        // Calculate default size based on screen
        if (_settingsService.Settings.WindowWidth <= 0 || _settingsService.Settings.WindowHeight <= 0)
        {
            var screen = Screens.Primary;
            if (screen != null)
            {
                var workingArea = screen.WorkingArea;
                Width = workingArea.Width * 0.25; // 25% of screen width
                Height = workingArea.Height; // Full height minus taskbar/dock
                
                // Center horizontally, align to left edge
                Position = new PixelPoint(workingArea.X, workingArea.Y);
            }
        }
        
        // Register hotkey
        _hotkeyService.RegisterHotkey();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Save window settings
        SaveWindowSettings();
        
        // Unregister hotkey
        _hotkeyService.UnregisterHotkey();
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_isInitialized) return;
        
        // Save window settings when properties change
        if (e.Property == WindowStateProperty || e.Property == WidthProperty || 
            e.Property == HeightProperty)
        {
            SaveWindowSettings();
        }
    }

    private void ApplyWindowSettings()
    {
        var settings = _settingsService.Settings;
        
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
            Position = new PixelPoint((int)settings.WindowX, (int)settings.WindowY);
            
            if (settings.WindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }
    }

    private void SaveWindowSettings()
    {
        if (!_isInitialized) return;
        
        var screen = GetCurrentScreen();
        var screenIndex = GetScreenIndex(screen);
        
        _settingsService.UpdateWindowSettings(
            Width,
            Height,
            Position.X,
            Position.Y,
            screenIndex,
            WindowState == WindowState.Maximized
        );
    }

    // Public method to allow external saving of window settings
    public void SaveCurrentWindowSettings()
    {
        SaveWindowSettings();
    }

    private Screen? GetCurrentScreen()
    {
        var center = new PixelPoint(
            Position.X + (int)(Width / 2),
            Position.Y + (int)(Height / 2)
        );
        
        return Screens.ScreenFromPoint(center);
    }

    private int GetScreenIndex(Screen? screen)
    {
        if (screen == null) return 0;
        
        var screens = Screens.All;
        for (int i = 0; i < screens.Count; i++)
        {
            if (screens[i] == screen)
                return i;
        }
        return 0;
    }

    private void ToggleWindowVisibility()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
            Activate();
            Focus();
        }
    }

    private void OnItemExpansionChanged(GeListItem item)
    {
        // Find the ScrollViewer by name in the XAML
        var scrollViewer = this.FindControl<ScrollViewer>("ItemsScrollViewer");
        
        // If not found by name, try to find it in the logical tree
        if (scrollViewer == null)
        {
            scrollViewer = this.GetLogicalDescendants().OfType<ScrollViewer>().FirstOrDefault();
        }
        
        if (scrollViewer == null || DataContext is not MainWindowViewModel viewModel) return;

        // Find the ListBox containing the items
        var listBox = scrollViewer.GetLogicalDescendants().OfType<ListBox>().FirstOrDefault();
        if (listBox == null) return;

        try
        {
            // Find the index of the item that was expanded
            var itemIndex = viewModel.ListItems.IndexOf(item);
            if (itemIndex < 0) return;

            // Store the current scroll position and item information before UI updates
            var currentScrollOffset = scrollViewer.Offset.Y;
            var viewportHeight = scrollViewer.Viewport.Height;
            
            // Schedule the scroll adjustment after the UI has updated
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                // Give the UI time to fully update the layout
                await Task.Delay(50);
                
                // Re-get the container after layout update
                var container = listBox.ContainerFromIndex(itemIndex);
                if (container is ListBoxItem listBoxItem)
                {
                    // Get the updated bounds after expansion/collapse
                    var itemBounds = listBoxItem.Bounds;
                    var itemTopInScrollContent = itemBounds.Y;
                    
                    // Calculate where the item's top was relative to the viewport before the change
                    var itemTopInViewportBefore = itemTopInScrollContent - currentScrollOffset;
                    
                    if (item.IsExpanded)
                    {
                        // When expanding: keep the item header at the same viewport position
                        // This prevents the item from jumping around during expansion
                        var targetScrollOffset = Math.Max(0, itemTopInScrollContent - itemTopInViewportBefore);
                        
                        // Ensure we don't scroll past the content
                        var maxScrollOffset = Math.Max(0, scrollViewer.Extent.Height - viewportHeight);
                        targetScrollOffset = Math.Min(targetScrollOffset, maxScrollOffset);
                        
                        scrollViewer.Offset = scrollViewer.Offset.WithY(targetScrollOffset);
                    }
                    else
                    {
                        // When collapsing: keep the item visible but don't jump around
                        // Only adjust if the item would be out of view
                        var itemBottomInViewport = itemTopInViewportBefore + itemBounds.Height;
                        
                        if (itemTopInViewportBefore < 0)
                        {
                            // Item top is above viewport, scroll to show it
                            var targetScrollOffset = Math.Max(0, itemTopInScrollContent - 20); // 20px padding
                            scrollViewer.Offset = scrollViewer.Offset.WithY(targetScrollOffset);
                        }
                        else if (itemBottomInViewport > viewportHeight)
                        {
                            // Item bottom is below viewport, scroll to show it
                            var targetScrollOffset = Math.Max(0, itemTopInScrollContent + itemBounds.Height - viewportHeight + 20);
                            scrollViewer.Offset = scrollViewer.Offset.WithY(targetScrollOffset);
                        }
                        // If item is already fully visible, don't adjust scroll position
                    }
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
        catch
        {
            // Ignore any errors in scroll position calculation
            // This is a UI enhancement, not critical functionality
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Handle hotkey detection for settings
        base.OnKeyDown(e);
    }
}