using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using GeFeSLE.ViewModels;
using GeFeSLE.Views;
using Avalonia.Controls;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using GeFeSLE.Services;

namespace GeFeSLE;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;
    private SettingsService? _settingsService;
    private GeFeSLEApiClient? _apiClient;
    private HotkeyService? _hotkeyService;
    private UnixSignalService? _signalService;
    // SettingsWindow removed; now part of MainWindow as tab

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _settingsService = new SettingsService();
        _apiClient = new GeFeSLEApiClient(new System.Net.Http.HttpClient());
        _hotkeyService = new HotkeyService(_settingsService);
        _signalService = new UnixSignalService();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            if (_settingsService == null || _apiClient == null || _hotkeyService == null || _signalService == null)
                throw new InvalidOperationException("Services not initialized");
            _mainWindow = new MainWindow(new MainWindowViewModel(_settingsService, _apiClient, _hotkeyService), _settingsService, _hotkeyService)
            {
                WindowStartupLocation = WindowStartupLocation.Manual, // We'll handle positioning ourselves
                ShowInTaskbar = true
            };

            // Handle window closing to minimize to tray instead
            _mainWindow.Closing += MainWindow_Closing;

            desktop.MainWindow = _mainWindow;

            // Show the main window on startup
            _mainWindow.Show();
            
            // Set up tray icon
            SetupTrayIcon();
            
            // Set up signal handling on Unix systems (after main window is created)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SetupUnixSignalHandling();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
        if (_trayIcon != null)
        {
            _trayIcon.Clicked += TrayIcon_Clicked;
            if (_trayIcon.Menu?.Items != null)
            {
                foreach (var item in _trayIcon.Menu.Items.OfType<NativeMenuItem>())
                {
                    switch (item.Header)
                    {
                        case "Toggle Window":
                            item.Click += ToggleMainWindowFromMenu;
                            break;
                        case "Settings":
                            item.Click += ShowSettingsTab;
                            break;
                        case "Exit":
                            item.Click += ExitApplication;
                            break;
                    }
                }
            }
        }
    }

    private void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        DBg.d(LogLevel.Debug, "Tray icon clicked - toggling main window");
        ToggleMainWindow();
    }

    private void ShowMainWindow(object? sender, EventArgs e)
    {
        DBg.d(LogLevel.Debug, "Show main window from menu");
        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    private void ToggleMainWindowFromMenu(object? sender, EventArgs e)
    {
        DBg.d(LogLevel.Debug, "Toggle main window from menu");
        ToggleMainWindow();
    }

    private void ShowSettingsTab(object? sender, EventArgs e)
    {
        DBg.d(LogLevel.Debug, "Show settings tab from menu");
        if (_mainWindow != null)
        {
            // Show the window first
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
            
            // TODO: Switch to Settings tab if we have access to the tab control
            // For now, just showing the window is sufficient since Settings is a tab
        }
    }

    private void ToggleMainWindow()
    {
        DBg.d(LogLevel.Debug, "Toggling main window visibility");
        if (_mainWindow != null)
        {
            if (_mainWindow.IsVisible)
            {
                DBg.d(LogLevel.Debug, "Window is visible - hiding it");
                _mainWindow.Hide();
            }
            else
            {
                DBg.d(LogLevel.Debug, "Window is hidden - showing it");
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
                _mainWindow.Focus();
            }
        }
    }

    // Settings tab is now part of MainWindow; no separate window

    private void ExitApplication(object? sender, EventArgs e)
    {
        DBg.d(LogLevel.Trace, "ENTER ExitApplication");
        
        // Persist configuration before exit
        PersistConfigurationOnShutdown();
        
        // Clean up signal handling
        _signalService?.StopListening();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
        
        DBg.d(LogLevel.Trace, "RETURN ExitApplication");
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        // Cancel the close operation
        e.Cancel = true;
        
        // Hide the window instead of closing it
        if (_mainWindow != null)
        {
            _mainWindow.Hide();
        }
    }

    private void SetupUnixSignalHandling()
    {
        DBg.d(LogLevel.Trace, "ENTER SetupUnixSignalHandling");
        
        if (_signalService == null || _mainWindow == null)
        {
            DBg.d(LogLevel.Error, "Signal service or main window not initialized");
            DBg.d(LogLevel.Trace, "RETURN SetupUnixSignalHandling (not initialized)");
            return;
        }

        try
        {
            // Set the toggle action
            _signalService.SetToggleWindowAction(ToggleMainWindow);
            
            // Set the persist config action for graceful shutdown
            _signalService.SetPersistConfigAction(PersistConfigurationOnShutdown);
            
            // Start listening for signals
            var success = _signalService.StartListening();
            
            if (success)
            {
                DBg.d(LogLevel.Debug, "Unix signal handling setup successfully");
            }
            else
            {
                DBg.d(LogLevel.Warning, "Failed to setup Unix signal handling");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Exception in SetupUnixSignalHandling: {ex.Message}");
        }
        
        DBg.d(LogLevel.Trace, "RETURN SetupUnixSignalHandling");
    }

    private void PersistConfigurationOnShutdown()
    {
        DBg.d(LogLevel.Trace, "ENTER PersistConfigurationOnShutdown");
        
        try
        {
            // Save window settings if main window exists
            if (_mainWindow != null)
            {
                DBg.d(LogLevel.Debug, "Saving window settings before shutdown");
                _mainWindow.SaveCurrentWindowSettings();
            }
            
            // Ensure all settings are persisted
            if (_settingsService != null)
            {
                DBg.d(LogLevel.Debug, "Force saving all settings before shutdown");
                _settingsService.SaveSettings();
            }
            
            DBg.d(LogLevel.Debug, "Configuration persistence completed");
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error persisting configuration on shutdown: {ex.Message}");
        }
        
        DBg.d(LogLevel.Trace, "RETURN PersistConfigurationOnShutdown");
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}