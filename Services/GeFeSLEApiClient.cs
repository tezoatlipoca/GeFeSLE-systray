using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GeFeSLE.Models;
using System.Threading.Tasks;
using System.Net;

namespace GeFeSLE.Services;

public class GeFeSLEApiClient
{
    private HttpClient _httpClient;
    private CookieContainer _cookieContainer;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _baseAddress;

    public GeFeSLEApiClient(HttpClient httpClient)
    {
        DBg.d(LogLevel.Trace, "ENTER");
        _cookieContainer = new CookieContainer();
        _httpClient = httpClient;
        _baseAddress = httpClient.BaseAddress?.ToString();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        DBg.d(LogLevel.Trace, "RETURN");
    }

    public void SetBaseAddress(string baseUrl)
    {
        DBg.d(LogLevel.Trace, "ENTER");
        if (_baseAddress != baseUrl)
        {
            _baseAddress = baseUrl;
            var handler = new HttpClientHandler() { CookieContainer = _cookieContainer };
            _httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        }
        DBg.d(LogLevel.Trace, "RETURN");
    }

    public void SetSessionCookies(string? cookies)
    {
        DBg.d(LogLevel.Trace, "ENTER");
        if (!string.IsNullOrEmpty(cookies) && !string.IsNullOrEmpty(_baseAddress))
        {
            var uri = new Uri(_baseAddress);
            var cookiePairs = cookies.Split(';');
            foreach (var pair in cookiePairs)
            {
                var parts = pair.Trim().Split('=', 2);
                if (parts.Length == 2)
                {
                    _cookieContainer.Add(new Cookie(parts[0], parts[1], "/", uri.Host));
                }
            }
        }
        DBg.d(LogLevel.Trace, "RETURN");
    }

    public string? GetSessionCookies()
    {
        DBg.d(LogLevel.Trace, "ENTER");
        if (!string.IsNullOrEmpty(_baseAddress))
        {
            var uri = new Uri(_baseAddress);
            var cookies = _cookieContainer.GetCookies(uri);
            if (cookies.Count > 0)
            {
                var cookieStrings = new List<string>();
                foreach (Cookie cookie in cookies)
                {
                    cookieStrings.Add($"{cookie.Name}={cookie.Value}");
                }
                var result = string.Join("; ", cookieStrings);
                DBg.d(LogLevel.Trace, "RETURN");
                return result;
            }
        }
        DBg.d(LogLevel.Trace, "RETURN (null)");
        return null;
    }

    // If you need to update login info (e.g., add auth headers/cookies),
    // do it here without recreating the HttpClient:
    public void SetAuthHeader(string name, string value)
    {
        DBg.d(LogLevel.Trace, "ENTER");
        if (_httpClient.DefaultRequestHeaders.Contains(name))
            _httpClient.DefaultRequestHeaders.Remove(name);
        _httpClient.DefaultRequestHeaders.Add(name, value);
        DBg.d(LogLevel.Trace, "RETURN");
    }

