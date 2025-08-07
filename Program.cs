using Avalonia;
using System;
using GeFeSLE.Services;

namespace GeFeSLE;

sealed class Program
{
    private static SingleInstanceService? _singleInstance;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Check for single instance before initializing Avalonia
        _singleInstance = new SingleInstanceService();
        
        if (!_singleInstance.TryAcquire())
        {
            Console.WriteLine("Another instance of GeFeSLE-systray is already running.");
            Environment.Exit(1);
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Clean up single instance lock
            _singleInstance?.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
