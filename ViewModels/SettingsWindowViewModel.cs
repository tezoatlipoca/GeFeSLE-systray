using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeFeSLE.Services;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GeFeSLE.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly GeFeSLEApiClient _apiClient;
    private readonly HotkeyService _hotkeyService;

    [ObservableProperty]
    private string? serverUrl;

    [ObservableProperty]
    private string? username;

    [ObservableProperty]
    private string? password;

    [ObservableProperty]
    private bool rememberLogin;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool isLoggedIn;

    // Hotkey properties
    [ObservableProperty]
    private List<string> availableModifiers = new();

    [ObservableProperty]
    private List<string> availableKeys = new();

    [ObservableProperty]
    private string selectedModifiers = "Control,Alt";

    [ObservableProperty]
    private string selectedKey = "G";

    [ObservableProperty]
    private string hotkeyStatus = "";

    [ObservableProperty]
    private string hotkeyStatusColor = "Green";

    public SettingsWindowViewModel(SettingsService settingsService, GeFeSLEApiClient apiClient, HotkeyService hotkeyService)
    {
        _settingsService = settingsService;
        _apiClient = apiClient;
        _hotkeyService = hotkeyService;

        // Load settings and populate fields
        ServerUrl = _settingsService.Settings.ServerUrl;
        Username = _settingsService.Settings.Username;
        Password = _settingsService.GetPassword();
        RememberLogin = _settingsService.Settings.RememberLogin;
        
        // Load hotkey settings
        SelectedModifiers = _settingsService.Settings.HotkeyModifiers;
        SelectedKey = _settingsService.Settings.HotkeyKey;
        AvailableModifiers = _hotkeyService.GetAvailableModifiers();
        AvailableKeys = _hotkeyService.GetAvailableKeys();
        
        // Set base address in API client if we have a server URL
        if (!string.IsNullOrEmpty(ServerUrl))
        {
            _apiClient.SetBaseAddress(ServerUrl);
        }
        
        // Load session cookies if available
        if (!string.IsNullOrEmpty(_settingsService.Settings.SessionCookies))
        {
            _apiClient.SetSessionCookies(_settingsService.Settings.SessionCookies);
        }
        
        IsLoggedIn = _settingsService.IsLoggedIn();
        UpdateHotkeyStatus();
    }

    // Constructor overload for backward compatibility
    public SettingsWindowViewModel(SettingsService settingsService, GeFeSLEApiClient apiClient) 
        : this(settingsService, apiClient, new HotkeyService(settingsService))
    {
    }

    [RelayCommand]
    private async Task Login()
    {
        await PerformLoginAsync();
    }
    
    public async Task<bool> ValidateExistingSessionAsync()
    {
        if (string.IsNullOrEmpty(ServerUrl))
            return false;
            
        _apiClient.SetBaseAddress(ServerUrl);
        var user = await _apiClient.GetCurrentUserAsync();
        
        if (user != null && user.IsAuthenticated)
        {
            IsLoggedIn = true;
            Username = user.UserName; // Update the username from the server response
            StatusMessage = $"Session validated. Logged in as {user.UserName}.";
            return true;
        }
        else
        {
            IsLoggedIn = false;
            return false;
        }
    }

    public async Task AttemptAutoLoginAsync()
    {
        // Don't set status message here - let the caller handle it
        if (string.IsNullOrWhiteSpace(ServerUrl) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            return;
        }
        _apiClient.SetBaseAddress(ServerUrl);
        var loginDto = new GeFeSLE.Models.LoginDto { Username = Username, Password = Password };
        var response = await _apiClient.LoginAsync(loginDto);
        if (response.Success)
        {
            // Save session cookies
            var cookies = _apiClient.GetSessionCookies();
            _settingsService.UpdateLoginInfo(Username!, Password, "dummy-session-token", RememberLogin);
            if (!string.IsNullOrEmpty(cookies))
            {
                _settingsService.UpdateSessionCookies(cookies);
            }
            
            IsLoggedIn = true;
            // Don't set status message for auto-login
        }
    }
    
    private async Task PerformLoginAsync()
    {
        StatusMessage = "Logging in...";
        if (string.IsNullOrWhiteSpace(ServerUrl) || string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Please fill in all fields.";
            return;
        }
        _apiClient.SetBaseAddress(ServerUrl);
        var loginDto = new GeFeSLE.Models.LoginDto { Username = Username, Password = Password };
        var response = await _apiClient.LoginAsync(loginDto);
        if (response.Success)
        {
            // Save session cookies
            var cookies = _apiClient.GetSessionCookies();
            _settingsService.UpdateLoginInfo(Username!, Password, "dummy-session-token", RememberLogin);
            if (!string.IsNullOrEmpty(cookies))
            {
                _settingsService.UpdateSessionCookies(cookies);
            }
            
            IsLoggedIn = true;
            if (!string.IsNullOrEmpty(response.Username) && !string.IsNullOrEmpty(response.Role))
            {
                StatusMessage = $"Login successful. Logged in as {response.Username} with role: {response.Role}";
            }
            else
            {
                StatusMessage = "Login successful.";
            }
        }
        else
        {
            StatusMessage = response.ErrorMessage ?? "Login failed.";
        }
    }

    [RelayCommand]
    private void Logout()
    {
        _settingsService.ClearLoginInfo();
        // Also recreate the HttpClient to clear cookies
        if (!string.IsNullOrEmpty(ServerUrl))
        {
            _apiClient.SetBaseAddress(ServerUrl);
        }
        IsLoggedIn = false;
        StatusMessage = "Logged out.";
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        StatusMessage = "Testing connection...";
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            StatusMessage = "Please enter a server URL.";
            return;
        }
        _apiClient.SetBaseAddress(ServerUrl);
        var user = await _apiClient.GetCurrentUserAsync();
        if (user != null && user.IsAuthenticated)
        {
            StatusMessage = $"Connected. Logged in as {user.UserName}.";
        }
        else
        {
            StatusMessage = "Connection failed or not authenticated.";
        }
    }

    partial void OnServerUrlChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _settingsService.UpdateServerUrl(value);
    }

    partial void OnUsernameChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _settingsService.Settings.Username = value;
        _settingsService.SaveSettings();
    }

    partial void OnPasswordChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _settingsService.UpdatePassword(value);
    }

    partial void OnRememberLoginChanged(bool value)
    {
        _settingsService.Settings.RememberLogin = value;
        _settingsService.SaveSettings();
    }

    [RelayCommand]
    private void ApplyHotkey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            HotkeyStatus = "Note: Global hotkeys are not supported on Linux. Use the system tray icon to show/hide the window, or set up a custom keyboard shortcut in your desktop environment.";
            HotkeyStatusColor = "Orange";
            // Still save the settings for potential future use
            _settingsService.UpdateHotkeySettings(SelectedModifiers, SelectedKey);
            return;
        }

        if (_hotkeyService.IsHotkeyInUse(SelectedModifiers, SelectedKey))
        {
            HotkeyStatus = $"Hotkey {SelectedModifiers}+{SelectedKey} is already in use by the system or another application.";
            HotkeyStatusColor = "Red";
        }
        else
        {
            var success = _hotkeyService.UpdateHotkey(SelectedModifiers, SelectedKey);
            if (success)
            {
                HotkeyStatus = $"Hotkey {SelectedModifiers}+{SelectedKey} applied successfully.";
                HotkeyStatusColor = "Green";
            }
            else
            {
                HotkeyStatus = "Failed to apply hotkey. Please try a different combination.";
                HotkeyStatusColor = "Red";
            }
        }
    }

    private void UpdateHotkeyStatus()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            HotkeyStatus = "Global hotkeys not supported on Linux. Use tray icon or desktop environment shortcuts.";
            HotkeyStatusColor = "Orange";
        }
        else
        {
            HotkeyStatus = $"Current hotkey: {SelectedModifiers}+{SelectedKey}";
            HotkeyStatusColor = "Blue";
        }
    }
}
