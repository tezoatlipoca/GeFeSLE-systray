using System;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace GeFeSLE.Services;

/// <summary>
/// Service to ensure only one instance of the application is running at a time.
/// Uses Mutex on Windows and file locking on Linux for cross-platform compatibility.
/// </summary>
public class SingleInstanceService : IDisposable
{
    private readonly string _mutexName;
    private readonly string _lockFilePath;
    private Mutex? _mutex;
    private FileStream? _lockFileStream;
    private bool _hasAcquiredLock = false;
    private bool _disposed = false;

    public SingleInstanceService(string applicationName = "GeFeSLE-systray")
    {
        // For Windows, use a simple mutex name like your server application
        _mutexName = applicationName;
        
        // For Linux, use a lock file in /tmp
        _lockFilePath = Path.Combine(Path.GetTempPath(), $"{applicationName}_SingleInstance.lock");
        
        DBg.d(LogLevel.Debug, $"SingleInstance initialized - Mutex: {_mutexName}, LockFile: {_lockFilePath}");
    }

    /// <summary>
    /// Attempts to acquire the single instance lock.
    /// </summary>
    /// <returns>True if this is the first instance, false if another instance is already running</returns>
    public bool TryAcquire()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return TryAcquireWindows();
            }
            else
            {
                return TryAcquireLinux();
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Failed to acquire single instance lock: {ex.Message}");
            return true; // Allow application to continue on error
        }
    }

    private bool TryAcquireWindows()
    {
        try
        {
            // Try to create or open the mutex - use the same pattern as your server
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            
            // If we created a new mutex, we are the first instance
            if (createdNew)
            {
                _hasAcquiredLock = true;
                DBg.d(LogLevel.Debug, $"Windows mutex '{_mutexName}' created new - first instance");
                return true;
            }
            else
            {
                // Mutex already exists, another instance is running
                DBg.d(LogLevel.Debug, $"Windows mutex '{_mutexName}' already exists - another instance running");
                _mutex?.Dispose();
                _mutex = null;
                return false;
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Failed to acquire Windows mutex: {ex.Message}");
            return true; // Allow application to continue on error
        }
    }

    private bool TryAcquireLinux()
    {
        try
        {
            // Clean up any stale lock files from crashed processes
            CleanupStaleLockFile();
            
            // Try to create and lock the file exclusively
            _lockFileStream = new FileStream(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            
            // Write our process ID to the lock file
            var processId = Environment.ProcessId.ToString();
            var data = System.Text.Encoding.UTF8.GetBytes(processId);
            _lockFileStream.Write(data, 0, data.Length);
            _lockFileStream.Flush();
            
            _hasAcquiredLock = true;
            DBg.d(LogLevel.Debug, $"Linux lock file '{_lockFilePath}' created and acquired (PID: {processId})");
            
            return true;
        }
        catch (IOException ex) when (ex.Message.Contains("being used by another process") || 
                                   ex.Message.Contains("Resource temporarily unavailable"))
        {
            // File is locked by another process
            DBg.d(LogLevel.Debug, $"Linux lock file '{_lockFilePath}' is already locked by another instance");
            return false;
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Failed to acquire Linux lock file: {ex.Message}");
            return true; // Allow application to continue on error
        }
    }

    private void CleanupStaleLockFile()
    {
        try
        {
            if (File.Exists(_lockFilePath))
            {
                // Try to read the PID from the lock file
                var pidText = File.ReadAllText(_lockFilePath).Trim();
                if (int.TryParse(pidText, out int pid))
                {
                    // Check if the process is still running
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        if (process.ProcessName.Contains("GeFeSLE") || process.ProcessName.Contains("dotnet"))
                        {
                            // Process is still running, don't clean up
                            DBg.d(LogLevel.Debug, $"Lock file PID {pid} is still running: {process.ProcessName}");
                            return;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process doesn't exist, safe to clean up
                        DBg.d(LogLevel.Debug, $"Lock file PID {pid} no longer exists, cleaning up stale lock file");
                    }
                }
                
                // Clean up stale lock file
                File.Delete(_lockFilePath);
                DBg.d(LogLevel.Debug, $"Cleaned up stale lock file: {_lockFilePath}");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Warning, $"Failed to cleanup stale lock file: {ex.Message}");
        }
    }

    /// <summary>
    /// Releases the single instance lock.
    /// </summary>
    public void Release()
    {
        if (_hasAcquiredLock && !_disposed)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (_mutex != null)
                    {
                        _mutex.ReleaseMutex();
                        DBg.d(LogLevel.Debug, "Released Windows mutex");
                    }
                }
                else
                {
                    _lockFileStream?.Close();
                    _lockFileStream?.Dispose();
                    _lockFileStream = null;
                    
                    // Clean up the lock file
                    if (File.Exists(_lockFilePath))
                    {
                        File.Delete(_lockFilePath);
                        DBg.d(LogLevel.Debug, $"Released and deleted Linux lock file: {_lockFilePath}");
                    }
                }
                
                _hasAcquiredLock = false;
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Warning, $"Failed to release single instance lock: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Release();
            _mutex?.Dispose();
            _lockFileStream?.Dispose();
            _disposed = true;
        }
    }
}
