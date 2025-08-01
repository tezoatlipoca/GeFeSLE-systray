# Linux Hotkey Setup

Since global hotkeys require special permissions on Linux and vary by desktop environment, here are alternative approaches:

## Option 1: Use the System Tray Icon (Most Reliable)
Simply click the GeFeSLE system tray icon to toggle window visibility. This is the most reliable method across all desktop environments.

## Option 2: Desktop Environment Keyboard Shortcuts (Now Available!)

**Signal handling is now implemented!** You can use `pkill -SIGUSR1 GeFeSLE-systray` to toggle window visibility.

### What the signals do:
- **`pkill -SIGUSR1 GeFeSLE-systray`**: Toggles window visibility (show if hidden, hide if visible)
- **`pkill -SIGTERM GeFeSLE-systray`**: Gracefully shuts down the application, saving all configuration
- **`pkill -SIGINT GeFeSLE-systray`**: Same as Ctrl+C, gracefully shuts down with configuration save

### How signal commands work:
- **`pkill`**: Finds processes by name and sends them a signal
- **`-SIGUSR1`**: Sends "User Signal 1" (custom signal for window toggle)
- **`-SIGTERM`**: Sends "Terminate" signal (graceful shutdown)
- **`-SIGINT`**: Sends "Interrupt" signal (like Ctrl+C)

### Setting up desktop environment shortcuts:

#### GNOME (Ubuntu default, Fedora Workstation)
1. Open Settings → Keyboard → View and Customize Shortcuts
2. Click the + button to add a custom shortcut
3. Name: "Toggle GeFeSLE Window"
4. Command: `pkill -SIGUSR1 GeFeSLE-systray`
5. Set your preferred key combination (e.g., Ctrl+Alt+G)

Optional: Add a shutdown shortcut:
- Name: "Shutdown GeFeSLE"
- Command: `pkill -SIGTERM GeFeSLE-systray`
- Key combination: (e.g., Ctrl+Alt+Shift+G)

#### KDE Plasma
1. System Settings → Shortcuts → Custom Shortcuts
2. Edit → New → Global Shortcut → Command/URL
3. Name: "Toggle GeFeSLE Window"
4. Command: `pkill -SIGUSR1 GeFeSLE-systray`
5. Set trigger key combination

Optional shutdown shortcut:
- Name: "Shutdown GeFeSLE"
- Command: `pkill -SIGTERM GeFeSLE-systray`

#### XFCE
1. Settings → Keyboard → Application Shortcuts
2. Add a new shortcut
3. Command: `pkill -SIGUSR1 GeFeSLE-systray`
4. Set your key combination

Add shutdown shortcut:
- Command: `pkill -SIGTERM GeFeSLE-systray`

## Option 3: Using a Toggle Script (Current Workaround)

Create a simple script that can be called from desktop environment shortcuts:

### Method A: Process-based toggle with graceful shutdown
Create `toggle-gefesle.sh`:
```bash
#!/bin/bash
# Check if GeFeSLE is running and visible
if pgrep -f "GeFeSLE-systray" > /dev/null; then
    # Use signal to toggle window (now handles graceful shutdown too)
    pkill -SIGUSR1 GeFeSLE-systray
else
    # Start the application
    nohup /path/to/GeFeSLE-systray &
fi
```

Create `shutdown-gefesle.sh` for graceful shutdown:
```bash
#!/bin/bash
# Gracefully shutdown GeFeSLE with configuration save
if pgrep -f "GeFeSLE-systray" > /dev/null; then
    echo "Shutting down GeFeSLE gracefully..."
    pkill -SIGTERM GeFeSLE-systray
else
    echo "GeFeSLE is not running"
fi
```

Make it executable: `chmod +x toggle-gefesle.sh`

Then use this script path in your desktop environment's keyboard shortcut settings.

### Method B: Simple launcher script
Create `show-gefesle.sh`:
```bash
#!/bin/bash
# Simple script to ensure GeFeSLE is running and attempt to show it
if ! pgrep -f "GeFeSLE-systray" > /dev/null; then
    # Start if not running
    nohup /path/to/GeFeSLE-systray &
else
    # Already running - user should click tray icon
    notify-send "GeFeSLE" "Application is running. Click the tray icon to toggle visibility." 2>/dev/null || echo "GeFeSLE is running - use tray icon"
fi
```

## Option 4: Using wmctrl (if available)
Install wmctrl: `sudo apt install wmctrl` (Ubuntu/Debian) or equivalent

Create a script to toggle the window:
```bash
#!/bin/bash
if wmctrl -l | grep -q "GeFeSLE"; then
    wmctrl -c "GeFeSLE"
else
    # Launch if not running, or use process signal
    pkill -SIGUSR1 GeFeSLE-systray
fi
```

## Technical Note
Global hotkey registration on Linux requires either:
- X11 integration (XGrabKey)
- Wayland protocol extensions
- Desktop environment specific APIs
- Special capabilities/permissions

These approaches often conflict with desktop environment security policies and require additional dependencies.
