using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

public static class DBg
{
    public static void d(LogLevel level,
            string? msg,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string member = "")
    {
        string now = DateTime.Now.ToString("s");
        var debugNfo = "";
        if(file is not null && member is not null) {
            string normalizedFile = file.Replace('/', Path.DirectorySeparatorChar)
                                .Replace('\\', Path.DirectorySeparatorChar);
            string filename = Path.GetFileName(normalizedFile);
            debugNfo = $"[{member}//{filename}:{line}]";
        }
        if (level < GlobalConfig.CURRENT_LEVEL)
        {
            return;
        }
        switch (level)
        {
            case LogLevel.Trace:
                if(debugNfo is not null) {
                    Console.WriteLine($"{now} TRACE | {debugNfo} {msg}");
                } else {
                    Console.WriteLine($"{now} TRACE | {msg}");
                }
                return;
            case LogLevel.Debug:
                if(debugNfo is not null){
                    Console.WriteLine($"{now} DEBUG | {debugNfo} {msg}");
                } else {
                    Console.WriteLine($"{now} DEBUG | {msg}");
                }
                return;
            case LogLevel.Information:
                Console.WriteLine($"{now} INFO  | {msg}");
                return;
            case LogLevel.Warning:
                Console.WriteLine($"{now} WARN  | {msg}");
                return;
            case LogLevel.Error:
                Console.WriteLine($"{now} ERROR | {msg}");
                return;
            case LogLevel.Critical:
                Console.WriteLine($"{now} FATAL | {msg}");
                return;
            default:
                Console.WriteLine($"db.dump | FATAL: Unexpected value for level: {msg}");
                return;
        }
    }

    /// <summary>
    /// Gets the current memory usage in MB
    /// </summary>
    /// <returns>Memory usage string formatted as "XX.X MB"</returns>
    public static string GetMemoryUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / (1024.0 * 1024.0);
            return $"{workingSetMB:F1} MB";
        }
        catch (Exception ex)
        {
            d(LogLevel.Warning, $"Failed to get memory usage: {ex.Message}");
            return "-- MB";
        }
    }

    /// <summary>
    /// Gets detailed memory information for debugging
    /// </summary>
    /// <returns>Detailed memory info string</returns>
    public static string GetDetailedMemoryInfo()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / (1024.0 * 1024.0);
            var gcMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            return $"Working Set: {workingSetMB:F1} MB, GC Memory: {gcMemoryMB:F1} MB";
        }
        catch (Exception ex)
        {
            d(LogLevel.Warning, $"Failed to get detailed memory info: {ex.Message}");
            return "Memory info unavailable";
        }
    }
}
