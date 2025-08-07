using System;
using System.Threading;
using System.Threading.Tasks;
using GeFeSLE.Models;

namespace GeFeSLE.Services;

/// <summary>
/// Background service that monitors user session health and automatically re-authenticates when needed.
/// </summary>
public class SessionHeartbeatService : IDisposable
{
    private readonly GeFeSLEApiClient _apiClient;
    private readonly SettingsService _settingsService;
    private readonly Timer _heartbeatTimer;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes
    private bool _disposed = false;
    
    // Event to notify when authentication status changes
    public event EventHandler<SessionStatusChangedEventArgs>? SessionStatusChanged;

    public SessionHeartbeatService(GeFeSLEApiClient apiClient, SettingsService settingsService)
    {
        _apiClient = apiClient;
        _settingsService = settingsService;
        
        // Create timer but don't start it yet
        _heartbeatTimer = new Timer(OnHeartbeatTimer, null, Timeout.Infinite, Timeout.Infinite);
        
        DBg.d(LogLevel.Debug, "SessionHeartbeatService initialized");
    }

    /// <summary>
    /// Starts the heartbeat monitoring. Call this after the user is initially logged in.
    /// </summary>
    public void StartHeartbeat()
    {
        if (_disposed)
            return;
            
        // Only start if we have the necessary settings
        if (string.IsNullOrEmpty(_settingsService.Settings.ServerUrl) ||
            string.IsNullOrEmpty(_settingsService.Settings.Username))
        {
            DBg.d(LogLevel.Warning, "Cannot start heartbeat - missing server URL or username");
            return;
        }

        DBg.d(LogLevel.Debug, $"Starting session heartbeat every {_heartbeatInterval.TotalMinutes} minutes");
        
        // Start the timer - first check in 30 seconds, then every 5 minutes
        _heartbeatTimer.Change(TimeSpan.FromSeconds(30), _heartbeatInterval);
    }