    // User authentication and management
    public async Task<UserDto?> GetCurrentUserAsync()
    {
        DBg.d(LogLevel.Trace, "ENTER");
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/me");
            request.Headers.Add("GeFeSLE-XMLHttpRequest", "true");
            DBg.d(LogLevel.Debug, $"GET /me");
            var response = await _httpClient.SendAsync(request);
            DBg.d(LogLevel.Debug, $"Response: {(int)response.StatusCode} {response.StatusCode}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Response Body: {json}");
                DBg.d(LogLevel.Trace, "RETURN (success)");
                return JsonSerializer.Deserialize<UserDto>(json, _jsonOptions);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Error Body: {error}");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error getting current user: {ex.Message}");
        }
        DBg.d(LogLevel.Trace, "RETURN (null)");
        return null;
    }

    public async Task<LoginResponse> LoginAsync(LoginDto loginDto)
    {
        DBg.d(LogLevel.Trace, "ENTER");
        try
        {
            // Create form-urlencoded content
            var formParams = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrEmpty(loginDto.Username))
                formParams.Add(new KeyValuePair<string, string>("Username", loginDto.Username));
            if (!string.IsNullOrEmpty(loginDto.Password))
                formParams.Add(new KeyValuePair<string, string>("Password", loginDto.Password));
            if (!string.IsNullOrEmpty(loginDto.OAuthProvider))
                formParams.Add(new KeyValuePair<string, string>("OAuthProvider", loginDto.OAuthProvider));
            if (!string.IsNullOrEmpty(loginDto.Instance))
                formParams.Add(new KeyValuePair<string, string>("Instance", loginDto.Instance));

            var content = new FormUrlEncodedContent(formParams);
            var request = new HttpRequestMessage(HttpMethod.Post, "/me") { Content = content };
            request.Headers.Add("GeFeSLE-XMLHttpRequest", "true");
            
            var formData = string.Join("&", formParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            DBg.d(LogLevel.Debug, $"POST /me Form Data: {formData}");
            
            var response = await _httpClient.SendAsync(request);
            DBg.d(LogLevel.Debug, $"Response: {(int)response.StatusCode} {response.StatusCode}");
            
            var respBody = await response.Content.ReadAsStringAsync();
            DBg.d(LogLevel.Debug, $"Response Body: {respBody}");
            
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    // Try to deserialize as JSON response (API mode)
                    var apiResponse = JsonSerializer.Deserialize<dynamic>(respBody, _jsonOptions);
                    if (apiResponse != null)
                    {
                        var jsonElement = (JsonElement)apiResponse;
                        var username = jsonElement.TryGetProperty("username", out var usernameProp) ? usernameProp.GetString() : null;
                        var role = jsonElement.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;
                        
                        DBg.d(LogLevel.Trace, "RETURN (success - API response)");
                        return new LoginResponse { Success = true, Username = username, Role = role };
                    }
                }
                catch
                {
                    // Response might be HTML (redirect), which is also success
                    DBg.d(LogLevel.Debug, "Response appears to be HTML (redirect), treating as success");
                }
                
                DBg.d(LogLevel.Trace, "RETURN (success - HTML response)");
                return new LoginResponse { Success = true };
            }
            else
            {
                DBg.d(LogLevel.Trace, "RETURN (failed)");
                return new LoginResponse { Success = false, ErrorMessage = $"Login failed: {response.StatusCode}" };
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error during login: {ex.Message}");
            DBg.d(LogLevel.Trace, "RETURN (exception)");
            return new LoginResponse { Success = false, ErrorMessage = $"Login error: {ex.Message}" };
        }
    }

    // List management
    public async Task<List<GeList>?> GetListsAsync()
    {
        DBg.d(LogLevel.Trace, "ENTER");
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/lists");
            request.Headers.Add("GeFeSLE-XMLHttpRequest", "true");
            DBg.d(LogLevel.Debug, $"GET /lists");
            var response = await _httpClient.SendAsync(request);
            DBg.d(LogLevel.Debug, $"Response: {(int)response.StatusCode} {response.StatusCode}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Response Body: {json}");
                //DBg.d(LogLevel.Trace, "RETURN (success)");
                return JsonSerializer.Deserialize<List<GeList>>(json, _jsonOptions);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Error Body: {error}");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error getting lists: {ex.Message}");
        }
        DBg.d(LogLevel.Trace, "RETURN (null)");
        return null;
    }

    public async Task<List<GeListItem>?> GetListItemsAsync(int listId)
    {
        DBg.d(LogLevel.Trace, "ENTER");
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/showitems/{listId}");
            request.Headers.Add("GeFeSLE-XMLHttpRequest", "true");
            DBg.d(LogLevel.Debug, $"GET /showitems/{listId}");
            var response = await _httpClient.SendAsync(request);
            DBg.d(LogLevel.Debug, $"Response: {(int)response.StatusCode} {response.StatusCode}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Response Body: {json}");
                return JsonSerializer.Deserialize<List<GeListItem>>(json, _jsonOptions);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Error Body: {error}");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error getting list items: {ex.Message}");
        }
        DBg.d(LogLevel.Trace, "RETURN (null)");
        return null;
    }

    public async Task<bool> UpdateItemAsync(GeListItem item)
    {
        DBg.d(LogLevel.Trace, "ENTER");
        try
        {
            var json = JsonSerializer.Serialize(item, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Put, "/modifyitem") { Content = content };
            request.Headers.Add("GeFeSLE-XMLHttpRequest", "true");
            
            DBg.d(LogLevel.Debug, "PUT /modifyitem");
            DBg.d(LogLevel.Debug, $"Request Body: {json}");
            
            var response = await _httpClient.SendAsync(request);
            DBg.d(LogLevel.Debug, $"Response: {(int)response.StatusCode} {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Response Body: {responseBody}");
                DBg.d(LogLevel.Trace, "RETURN (success)");
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Error Body: {error}");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error updating item: {ex.Message}");
        }
        DBg.d(LogLevel.Trace, "RETURN (false)");
        return false;
    }

    public async Task<bool> AddItemAsync(int listId, GeListItem item)
    {
        DBg.d(LogLevel.Trace, "ENTER");
        try
        {
            var json = JsonSerializer.Serialize(item, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"/additem/{listId}") { Content = content };
            request.Headers.Add("GeFeSLE-XMLHttpRequest", "true");
            
            DBg.d(LogLevel.Debug, $"POST /additem/{listId}");
            DBg.d(LogLevel.Debug, $"Request Body: {json}");
            
            var response = await _httpClient.SendAsync(request);
            DBg.d(LogLevel.Debug, $"Response: {(int)response.StatusCode} {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Response Body: {responseBody}");
                DBg.d(LogLevel.Trace, "RETURN (success)");
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                DBg.d(LogLevel.Debug, $"Error Body: {error}");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error adding item: {ex.Message}");
        }
        DBg.d(LogLevel.Trace, "RETURN (false)");
        return false;
    }
}
