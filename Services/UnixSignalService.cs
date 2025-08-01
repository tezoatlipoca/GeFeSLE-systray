using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace GeFeSLE.Services;

public class UnixSignalService
{
    private const int SIGUSR1 = 10;
    private const int SIGTERM = 15;
    private const int SIGINT = 2;
    private Action? _toggleWindowAction;
    private Action? _persistConfigAction;
    private bool _isListening = false;
    private CancellationTokenSource? _cancellationTokenSource;

    // P/Invoke declarations for Unix signal handling
    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr signal(int signum, IntPtr handler);

    [DllImport("libc")]
    private static extern int getpid();

    // Signal handler delegate
    private delegate void SignalHandler(int signal);
    private SignalHandler? _signalHandler;

    public UnixSignalService()
    {
        DBg.d(LogLevel.Trace, "ENTER UnixSignalService.ctor");
        DBg.d(LogLevel.Trace, "RETURN UnixSignalService.ctor");
    }

    public void SetToggleWindowAction(Action toggleAction)
    {
        DBg.d(LogLevel.Trace, "ENTER SetToggleWindowAction");
        _toggleWindowAction = toggleAction;
        DBg.d(LogLevel.Trace, "RETURN SetToggleWindowAction");
    }

    public void SetPersistConfigAction(Action persistAction)
    {
        DBg.d(LogLevel.Trace, "ENTER SetPersistConfigAction");
        _persistConfigAction = persistAction;
        DBg.d(LogLevel.Trace, "RETURN SetPersistConfigAction");
    }

    public bool StartListening()
    {
        DBg.d(LogLevel.Trace, "ENTER StartListening");

        if (_isListening)
        {
            DBg.d(LogLevel.Debug, "Already listening for signals");
            DBg.d(LogLevel.Trace, "RETURN StartListening (already listening)");
            return true;
        }

        try
        {
            // Only works on Unix-like systems
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && 
                !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                DBg.d(LogLevel.Debug, "Signal handling only available on Unix-like systems");
                DBg.d(LogLevel.Trace, "RETURN StartListening (not unix)");
                return false;
            }

            // Create signal handler
            _signalHandler = new SignalHandler(HandleSignal);
            
            // Register SIGUSR1 handler for window toggle
            var result1 = signal(SIGUSR1, Marshal.GetFunctionPointerForDelegate(_signalHandler));
            
            // Register SIGTERM handler for graceful shutdown
            var result2 = signal(SIGTERM, Marshal.GetFunctionPointerForDelegate(_signalHandler));
            
            // Register SIGINT handler for Ctrl+C
            var result3 = signal(SIGINT, Marshal.GetFunctionPointerForDelegate(_signalHandler));
            
            if (result1 == new IntPtr(-1) || result2 == new IntPtr(-1) || result3 == new IntPtr(-1))
            {
                var error = Marshal.GetLastWin32Error();
                DBg.d(LogLevel.Error, $"Failed to register signal handler: {error}");
                DBg.d(LogLevel.Trace, "RETURN StartListening (failed)");
                return false;
            }

            _isListening = true;
            _cancellationTokenSource = new CancellationTokenSource();

            var pid = getpid();
            DBg.d(LogLevel.Debug, $"Signal handlers registered for SIGUSR1, SIGTERM, SIGINT. Process ID: {pid}");
            DBg.d(LogLevel.Debug, $"To toggle window: pkill -SIGUSR1 GeFeSLE-systray");
            DBg.d(LogLevel.Debug, $"To gracefully shutdown: pkill -SIGTERM GeFeSLE-systray");
            
            DBg.d(LogLevel.Trace, "RETURN StartListening (success)");
            return true;
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Exception setting up signal handler: {ex.Message}");
            DBg.d(LogLevel.Trace, "RETURN StartListening (exception)");
            return false;
        }
    }

    public void StopListening()
    {
        DBg.d(LogLevel.Trace, "ENTER StopListening");
        
        if (!_isListening)
        {
            DBg.d(LogLevel.Trace, "RETURN StopListening (not listening)");
            return;
        }

        try
        {
            // Reset signal handlers to default
            signal(SIGUSR1, IntPtr.Zero);
            signal(SIGTERM, IntPtr.Zero);
            signal(SIGINT, IntPtr.Zero);
            
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            
            _isListening = false;
            _signalHandler = null;
            
            DBg.d(LogLevel.Debug, "Signal handlers unregistered");
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Exception stopping signal handler: {ex.Message}");
        }
        
        DBg.d(LogLevel.Trace, "RETURN StopListening");
    }

    private void HandleSignal(int signal)
    {
        DBg.d(LogLevel.Trace, "ENTER HandleSignal");
        
        if (signal == SIGUSR1)
        {
            DBg.d(LogLevel.Debug, "Received SIGUSR1 - toggling window");
            
            // Execute toggle action on the UI thread using Avalonia's Dispatcher
            try
            {
                // Marshal to UI thread using Avalonia's Dispatcher
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        DBg.d(LogLevel.Debug, "Executing toggle action on UI thread");
                        _toggleWindowAction?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        DBg.d(LogLevel.Error, $"Error in toggle action on UI thread: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Error, $"Error marshaling to UI thread: {ex.Message}");
            }
        }
        else if (signal == SIGTERM || signal == SIGINT)
        {
            string signalName = signal == SIGTERM ? "SIGTERM" : "SIGINT";
            DBg.d(LogLevel.Debug, $"Received {signalName} - initiating graceful shutdown");
            
            try
            {
                // First persist configuration synchronously (signal handler context)
                DBg.d(LogLevel.Debug, "Persisting configuration before shutdown");
                _persistConfigAction?.Invoke();
                
                // Then marshal shutdown to UI thread
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        DBg.d(LogLevel.Debug, "Initiating application shutdown from UI thread");
                        
                        // Get the application lifetime and shutdown
                        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                        {
                            desktop.Shutdown(0);
                        }
                        else
                        {
                            DBg.d(LogLevel.Warning, "Could not access application lifetime for shutdown");
                            Environment.Exit(0);
                        }
                    }
                    catch (Exception ex)
                    {
                        DBg.d(LogLevel.Error, $"Error in shutdown action on UI thread: {ex.Message}");
                        Environment.Exit(1);
                    }
                });
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Error, $"Error handling {signalName} signal: {ex.Message}");
                Environment.Exit(1);
            }
        }
        
        DBg.d(LogLevel.Trace, "RETURN HandleSignal");
    }

    public bool IsListening => _isListening;

    public void Dispose()
    {
        DBg.d(LogLevel.Trace, "ENTER Dispose");
        StopListening();
        DBg.d(LogLevel.Trace, "RETURN Dispose");
    }
}