    /// <summary>
    /// Stops the heartbeat monitoring.
    /// </summary>
    public void StopHeartbeat()
    {
        if (_disposed)
            return;
            
        DBg.d(LogLevel.Debug, "Stopping session heartbeat");
        _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Manually trigger a heartbeat check (useful for testing or immediate validation).
    /// </summary>
    public async Task<bool> CheckSessionNow()
    {
        return await PerformHeartbeatCheck();
    }

    private async void OnHeartbeatTimer(object? state)
    {
        try
        {
            await PerformHeartbeatCheck();
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error in heartbeat timer: {ex.Message}");
        }
    }

    private async Task<bool> PerformHeartbeatCheck()
    {
        DBg.d(LogLevel.Debug, "Performing session heartbeat check");
        
        try
        {
            // Ensure API client has the correct base address
            if (!string.IsNullOrEmpty(_settingsService.Settings.ServerUrl))
            {
                _apiClient.SetBaseAddress(_settingsService.Settings.ServerUrl);
            }
            
            // Call GET /me to check session status
            var currentUser = await _apiClient.GetCurrentUserAsync();
            
            if (currentUser != null && currentUser.IsAuthenticated)
            {
                // Session is valid
                var expectedUsername = _settingsService.Settings.Username;
                
                if (!string.IsNullOrEmpty(expectedUsername) && 
                    string.Equals(currentUser.UserName, expectedUsername, StringComparison.OrdinalIgnoreCase))
                {
                    DBg.d(LogLevel.Debug, $"Session heartbeat OK - authenticated as {currentUser.UserName}");
                    
                    // Update session cookies if they've changed
                    var currentCookies = _apiClient.GetSessionCookies();
                    if (!string.IsNullOrEmpty(currentCookies) && 
                        currentCookies != _settingsService.Settings.SessionCookies)
                    {
                        _settingsService.UpdateSessionCookies(currentCookies);
                        DBg.d(LogLevel.Debug, "Updated session cookies from heartbeat");
                    }
                    
                    // Notify that session is healthy
                    SessionStatusChanged?.Invoke(this, new SessionStatusChangedEventArgs
                    {
                        IsAuthenticated = true,
                        Username = currentUser.UserName,
                        StatusMessage = "Session validated"
                    });
                    
                    return true;
                }
                else
                {
                    DBg.d(LogLevel.Warning, $"Session heartbeat - username mismatch. Expected: {expectedUsername}, Got: {currentUser.UserName}");
                    // Different user is logged in, treat as unauthenticated
                    return await HandleSessionExpired("Username mismatch - different user logged in");
                }
            }
            else
            {
                DBg.d(LogLevel.Debug, "Session heartbeat - user not authenticated, attempting re-login");
                return await HandleSessionExpired("Session expired");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error during session heartbeat: {ex.Message}");
            
            // On network errors, don't try to re-authenticate, just log and continue
            // The heartbeat will try again on the next interval
            SessionStatusChanged?.Invoke(this, new SessionStatusChangedEventArgs
            {
                IsAuthenticated = false,
                StatusMessage = $"Heartbeat check failed: {ex.Message}"
            });
            
            return false;
        }
    }

    private async Task<bool> HandleSessionExpired(string reason)
    {
        DBg.d(LogLevel.Debug, $"Handling session expiry: {reason}");
        
        // Clear existing session cookies since they're no longer valid
        _settingsService.UpdateSessionCookies(null);
        
        // Try to re-authenticate if we have saved credentials and RememberLogin is enabled
        if (_settingsService.Settings.RememberLogin &&
            !string.IsNullOrEmpty(_settingsService.Settings.Username) &&
            !string.IsNullOrEmpty(_settingsService.GetPassword()))
        {
            DBg.d(LogLevel.Debug, "Attempting automatic re-authentication");
            
            try
            {
                var loginDto = new LoginDto
                {
                    Username = _settingsService.Settings.Username,
                    Password = _settingsService.GetPassword()
                };
                
                var loginResponse = await _apiClient.LoginAsync(loginDto);
                
                if (loginResponse.Success)
                {
                    DBg.d(LogLevel.Debug, "Automatic re-authentication successful");
                    
                    // Update session cookies
                    var newCookies = _apiClient.GetSessionCookies();
                    if (!string.IsNullOrEmpty(newCookies))
                    {
                        _settingsService.UpdateSessionCookies(newCookies);
                    }
                    
                    // Notify successful re-authentication
                    SessionStatusChanged?.Invoke(this, new SessionStatusChangedEventArgs
                    {
                        IsAuthenticated = true,
                        Username = _settingsService.Settings.Username,
                        StatusMessage = "Automatically re-authenticated"
                    });
                    
                    return true;
                }
                else
                {
                    DBg.d(LogLevel.Warning, $"Automatic re-authentication failed: {loginResponse.ErrorMessage}");
                    
                    // Notify that re-authentication failed
                    SessionStatusChanged?.Invoke(this, new SessionStatusChangedEventArgs
                    {
                        IsAuthenticated = false,
                        StatusMessage = $"Re-authentication failed: {loginResponse.ErrorMessage}"
                    });
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Error, $"Error during automatic re-authentication: {ex.Message}");
                
                SessionStatusChanged?.Invoke(this, new SessionStatusChangedEventArgs
                {
                    IsAuthenticated = false,
                    StatusMessage = $"Re-authentication error: {ex.Message}"
                });
                
                return false;
            }
        }
        else
        {
            DBg.d(LogLevel.Debug, "Cannot attempt re-authentication - missing credentials or RememberLogin disabled");
            
            // Notify that session expired and no auto-login is possible
            SessionStatusChanged?.Invoke(this, new SessionStatusChangedEventArgs
            {
                IsAuthenticated = false,
                StatusMessage = "Session expired - please log in again"
            });
            
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _heartbeatTimer?.Dispose();
            _disposed = true;
            DBg.d(LogLevel.Debug, "SessionHeartbeatService disposed");
        }
    }
}

/// <summary>
/// Event arguments for session status changes.
/// </summary>
public class SessionStatusChangedEventArgs : EventArgs
{
    public bool IsAuthenticated { get; set; }
    public string? Username { get; set; }
    public string? StatusMessage { get; set; }
}
