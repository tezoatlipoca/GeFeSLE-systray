namespace GeFeSLE.Services;

using System.Text.Json;
using System.IO;
using System;

public class AppSettings
{
    public string? ServerUrl { get; set; }
    public string? Username { get; set; }
    public string? ObfuscatedPassword { get; set; }
    public string? SessionToken { get; set; }
    public bool RememberLogin { get; set; } = false;
    public int? SelectedListId { get; set; }
    public string? SessionCookies { get; set; }
    
    // Window settings
    public double WindowWidth { get; set; } = 800;
    public double WindowHeight { get; set; } = 600;
    public double WindowX { get; set; } = 100;
    public double WindowY { get; set; } = 100;
    public int WindowScreen { get; set; } = 0;
    public bool WindowMaximized { get; set; } = false;
    
    // Hotkey settings
    public string HotkeyModifiers { get; set; } = "Control,Alt";
    public string HotkeyKey { get; set; } = "G";
    
    // UI settings
    public bool MetadataPanelExpanded { get; set; } = false;
}

public class SettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsPath;
    private AppSettings _settings;

    public AppSettings Settings => _settings;

    public SettingsService()
    {
        DBg.d(LogLevel.Trace, "ENTER SettingsService.ctor");
        _settingsPath = GetSettingsFilePath();
        _settings = LoadSettings();
        DBg.d(LogLevel.Trace, "RETURN SettingsService.ctor");
    }

    private string GetSettingsFilePath()
    {
        DBg.d(LogLevel.Trace, "ENTER GetSettingsFilePath");
        string configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appDir = Path.Combine(configDir, "GeFeSLE-systray");
        if (!Directory.Exists(appDir))
            Directory.CreateDirectory(appDir);
        var result = Path.Combine(appDir, SettingsFileName);
        DBg.d(LogLevel.Trace, "RETURN GetSettingsFilePath");
        return result;
    }

    private AppSettings LoadSettings()
    {
        DBg.d(LogLevel.Trace, "ENTER LoadSettings");
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    DBg.d(LogLevel.Trace, "RETURN LoadSettings (from file)");
                    return settings;
                }
            }
        }
        catch { }
        DBg.d(LogLevel.Trace, "RETURN LoadSettings (new)");
        return new AppSettings();
    }

    public void SaveSettings()
    {
        DBg.d(LogLevel.Trace, "ENTER SaveSettings");
        try
        {
            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
        DBg.d(LogLevel.Trace, "RETURN SaveSettings");
    }

    public void UpdateServerUrl(string serverUrl)
    {
        DBg.d(LogLevel.Trace, "ENTER UpdateServerUrl");
        _settings.ServerUrl = serverUrl;
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN UpdateServerUrl");
    }

    public void UpdateLoginInfo(string username, string? password, string? sessionToken, bool rememberLogin)
    {
        DBg.d(LogLevel.Trace, "ENTER UpdateLoginInfo");
        _settings.Username = username;
        _settings.ObfuscatedPassword = Obfuscate(password);
        _settings.SessionToken = sessionToken;
        _settings.RememberLogin = rememberLogin;
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN UpdateLoginInfo");
    }

    public void UpdateSelectedList(int? listId)
    {
        DBg.d(LogLevel.Trace, "ENTER UpdateSelectedList");
        _settings.SelectedListId = listId;
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN UpdateSelectedList");
    }

    public void UpdateSessionCookies(string? cookies)
    {
        DBg.d(LogLevel.Trace, "ENTER UpdateSessionCookies");
        _settings.SessionCookies = cookies;
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN UpdateSessionCookies");
    }

    public void ClearSessionCookies()
    {
        DBg.d(LogLevel.Trace, "ENTER ClearSessionCookies");
        _settings.SessionCookies = null;
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN ClearSessionCookies");
    }

    public void UpdateWindowSettings(double width, double height, double x, double y, int screen, bool maximized)
    {
        DBg.d(LogLevel.Trace, "ENTER UpdateWindowSettings");
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        _settings.WindowX = x;
        _settings.WindowY = y;
        _settings.WindowScreen = screen;
        _settings.WindowMaximized = maximized;
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN UpdateWindowSettings");
    }

    public void UpdateHotkeySettings(string modifiers, string key)
    {
        DBg.d(LogLevel.Trace, "ENTER UpdateHotkeySettings");
        _settings.HotkeyModifiers = modifiers;
        _settings.HotkeyKey = key;
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN UpdateHotkeySettings");
    }

    public void UpdatePassword(string? password)
    {
        DBg.d(LogLevel.Trace, "ENTER UpdatePassword");
        _settings.ObfuscatedPassword = Obfuscate(password);
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN UpdatePassword");
    }

    public void ClearLoginInfo()
    {
        DBg.d(LogLevel.Trace, "ENTER ClearLoginInfo");
        _settings.Username = null;
        _settings.ObfuscatedPassword = null;
        _settings.SessionToken = null;
        _settings.SessionCookies = null;
        _settings.RememberLogin = false;
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN ClearLoginInfo");
    }

    public string? GetPassword()
    {
        DBg.d(LogLevel.Trace, "ENTER GetPassword");
        var result = Deobfuscate(_settings.ObfuscatedPassword);
        DBg.d(LogLevel.Trace, "RETURN GetPassword");
        return result;
    }

    // Simple obfuscation (base64, not secure, but better than plain text)
    private string? Obfuscate(string? input)
    {
        DBg.d(LogLevel.Trace, "ENTER Obfuscate");
        if (string.IsNullOrEmpty(input)) {
            DBg.d(LogLevel.Trace, "RETURN Obfuscate (null)");
            return null;
        }
        var result = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(input));
        DBg.d(LogLevel.Trace, "RETURN Obfuscate");
        return result;
    }
    private string? Deobfuscate(string? input)
    {
        DBg.d(LogLevel.Trace, "ENTER Deobfuscate");
        if (string.IsNullOrEmpty(input)) {
            DBg.d(LogLevel.Trace, "RETURN Deobfuscate (null)");
            return null;
        }
        try {
            var result = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(input));
            DBg.d(LogLevel.Trace, "RETURN Deobfuscate");
            return result;
        }
        catch {
            DBg.d(LogLevel.Trace, "RETURN Deobfuscate (fail)");
            return null;
        }
    }

    public bool IsLoggedIn()
    {
        DBg.d(LogLevel.Trace, "ENTER IsLoggedIn");
        // For now, just check if we have saved credentials and remember login is enabled
        // The actual login validation will happen during auto-login attempt
        var result = RememberLogin && !string.IsNullOrEmpty(_settings.Username) && 
                     !string.IsNullOrEmpty(GetPassword()) && !string.IsNullOrEmpty(_settings.ServerUrl);
        DBg.d(LogLevel.Trace, "RETURN IsLoggedIn");
        return result;
    }
    
    public void UpdateMetadataPanelState(bool expanded)
    {
        DBg.d(LogLevel.Trace, "ENTER UpdateMetadataPanelState");
        _settings.MetadataPanelExpanded = expanded;
        SaveSettings();
        DBg.d(LogLevel.Trace, "RETURN UpdateMetadataPanelState");
    }
    
    public bool RememberLogin => _settings.RememberLogin;
}
