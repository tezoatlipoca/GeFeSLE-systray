using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GeFeSLE.Models;
using GeFeSLE.Services;
using Avalonia.Threading;

namespace GeFeSLE.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GeFeSLEApiClient _apiClient;
    private readonly SettingsService _settingsService;
    private readonly ImageCacheService _imageCacheService;
    
    // Dictionary to track expansion state of items across list redraws
    private readonly Dictionary<int, bool> _itemExpansionStates = new();
    
    [ObservableProperty]
    private ObservableCollection<GeList> availableLists = new();
    
    [ObservableProperty]
    private GeList? selectedList;
    
    [ObservableProperty]
    private ObservableCollection<GeListItem> listItems = new();
    
    [ObservableProperty]
    private bool isLoadingItems = false;
    
    [ObservableProperty]
    private string statusMessage = string.Empty;
    
    [ObservableProperty]
    private string dropdownPlaceholder = "Log in first (see Settings)";
    
    [ObservableProperty]
    private bool metadataPanelExpanded = false;
    
    [ObservableProperty]
    private bool hasListMetadata = false;
    
    // Event for scroll position preservation
    public event Action<GeListItem>? ItemExpansionChanged;

    public string Greeting { get; } = "Welcome to Avalonia!";
    public SettingsWindowViewModel SettingsViewModel { get; }

    public MainWindowViewModel(SettingsService settingsService, GeFeSLEApiClient apiClient, HotkeyService hotkeyService, ImageCacheService imageCacheService)
    {
        _apiClient = apiClient;
        _settingsService = settingsService;
        _imageCacheService = imageCacheService;
        SettingsViewModel = new SettingsWindowViewModel(settingsService, apiClient, hotkeyService);
        
        // Initialize metadata panel state from settings
        MetadataPanelExpanded = _settingsService.Settings.MetadataPanelExpanded;
        
        // Load session cookies if available
        if (!string.IsNullOrEmpty(_settingsService.Settings.SessionCookies))
        {
            _apiClient.SetSessionCookies(_settingsService.Settings.SessionCookies);
        }
        
        // Subscribe to login status changes to load lists when logged in
        SettingsViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.IsLoggedIn))
            {
                if (SettingsViewModel.IsLoggedIn)
                {
                    DropdownPlaceholder = "Select a list...";
                    _ = LoadListsAsync();
                }
                else
                {
                    DropdownPlaceholder = "Log in first (see Settings)";
                    AvailableLists.Clear();
                    SelectedList = null;
                    HasListMetadata = false;
                }
            }
        };
        
        // Try auto-login if credentials are saved
        _ = TryAutoLoginAsync();
    }

    private async Task TryAutoLoginAsync()
    {
        // First, try to validate existing session if we have saved cookies/session
        if (!string.IsNullOrEmpty(_settingsService.Settings.SessionCookies) &&
            !string.IsNullOrEmpty(_settingsService.Settings.ServerUrl))
        {
            StatusMessage = "Validating saved session...";
            
            var sessionValid = await SettingsViewModel.ValidateExistingSessionAsync();
            if (sessionValid)
            {
                // Session is still valid, we're logged in
                DropdownPlaceholder = "Select a list...";
                StatusMessage = "Loading lists...";
                await LoadListsAsync();
                return;
            }
            else
            {
                // Session expired, clear saved cookies
                _settingsService.UpdateSessionCookies(null);
            }
        }
        
        // If session validation failed or no session, try auto-login with saved credentials
        if (_settingsService.Settings.RememberLogin && 
            !string.IsNullOrEmpty(_settingsService.Settings.Username) && 
            !string.IsNullOrEmpty(_settingsService.GetPassword()) &&
            !string.IsNullOrEmpty(_settingsService.Settings.ServerUrl))
        {
            StatusMessage = "Auto-logging in with saved credentials...";
            await SettingsViewModel.AttemptAutoLoginAsync();
            
            // After auto-login attempt, check if we're now logged in and load lists
            if (SettingsViewModel.IsLoggedIn)
            {
                DropdownPlaceholder = "Select a list...";
                StatusMessage = "Loading lists...";
                await LoadListsAsync();
            }
            else
            {
                StatusMessage = "Auto-login failed. Please check your credentials in Settings.";
                DropdownPlaceholder = "Log in first (see Settings)";
            }
        }
        else
        {
            DropdownPlaceholder = "Log in first (see Settings)";
            StatusMessage = "";
        }
    }

    [RelayCommand]
    private async Task LoadLists()
    {
        await LoadListsAsync();
    }

    private async Task LoadListItemsAsync(int listId)
    {
        IsLoadingItems = true;
        try
        {
            DBg.d(LogLevel.Debug, $"Loading items for list {listId}");
            
            if (!SettingsViewModel.IsLoggedIn)
            {
                DBg.d(LogLevel.Warning, "Not logged in, cannot load list items");
                return;
            }

            _apiClient.SetBaseAddress(SettingsViewModel.ServerUrl ?? "");
            var items = await _apiClient.GetListItemsAsync(listId);
            
            if (items != null && items.Count > 0)
            {
                // Only show visible items and ensure no null values
                var visibleItems = items.Where(item => item != null && item.Visible).ToList();
                
                // Phase 1: Extract all image URLs from all items before rendering any UI
                StatusMessage = "Extracting images...";
                var allImageUrls = new List<string>();
                
                foreach (var item in visibleItems)
                {
                    try
                    {
                        // Ensure no null or problematic properties that could cause UI issues
                        if (string.IsNullOrEmpty(item.Name)) 
                            item.Name = "[No Name]";
                        if (item.Comment == null) 
                            item.Comment = "";
                        if (item.Tags == null) 
                            item.Tags = new List<string>();
                        
                        // Ensure dates are valid
                        if (item.CreatedDate == default)
                            item.CreatedDate = DateTime.Now;
                        if (item.ModifiedDate == default)
                            item.ModifiedDate = DateTime.Now;
                        
                        // Clean up any null tags
                        item.Tags = item.Tags.Where(tag => !string.IsNullOrEmpty(tag)).ToList();
                        
                        // Restore expansion state if it was previously expanded
                        item.IsExpanded = _itemExpansionStates.ContainsKey(item.Id) && _itemExpansionStates[item.Id];
                        
                        // Extract image URLs from this item's content
                        var itemImageUrls = await _imageCacheService.ExtractImageUrlsFromContentAsync(item.Comment);
                        allImageUrls.AddRange(itemImageUrls);
                    }
                    catch (Exception ex)
                    {
                        DBg.d(LogLevel.Warning, $"Skipping problematic item: {ex.Message}");
                        // Skip items that cause issues
                        continue;
                    }
                }
                
                // Phase 2: Preload all images before showing any items
                if (allImageUrls.Count > 0)
                {
                    DBg.d(LogLevel.Debug, $"Found {allImageUrls.Count} images to preload");
                    StatusMessage = $"Loading images (0/{allImageUrls.Count})...";
                    
                    await _imageCacheService.PreloadImagesAsync(allImageUrls, (completed, total) =>
                    {
                        // Update status on UI thread
                        Dispatcher.UIThread.Post(() =>
                        {
                            StatusMessage = $"Loading images ({completed}/{total})...";
                        });
                    });
                    
                    DBg.d(LogLevel.Debug, "All images preloaded successfully");
                }
                
                // Phase 3: Now render all items at once with cached images
                StatusMessage = "Rendering items...";
                ListItems.Clear();
                int position = 1;
                foreach (var item in visibleItems)
                {
                    try
                    {
                        item.DisplayPosition = position++;
                        ListItems.Add(item);
                    }
                    catch (Exception ex)
                    {
                        DBg.d(LogLevel.Warning, $"Error adding item to list: {ex.Message}");
                    }
                }
                
                DBg.d(LogLevel.Debug, $"Loaded {ListItems.Count} valid items with all images preloaded");
                StatusMessage = "";
            }
            else
            {
                ListItems.Clear();
                DBg.d(LogLevel.Debug, "No items found or empty response");
                StatusMessage = "";
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Error loading list items: {ex.Message}");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoadingItems = false;
        }
    }

    private async Task LoadListsAsync()
    {
        StatusMessage = "Loading lists...";
        try
        {
            if (!SettingsViewModel.IsLoggedIn)
            {
                StatusMessage = "Please log in first to load lists.";
                DropdownPlaceholder = "Log in first (see Settings)";
                return;
            }

            _apiClient.SetBaseAddress(SettingsViewModel.ServerUrl ?? "");
            var lists = await _apiClient.GetListsAsync();
            
            AvailableLists.Clear();
            if (lists != null && lists.Count > 0)
            {
                foreach (var list in lists)
                {
                    AvailableLists.Add(list);
                }
                StatusMessage = ""; // Clear status message when lists are found
                DropdownPlaceholder = "Select a list...";
                
                // Restore previously selected list if available
                if (_settingsService.Settings.SelectedListId.HasValue)
                {
                    var savedList = lists.FirstOrDefault(l => l.Id == _settingsService.Settings.SelectedListId.Value);
                    if (savedList != null)
                    {
                        SelectedList = savedList;
                        UpdateHasListMetadata(savedList);
                    }
                }
            }
            else
            {
                StatusMessage = "No lists found.";
                DropdownPlaceholder = "No lists available";
                HasListMetadata = false;
            }
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error loading lists: {ex.Message}";
            DropdownPlaceholder = "Error loading lists";
            HasListMetadata = false;
        }
    }

    partial void OnSelectedListChanged(GeList? value)
    {
        if (value != null)
        {
            // Clear expansion states when switching to a different list
            if (selectedList?.Id != value.Id)
            {
                _itemExpansionStates.Clear();
            }
            
            // Persist the selected list ID
            _settingsService.UpdateSelectedList(value.Id);
            
            // Save session cookies for future use
            var cookies = _apiClient.GetSessionCookies();
            if (!string.IsNullOrEmpty(cookies))
            {
                _settingsService.UpdateSessionCookies(cookies);
            }
            
            // Update metadata availability indicator
            UpdateHasListMetadata(value);
            
            // Load list items
            _ = LoadListItemsAsync(value.Id);
            
            // Don't show status message for list selection
        }
        else
        {
            HasListMetadata = false;
            ListItems.Clear();
        }
    }
    
    [RelayCommand]
    private void ToggleMetadataPanel()
    {
        MetadataPanelExpanded = !MetadataPanelExpanded;
        _settingsService.UpdateMetadataPanelState(MetadataPanelExpanded);
    }
    
    [RelayCommand]
    private void ToggleItemExpansion(GeListItem item)
    {
        if (item != null)
        {
            // Simply toggle the selected item (allow multiple expansions)
            item.IsExpanded = !item.IsExpanded;
            
            // Store the expansion state for retention across list redraws
            _itemExpansionStates[item.Id] = item.IsExpanded;
            
            DBg.d(LogLevel.Debug, $"{(item.IsExpanded ? "Expanded" : "Collapsed")} item {item.Id}");
            
            // Notify the view to handle scroll position preservation
            // Use Dispatcher to ensure UI has updated before scroll adjustment
            Dispatcher.UIThread.Post(() => ItemExpansionChanged?.Invoke(item), DispatcherPriority.Loaded);
        }
    }
    
    [RelayCommand]
    private void OpenItemUri(GeListItem item)
    {
        if (item?.IsNameClickable == true && !string.IsNullOrEmpty(item.Name))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.Name,
                    UseShellExecute = true
                });
                DBg.d(LogLevel.Debug, $"Opened URL: {item.Name}");
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Error, $"Failed to open URL {item.Name}: {ex.Message}");
            }
        }
    }
    
    private void UpdateHasListMetadata(GeList? list)
    {
        if (list == null)
        {
            HasListMetadata = false;
            return;
        }
        
        // Only indicate metadata when there's meaningful comment/description content
        HasListMetadata = !string.IsNullOrWhiteSpace(list.Comment);
        
        DBg.d(LogLevel.Debug, $"HasListMetadata: {HasListMetadata} - Comment present: {!string.IsNullOrWhiteSpace(list.Comment)}");
    }
}
