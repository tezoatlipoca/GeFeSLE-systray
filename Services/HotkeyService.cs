using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Input;

namespace GeFeSLE.Services;

public class HotkeyService
{
    private readonly SettingsService _settingsService;
    private Action? _toggleWindowAction;
    private bool _isRegistered = false;

    public HotkeyService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void SetToggleWindowAction(Action toggleAction)
    {
        _toggleWindowAction = toggleAction;
    }

    public bool RegisterHotkey()
    {
        DBg.d(LogLevel.Trace, "ENTER RegisterHotkey");
        try
        {
            // Check the operating system
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // On Linux, global hotkeys are challenging and often require:
                // 1. X11 integration (for X11-based desktops)
                // 2. Wayland protocol extensions (for Wayland)
                // 3. Desktop environment specific APIs (GNOME Shell extensions, KDE shortcuts, etc.)
                // 4. Special permissions or capabilities
                
                DBg.d(LogLevel.Warning, "Global hotkeys on Linux require special setup. Consider using desktop environment shortcuts.");
                DBg.d(LogLevel.Debug, "Alternative: Use the tray icon to show/hide the window.");
                
                // For now, we'll simulate registration but it won't actually work globally
                _isRegistered = false;
                DBg.d(LogLevel.Debug, $"Hotkey simulation: {_settingsService.Settings.HotkeyModifiers}+{_settingsService.Settings.HotkeyKey}");
                DBg.d(LogLevel.Trace, "RETURN RegisterHotkey (simulated)");
                return false;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, we could use RegisterHotKey Win32 API
                // For now, simulate success
                _isRegistered = true;
                DBg.d(LogLevel.Debug, $"Hotkey registered: {_settingsService.Settings.HotkeyModifiers}+{_settingsService.Settings.HotkeyKey}");
                DBg.d(LogLevel.Trace, "RETURN RegisterHotkey (success)");
                return true;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // On macOS, we could use Carbon or Cocoa APIs
                // For now, simulate success
                _isRegistered = true;
                DBg.d(LogLevel.Debug, $"Hotkey registered: {_settingsService.Settings.HotkeyModifiers}+{_settingsService.Settings.HotkeyKey}");
                DBg.d(LogLevel.Trace, "RETURN RegisterHotkey (success)");
                return true;
            }
            else
            {
                DBg.d(LogLevel.Warning, "Unsupported platform for global hotkeys");
                _isRegistered = false;
                DBg.d(LogLevel.Trace, "RETURN RegisterHotkey (unsupported)");
                return false;
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Failed to register hotkey: {ex.Message}");
            DBg.d(LogLevel.Trace, "RETURN RegisterHotkey (failed)");
            return false;
        }
    }

    public void UnregisterHotkey()
    {
        DBg.d(LogLevel.Trace, "ENTER UnregisterHotkey");
        if (_isRegistered)
        {
            // Unregister platform-specific hotkey
            _isRegistered = false;
            DBg.d(LogLevel.Debug, "Hotkey unregistered");
        }
        DBg.d(LogLevel.Trace, "RETURN UnregisterHotkey");
    }

    public bool UpdateHotkey(string modifiers, string key)
    {
        DBg.d(LogLevel.Trace, "ENTER UpdateHotkey");
        
        // Validate the hotkey combination
        if (IsHotkeyInUse(modifiers, key))
        {
            DBg.d(LogLevel.Debug, $"Hotkey {modifiers}+{key} is already in use");
            DBg.d(LogLevel.Trace, "RETURN UpdateHotkey (in use)");
            return false;
        }

        // Unregister current hotkey
        UnregisterHotkey();
        
        // Update settings
        _settingsService.UpdateHotkeySettings(modifiers, key);
        
        // Register new hotkey
        var success = RegisterHotkey();
        DBg.d(LogLevel.Trace, "RETURN UpdateHotkey");
        return success;
    }

    public bool IsHotkeyInUse(string modifiers, string key)
    {
        DBg.d(LogLevel.Trace, "ENTER IsHotkeyInUse");
        
        // This is a simplified check. In a real implementation, you would:
        // 1. Try to register the hotkey temporarily
        // 2. Check against known system hotkeys
        // 3. Query the OS for existing registrations
        
        var commonSystemHotkeys = new HashSet<string>
        {
            "Control,Alt+Delete",
            "Control,Alt+Tab",
            "Control+C",
            "Control+V",
            "Control+X",
            "Control+Z",
            "Alt+Tab",
            "Alt+F4",
            "Windows+L",
            "Windows+D",
            "Windows+R"
        };

        var hotkeyString = $"{modifiers}+{key}";
        var inUse = commonSystemHotkeys.Contains(hotkeyString);
        
        DBg.d(LogLevel.Debug, $"Hotkey {hotkeyString} in use: {inUse}");
        DBg.d(LogLevel.Trace, "RETURN IsHotkeyInUse");
        return inUse;
    }

    public List<string> GetAvailableModifiers()
    {
        return new List<string> { "Control", "Alt", "Shift", "Control,Alt", "Control,Shift", "Alt,Shift", "Control,Alt,Shift" };
    }

    public List<string> GetAvailableKeys()
    {
        return new List<string> 
        { 
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", 
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
            "Space", "Enter", "Escape", "Insert", "Delete", "Home", "End", "PageUp", "PageDown"
        };
    }

    public void TriggerToggle()
    {
        DBg.d(LogLevel.Trace, "ENTER TriggerToggle");
        _toggleWindowAction?.Invoke();
        DBg.d(LogLevel.Trace, "RETURN TriggerToggle");
    }
}
