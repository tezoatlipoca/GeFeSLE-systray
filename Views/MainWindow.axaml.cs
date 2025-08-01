using System;
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

            // Get the container for this item
            var container = listBox.ContainerFromIndex(itemIndex);
            if (container is ListBoxItem listBoxItem)
            {
                // Store the item's current top position relative to the scroll viewer
                var itemBounds = listBoxItem.Bounds;
                var currentScrollOffset = scrollViewer.Offset.Y;
                
                // Calculate the item's absolute position in the scroll content
                var itemTopInScrollContent = itemBounds.Y;
                
                // Calculate the item's current position relative to the visible area
                var itemTopInViewport = itemTopInScrollContent - currentScrollOffset;
                
                // If the item is expanding and we want to keep its header visible at the same position
                if (item.IsExpanded)
                {
                    // Keep the item header at its current position in the viewport
                    // This means the scroll offset should remain where the item header is currently visible
                    var targetScrollOffset = Math.Max(0, itemTopInScrollContent - itemTopInViewport);
                    scrollViewer.Offset = scrollViewer.Offset.WithY(targetScrollOffset);
                }
                else
                {
                    // When collapsing, ensure the item is still visible
                    var targetScrollOffset = Math.Max(0, itemTopInScrollContent - 20); // 20px padding from top
                    scrollViewer.Offset = scrollViewer.Offset.WithY(targetScrollOffset);
                }
            }
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