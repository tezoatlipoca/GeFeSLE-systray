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
using Avalonia.VisualTree;
using Avalonia.Threading;
using System.Linq;

namespace GeFeSLE.Views;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkeyService;
    private bool _isInitialized = false;
    
    // For scroll position preservation during item expansion/collapse
    private GeListItem? _lastToggledItem;
    private Point _lastCursorPosition;

    public MainWindow(MainWindowViewModel viewModel, SettingsService settingsService, HotkeyService hotkeyService)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        
        AvaloniaXamlLoader.Load(this);
        DataContext = viewModel;
        
        // Set up scroll position preservation
        viewModel.ItemExpansionChanged += OnItemExpansionChanged;
        
        // Track cursor position for scroll preservation
        PointerMoved += OnPointerMoved;
        
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
        // Preserve scroll position to keep the toggled item's header under the cursor
        PreserveScrollPositionForItem(item);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // Track cursor position for potential scroll preservation
        _lastCursorPosition = e.GetPosition(this);
    }

    private async void PreserveScrollPositionForItem(GeListItem toggledItem)
    {
        if (toggledItem == null) return;

        try
        {
            var scrollViewer = this.FindControl<ScrollViewer>("ItemsScrollViewer");
            var listBox = this.GetVisualDescendants().OfType<ListBox>().FirstOrDefault();
            
            if (scrollViewer == null || listBox == null) return;

            // Store reference and initial position
            _lastToggledItem = toggledItem;
            var initialScrollY = scrollViewer.Offset.Y;

            // Wait for the layout to update after expansion/collapse
            await Task.Delay(150); // Give more time for layout to settle

            // Find the container for the toggled item
            var itemContainer = FindItemContainer(listBox, toggledItem);
            if (itemContainer == null) return;

            // Get the current position of the item container
            var containerBounds = itemContainer.Bounds;
            var scrollViewerBounds = scrollViewer.Bounds;

            // Calculate the top of the item relative to the scroll viewer viewport
            var itemTop = itemContainer.TranslatePoint(new Point(0, 0), scrollViewer);
            if (!itemTop.HasValue) return;

            var currentItemTopInViewport = itemTop.Value.Y - scrollViewer.Offset.Y;

            // If the item header has moved significantly or is outside comfortable viewing area,
            // adjust scroll to keep it in a good position
            var viewportHeight = scrollViewer.Viewport.Height;
            var targetPositionInViewport = Math.Min(viewportHeight * 0.3, 100); // 30% down or 100px max

            // Only adjust if the item moved too much or is not optimally positioned
            if (currentItemTopInViewport < 0 || 
                currentItemTopInViewport > viewportHeight * 0.7 ||
                Math.Abs(currentItemTopInViewport - targetPositionInViewport) > 50)
            {
                var desiredScrollY = itemTop.Value.Y - targetPositionInViewport;
                
                // Clamp to valid scroll range
                var maxScroll = Math.Max(0, scrollViewer.ScrollBarMaximum.Y);
                var newScrollY = Math.Max(0, Math.Min(maxScroll, desiredScrollY));

                // Apply smooth scroll adjustment
                scrollViewer.Offset = scrollViewer.Offset.WithY(newScrollY);
                
                DBg.d(LogLevel.Debug, $"Adjusted scroll position for item {toggledItem.Id}: {initialScrollY:F1} -> {newScrollY:F1}px");
            }
            else
            {
                DBg.d(LogLevel.Debug, $"Item {toggledItem.Id} header position is good, no scroll adjustment needed");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Warning, $"Failed to preserve scroll position: {ex.Message}");
        }
    }

    private Control? FindItemContainer(ListBox listBox, GeListItem item)
    {
        try
        {
            // Find all ListBoxItem containers and check their DataContext
            var containers = listBox.GetVisualDescendants().OfType<ListBoxItem>();
            
            foreach (var container in containers)
            {
                if (container.DataContext == item)
                {
                    return container;
                }
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Handle hotkey detection for settings
        base.OnKeyDown(e);
    }
}