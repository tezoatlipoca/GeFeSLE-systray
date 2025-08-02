using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace GeFeSLE.Services
{
    public class CachedImage
    {
        public Bitmap? Bitmap { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsLoaded { get; set; }
        public DateTime CachedAt { get; set; }
    }

    public class ImageCacheService
    {
        private readonly ConcurrentDictionary<string, CachedImage> _imageCache = new();
        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(24); // Images expire after 24 hours

        public ImageCacheService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // Create cache directory in the application's data folder
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cacheDirectory = Path.Combine(appDataPath, "GeFeSLE-systray", "ImageCache");
            Directory.CreateDirectory(_cacheDirectory);
        }

        public async Task<List<string>> ExtractImageUrlsFromContentAsync(string htmlContent)
        {
            var imageUrls = new HashSet<string>();

            if (string.IsNullOrWhiteSpace(htmlContent))
                return new List<string>(imageUrls);

            await Task.Run(() =>
            {
                try
                {
                    // Parse HTML to find img tags
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(htmlContent);
                    
                    foreach (var imgNode in htmlDoc.DocumentNode.SelectNodes("//img") ?? Enumerable.Empty<HtmlNode>())
                    {
                        var src = imgNode.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src) && IsValidImageUrl(src))
                        {
                            imageUrls.Add(src);
                        }
                    }

                    // Also extract markdown images: ![alt](url)
                    var markdownImageRegex = new Regex(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.IgnoreCase);
                    var markdownMatches = markdownImageRegex.Matches(htmlContent);
                    foreach (Match match in markdownMatches)
                    {
                        var url = match.Groups[2].Value;
                        if (!string.IsNullOrEmpty(url) && IsValidImageUrl(url))
                        {
                            imageUrls.Add(url);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DBg.d(LogLevel.Warning, $"Error extracting image URLs: {ex.Message}");
                }
            });

            return new List<string>(imageUrls);
        }

        public async Task<int> PreloadImagesAsync(List<string> imageUrls, Action<int, int, int>? progressCallback = null)
        {
            var tasks = new List<Task>();
            int completed = 0;
            int successful = 0;
            int total = imageUrls.Count;

            foreach (var url in imageUrls)
            {
                tasks.Add(LoadImageAsync(url).ContinueWith(t =>
                {
                    Interlocked.Increment(ref completed);
                    
                    // Check if the load was successful
                    if (_imageCache.TryGetValue(url, out var cachedImage) && 
                        cachedImage.IsLoaded && cachedImage.Bitmap != null)
                    {
                        Interlocked.Increment(ref successful);
                    }
                    
                    progressCallback?.Invoke(completed, successful, total);
                }));
            }

            await Task.WhenAll(tasks);
            return successful;
        }

        public CachedImage? GetCachedImage(string imageUrl)
        {
            if (_imageCache.TryGetValue(imageUrl, out var cachedImage))
            {
                // Check if cache is still valid
                if (DateTime.Now - cachedImage.CachedAt < CacheExpiry)
                {
                    return cachedImage;
                }
                else
                {
                    // Cache expired, remove it
                    _imageCache.TryRemove(imageUrl, out _);
                    CleanupCachedFile(imageUrl);
                }
            }
            return null;
        }

        private async Task<CachedImage> LoadImageAsync(string imageUrl)
        {
            // Check memory cache first
            var existing = GetCachedImage(imageUrl);
            if (existing != null)
            {
                return existing;
            }

            var cachedImage = new CachedImage
            {
                CachedAt = DateTime.Now,
                IsLoaded = false
            };

            try
            {
                // Check disk cache first
                var cachedFilePath = GetCacheFilePath(imageUrl);
                if (File.Exists(cachedFilePath) && 
                    DateTime.Now - File.GetLastWriteTime(cachedFilePath) < CacheExpiry)
                {
                    try
                    {
                        await using var fileStream = File.OpenRead(cachedFilePath);
                        var bitmap = new Bitmap(fileStream);
                        cachedImage.Bitmap = bitmap;
                        cachedImage.IsLoaded = true;
                        _imageCache[imageUrl] = cachedImage;
                        return cachedImage;
                    }
                    catch (Exception ex)
                    {
                        DBg.d(LogLevel.Warning, $"Failed to load cached image from disk {imageUrl}: {ex.Message}");
                        // Continue to download fresh copy
                    }
                }

                // Download from internet
                var response = await _httpClient.GetAsync(imageUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    
                    // Save to disk cache
                    try
                    {
                        await File.WriteAllBytesAsync(cachedFilePath, imageBytes);
                    }
                    catch (Exception ex)
                    {
                        DBg.d(LogLevel.Warning, $"Failed to cache image to disk {imageUrl}: {ex.Message}");
                    }

                    // Create bitmap
                    using var stream = new MemoryStream(imageBytes);
                    var bitmap = new Bitmap(stream);
                    
                    cachedImage.Bitmap = bitmap;
                    cachedImage.IsLoaded = true;
                }
                else
                {
                    cachedImage.ErrorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                    cachedImage.IsLoaded = true;
                }
            }
            catch (HttpRequestException)
            {
                cachedImage.ErrorMessage = "Network error";
                cachedImage.IsLoaded = true;
            }
            catch (TaskCanceledException)
            {
                cachedImage.ErrorMessage = "Timeout";
                cachedImage.IsLoaded = true;
            }
            catch (Exception ex)
            {
                cachedImage.ErrorMessage = $"Error: {ex.Message}";
                cachedImage.IsLoaded = true;
            }

            _imageCache[imageUrl] = cachedImage;
            return cachedImage;
        }

        private bool IsValidImageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                var uri = new Uri(url);
                return uri.Scheme == "http" || uri.Scheme == "https";
            }
            catch
            {
                return false;
            }
        }

        private string GetCacheFilePath(string imageUrl)
        {
            // Create a safe filename from the URL
            var fileName = $"{imageUrl.GetHashCode():X8}_{Path.GetFileName(new Uri(imageUrl).AbsolutePath)}";
            // Remove invalid filename characters
            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_cacheDirectory, fileName);
        }

        private void CleanupCachedFile(string imageUrl)
        {
            try
            {
                var cachedFilePath = GetCacheFilePath(imageUrl);
                if (File.Exists(cachedFilePath))
                {
                    File.Delete(cachedFilePath);
                }
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Warning, $"Failed to cleanup cached file for {imageUrl}: {ex.Message}");
            }
        }

        public void ClearCache()
        {
            _imageCache.Clear();
            
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    Directory.Delete(_cacheDirectory, true);
                    Directory.CreateDirectory(_cacheDirectory);
                }
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Warning, $"Failed to clear disk cache: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
