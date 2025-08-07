using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
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
    
    [ObservableProperty]
    private string textSearchQuery = string.Empty;
    
    [ObservableProperty]
    private string tagsSearchQuery = string.Empty;
    
    [ObservableProperty]
    private bool settingsPanelVisible = false;
    
    [ObservableProperty]
    private bool editPanelVisible = false;
    
    [ObservableProperty]
    private bool isAddingNewItem = false;
    
    [ObservableProperty]
    private GeListItem? currentEditItem = null;
    
    [ObservableProperty]
    private string editItemName = string.Empty;
    
    [ObservableProperty]
    private string editItemComment = string.Empty;
    
    [ObservableProperty]
    private string editItemTags = string.Empty;
    
    [ObservableProperty]
    private string editValidationError = string.Empty;
    
    [ObservableProperty]
    private bool movePanelVisible = false;
    
    [ObservableProperty]
    private GeListItem? currentMoveItem = null;
    
    [ObservableProperty]
    private ObservableCollection<GeList> availableMoveLists = new();
    
    // Notification properties
    [ObservableProperty]
    private bool notificationVisible = false;
    
    [ObservableProperty]
    private string notificationMessage = string.Empty;
    
    [ObservableProperty]
    private string notificationColor = "LimeGreen";
    
    // Window title with memory usage
    [ObservableProperty]
    private string windowTitle = "GeFeSLE";
    
    // Memory monitoring timer
    private Timer? _memoryTimer;
    
    // Confirmation dialog properties
    [ObservableProperty]
    private bool confirmationDialogVisible = false;
    
    [ObservableProperty]
    private string confirmationMessage = string.Empty;
    
    [ObservableProperty]
    private GeListItem? itemToDelete = null;
    
    private TaskCompletionSource<bool>? _confirmationTaskSource;
    
    // Store all items (unfiltered) for search functionality
    private List<GeListItem> _allItems = new List<GeListItem>();
    
    // Track whether filters were previously active
    private bool _previouslyFiltered = false;
    
    // Event for scroll position preservation
    public event Action<GeListItem>? ItemExpansionChanged;

    public string Greeting { get; } = "Welcome to Avalonia!";
    public SettingsWindowViewModel SettingsViewModel { get; }
    private readonly SessionHeartbeatService _heartbeatService;

    public MainWindowViewModel(SettingsService settingsService, GeFeSLEApiClient apiClient, HotkeyService hotkeyService, ImageCacheService imageCacheService, SessionHeartbeatService heartbeatService)
    {
        _apiClient = apiClient;
        _settingsService = settingsService;
        _imageCacheService = imageCacheService;
        _heartbeatService = heartbeatService;
        SettingsViewModel = new SettingsWindowViewModel(settingsService, apiClient, hotkeyService);
        
        // Subscribe to notification requests from settings
        SettingsViewModel.NotificationRequested += (message, color) => ShowNotification(message, color);
        
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
                    
                    // Start heartbeat monitoring when logged in
                    _heartbeatService.StartHeartbeat();
                }
                else
                {
                    DropdownPlaceholder = "Log in first (see Settings)";
                    AvailableLists.Clear();
                    SelectedList = null;
                    HasListMetadata = false;
                    
                    // Stop heartbeat monitoring when logged out
                    _heartbeatService.StopHeartbeat();
                }
            }
        };
        
        // Subscribe to heartbeat service events
        _heartbeatService.SessionStatusChanged += OnSessionStatusChanged;
        
        // Try auto-login if credentials are saved
        _ = TryAutoLoginAsync();
        
        // Initialize memory monitoring timer
        InitializeMemoryTimer();
    }

    private void InitializeMemoryTimer()
    {
        _memoryTimer = new Timer(10000); // Update every 10 seconds
        _memoryTimer.Elapsed += (sender, e) => UpdateMemoryUsage();
        _memoryTimer.AutoReset = true;
        _memoryTimer.Start();
        
        // Update immediately
        UpdateMemoryUsage();
    }

    private void UpdateMemoryUsage()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var memoryUsage = DBg.GetMemoryUsage();
                WindowTitle = $"GeFeSLE - RAM: {memoryUsage}";
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Warning, $"Failed to update memory usage in title: {ex.Message}");
                WindowTitle = "GeFeSLE";
            }
        });
    }

    private async void ShowNotification(string message, string color = "LimeGreen", int durationMs = 3000)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            NotificationMessage = message;
            NotificationColor = color;
            NotificationVisible = true;
        });

        // Auto-hide after specified duration
        _ = Task.Delay(durationMs).ContinueWith(_ =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                NotificationVisible = false;
            });
        });
    }

    private async Task TryAutoLoginAsync()
    {
        // First, try to validate existing session if we have saved cookies/session
        if (!string.IsNullOrEmpty(_settingsService.Settings.SessionCookies) &&
            !string.IsNullOrEmpty(_settingsService.Settings.ServerUrl))
        {
            ShowNotification("Validating saved session...", "DodgerBlue");
            
            var sessionValid = await SettingsViewModel.ValidateExistingSessionAsync();
            if (sessionValid)
            {
                // Session is still valid, we're logged in
                DropdownPlaceholder = "Select a list...";
                ShowNotification("Loading lists...", "DodgerBlue");
                await LoadListsAsync();
                
                // Start heartbeat monitoring since we're logged in
                _heartbeatService.StartHeartbeat();
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
            ShowNotification("Auto-logging in with saved credentials...", "DodgerBlue");
            await SettingsViewModel.AttemptAutoLoginAsync();
            
            // After auto-login attempt, check if we're now logged in and load lists
            if (SettingsViewModel.IsLoggedIn)
            {
                DropdownPlaceholder = "Select a list...";
                ShowNotification("Loading lists...", "DodgerBlue");
                await LoadListsAsync();
                
                // Start heartbeat monitoring since auto-login succeeded
                _heartbeatService.StartHeartbeat();
            }
            else
            {
                ShowNotification("Auto-login failed. Please check your credentials in Settings.", "Orange");
                DropdownPlaceholder = "Log in first (see Settings)";
            }
        }
        else
        {
            DropdownPlaceholder = "Log in first (see Settings)";
        }
    }

    private void OnSessionStatusChanged(object? sender, SessionStatusChangedEventArgs e)
    {
        // This runs on a background thread, so we need to dispatch to the UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (e.IsAuthenticated)
            {
                // Session is healthy or re-authenticated successfully
                if (e.StatusMessage == "Automatically re-authenticated")
                {
                    StatusMessage = "Session restored automatically";
                    // Reload lists since we might have been disconnected
                    _ = LoadListsAsync();
                }
                // For regular heartbeat success, don't update UI since user might be working
                
                // Ensure SettingsViewModel knows we're logged in
                if (!SettingsViewModel.IsLoggedIn)
                {
                    SettingsViewModel.IsLoggedIn = true;
                }
            }
            else
            {
                // Session lost and couldn't be restored
                StatusMessage = e.StatusMessage ?? "Session expired";
                DropdownPlaceholder = "Log in first (see Settings)";
                AvailableLists.Clear();
                SelectedList = null;
                HasListMetadata = false;
                
                // Update SettingsViewModel
                SettingsViewModel.IsLoggedIn = false;
            }
        });
    }

    [RelayCommand]
    private async Task LoadLists()
    {
        await LoadListsAsync();
    }

    private async Task LoadListItemsAsync(int listId)
    {
        IsLoadingItems = true;
        // Clear previous status when starting to load new list
        StatusMessage = "";
        _previouslyFiltered = false; // Reset filter state for new list
        
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
                ShowNotification("Extracting images...", "DodgerBlue", 2000);
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
                    ShowNotification($"Loading 0 / {allImageUrls.Count} images", "DodgerBlue", 5000);
                    
                    var successfulLoads = await _imageCacheService.PreloadImagesAsync(allImageUrls, (completed, successful, total) =>
                    {
                        // Update status on UI thread with temporary notification
                        Dispatcher.UIThread.Post(() =>
                        {
                            ShowNotification($"Loading {successful} / {total} images", "DodgerBlue", 1000);
                        });
                    });
                    
                    // Final status after loading complete
                    ShowNotification($"Loaded {successfulLoads} / {allImageUrls.Count} images", "LimeGreen", 3000);
                    
                    DBg.d(LogLevel.Debug, "All images preloaded successfully");
                }
                
                // Phase 3: Store all items with original positions and apply search filtering
                
                // Store raw values for editing before any processing
                foreach (var item in visibleItems)
                {
                    if (item != null)
                    {
                        // Store original values from server for editing
                        item.RawName = item.Name;
                        item.RawComment = item.Comment;
                    }
                }
                
                // Store all items with their original positions for search functionality
                _allItems = visibleItems.ToList();
                int originalPosition = 1;
                foreach (var item in _allItems)
                {
                    item.DisplayPosition = originalPosition++;
                }
                
                // Restore search settings for this list
                if (SelectedList != null)
                {
                    var searchSettings = _settingsService.GetListSearchSettings(SelectedList.Id);
                    TextSearchQuery = searchSettings.TextSearchQuery;
                    TagsSearchQuery = searchSettings.TagsSearchQuery;
                }
                
                // Apply search filtering and display
                ApplySearchFilters();
                
                DBg.d(LogLevel.Debug, $"Loaded {ListItems.Count} valid items with all images preloaded");
                StatusMessage = "";
            }
            else
            {
                _allItems.Clear();
                ListItems.Clear();
                StatusMessage = "";
                DBg.d(LogLevel.Debug, "No items found or empty response");
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
                // Note: Search terms are now persisted per list, not cleared
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
    private void ToggleSettingsPanel()
    {
        SettingsPanelVisible = !SettingsPanelVisible;
    }
    
    [RelayCommand]
    private void EditItem(GeListItem item)
    {
        if (item != null)
        {
            IsAddingNewItem = false;
            CurrentEditItem = item;
            EditItemName = item.RawName ?? item.Name ?? string.Empty;
            EditItemComment = item.RawComment ?? item.Comment ?? string.Empty;
            EditItemTags = ConvertTagsToString(item.Tags ?? new List<string>());
            EditPanelVisible = true;
        }
    }
    
    [RelayCommand]
    private async Task DeleteItem(GeListItem item)
    {
        if (item == null || SelectedList == null)
            return;
            
        try
        {
            // Check if confirmation is enabled and show dialog if needed
            if (_settingsService.Settings.ConfirmItemDeletion)
            {
                bool confirmed = await ShowDeleteConfirmationDialog(item);
                if (!confirmed)
                    return; // User cancelled deletion
            }
            
            // Delete the item from the server
            bool success = await _apiClient.DeleteItemAsync(SelectedList.Id, item.Id);
            
            if (success)
            {
                DBg.d(LogLevel.Debug, $"Successfully deleted item {item.Id} from list {SelectedList.Id}");
                ShowNotification($"Item '{item.Name}' deleted successfully", "LimeGreen");
                
                // Refresh the items list to remove the deleted item
                await LoadListItemsAsync(SelectedList.Id);
            }
            else
            {
                DBg.d(LogLevel.Error, $"Failed to delete item {item.Id} from list {SelectedList.Id} - server returned error");
                ShowNotification("Failed to delete item", "Orange");
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Failed to delete item: {ex.Message}");
            ShowNotification($"Error deleting item: {ex.Message}", "Orange");
        }
    }
    
    [RelayCommand]
    private void MoveItem(GeListItem item)
    {
        if (item == null || SelectedList == null)
            return;
            
        CurrentMoveItem = item;
        
        // Populate available lists excluding the current list
        AvailableMoveLists.Clear();
        foreach (var list in AvailableLists)
        {
            if (list.Id != SelectedList.Id)
            {
                AvailableMoveLists.Add(list);
            }
        }
        
        MovePanelVisible = true;
    }
    
    [RelayCommand]
    private async Task ConfirmMoveItem(GeList targetList)
    {
        if (CurrentMoveItem == null || SelectedList == null || targetList == null)
            return;
            
        try
        {
            var moveDto = new MoveItemDto
            {
                itemid = CurrentMoveItem.Id,
                listid = targetList.Id
            };
            
            bool success = await _apiClient.MoveItemAsync(moveDto);
            
            if (success)
            {
                DBg.d(LogLevel.Debug, $"Successfully moved item {CurrentMoveItem.Id} from list {SelectedList.Id} to list {targetList.Id}");
                
                // Close the move panel
                CancelMoveItem();
                
                // Refresh the items list to remove the moved item
                await LoadListItemsAsync(SelectedList.Id);
            }
            else
            {
                DBg.d(LogLevel.Error, $"Failed to move item {CurrentMoveItem.Id} to list {targetList.Id} - server returned error");
                // TODO: Show error message to user in the UI
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Failed to move item: {ex.Message}");
            // TODO: Show error message to user in the UI
        }
    }
    
    [RelayCommand]
    private void CancelMoveItem()
    {
        MovePanelVisible = false;
        CurrentMoveItem = null;
        AvailableMoveLists.Clear();
    }
    
    [RelayCommand]
    private void AddNewItem()
    {
        if (SelectedList != null)
        {
            IsAddingNewItem = true;
            CurrentEditItem = null;
            EditItemName = string.Empty;
            EditItemComment = string.Empty;
            EditItemTags = string.Empty;
            EditPanelVisible = true;
        }
    }
    
    [RelayCommand]
    private void ToggleEditPanel()
    {
        EditPanelVisible = !EditPanelVisible;
        if (!EditPanelVisible)
        {
            IsAddingNewItem = false;
            CurrentEditItem = null;
            EditItemName = string.Empty;
            EditItemComment = string.Empty;
            EditItemTags = string.Empty;
            EditValidationError = string.Empty;
        }
    }
    
    [RelayCommand]
    private async Task SaveEditItem()
    {
        try
        {
            // Clear any previous validation errors
            EditValidationError = string.Empty;
            
            // Validate tags first
            var tagsValidationError = ValidateTagsString(EditItemTags);
            if (!string.IsNullOrEmpty(tagsValidationError))
            {
                EditValidationError = tagsValidationError;
                DBg.d(LogLevel.Error, $"Tags validation failed: {tagsValidationError}");
                return;
            }
            
            // Parse tags
            var parsedTags = ParseTagsFromString(EditItemTags);
            
            if (IsAddingNewItem)
            {
                // Adding a new item
                if (SelectedList == null)
                {
                    DBg.d(LogLevel.Error, "Cannot add item: no list selected");
                    return;
                }
                
                // Create new item object
                var newItem = new GeListItem
                {
                    Name = EditItemName,
                    Comment = EditItemComment,
                    RawName = EditItemName,
                    RawComment = EditItemComment,
                    ListId = SelectedList.Id,
                    CreatedDate = DateTime.Now,
                    ModifiedDate = DateTime.Now,
                    IsComplete = false,
                    Tags = parsedTags
                };
                
                // Save to server using POST endpoint
                bool success = await _apiClient.AddItemAsync(SelectedList.Id, newItem);
                
                if (success)
                {
                    DBg.d(LogLevel.Debug, $"Successfully added new item to list {SelectedList.Id}");
                    
                    // Close the edit panel
                    ToggleEditPanel();
                    
                    // Refresh the items list to show the new item
                    await LoadListItemsAsync(SelectedList.Id);
                }
                else
                {
                    DBg.d(LogLevel.Error, $"Failed to add new item to list {SelectedList.Id} - server returned error");
                    // TODO: Show error message to user in the UI
                }
            }
            else
            {
                // Editing existing item
                if (CurrentEditItem == null)
                    return;
                    
                // Update the item with new values
                CurrentEditItem.RawName = EditItemName;
                CurrentEditItem.RawComment = EditItemComment;
                CurrentEditItem.Name = EditItemName; // For display
                CurrentEditItem.Comment = EditItemComment; // For display (will be processed by RichHtmlControl)
                CurrentEditItem.Tags = parsedTags; // Update tags
                CurrentEditItem.ModifiedDate = DateTime.Now; // Update modification date
                
                // Save to server using PUT endpoint
                bool success = await _apiClient.UpdateItemAsync(CurrentEditItem);
                
                if (success)
                {
                    DBg.d(LogLevel.Debug, $"Successfully saved changes to item {CurrentEditItem.Id}");
                    
                    // Close the edit panel
                    ToggleEditPanel();
                    
                    // Optionally refresh the display to ensure we have the latest data
                    // Note: This will preserve the current search/filter state
                    ApplySearchFilters();
                }
                else
                {
                    DBg.d(LogLevel.Error, $"Failed to save changes to item {CurrentEditItem.Id} - server returned error");
                    // TODO: Show error message to user in the UI
                }
            }
        }
        catch (Exception ex)
        {
            DBg.d(LogLevel.Error, $"Failed to save item: {ex.Message}");
            // TODO: Show error message to user in the UI
        }
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

    [RelayCommand]
    private void OpenListOnServer(GeList list)
    {
        if (list != null && !string.IsNullOrEmpty(_settingsService.Settings.ServerUrl))
        {
            try
            {
                string serverUrl = _settingsService.Settings.ServerUrl.TrimEnd('/');
                string listUrl = $"{serverUrl}/lists/{list.Id}";
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = listUrl,
                    UseShellExecute = true
                });
                DBg.d(LogLevel.Debug, $"Opened list on server: {listUrl}");
                ShowNotification($"Opened list '{list.Name}' in browser", "DodgerBlue");
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Error, $"Failed to open list URL: {ex.Message}");
                ShowNotification("Failed to open list in browser", "Orange");
            }
        }
        else
        {
            ShowNotification("Server URL not configured", "Orange");
        }
    }
    
    // Helper methods for tag parsing and validation
    private List<string> ParseTagsFromString(string tagsText)
    {
        if (string.IsNullOrWhiteSpace(tagsText))
            return new List<string>();
            
        var tags = new List<string>();
        var currentTag = "";
        bool inQuotes = false;
        
        for (int i = 0; i < tagsText.Length; i++)
        {
            char c = tagsText[i];
            
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                // End of a tag
                if (!string.IsNullOrWhiteSpace(currentTag))
                {
                    tags.Add(currentTag.Trim());
                    currentTag = "";
                }
            }
            else
            {
                currentTag += c;
            }
        }
        
        // Add the last tag if there is one
        if (!string.IsNullOrWhiteSpace(currentTag))
        {
            tags.Add(currentTag.Trim());
        }
        
        return tags;
    }
    
    private string ValidateTagsString(string tagsText)
    {
        if (string.IsNullOrWhiteSpace(tagsText))
            return ""; // Empty is valid
            
        // Check for unmatched quotes
        int quoteCount = 0;
        foreach (char c in tagsText)
        {
            if (c == '"')
                quoteCount++;
        }
        
        if (quoteCount % 2 != 0)
        {
            return "Error: Unmatched quote marks in tags. Each multi-word tag must be enclosed in paired quotes.";
        }
        
        return ""; // Valid
    }
    
    private string ConvertTagsToString(List<string> tags)
    {
        if (tags == null || tags.Count == 0)
            return "";
            
        var result = new List<string>();
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
                continue;
                
            // If tag contains spaces, wrap in quotes
            if (tag.Contains(' '))
            {
                result.Add($"\"{tag}\"");
            }
            else
            {
                result.Add(tag);
            }
        }
        
        return string.Join(" ", result);
    }
    
    [RelayCommand]
    private void ClearSearch()
    {
        TextSearchQuery = string.Empty;
        TagsSearchQuery = string.Empty;
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

    partial void OnTextSearchQueryChanged(string value)
    {
        // Save search settings for current list
        if (SelectedList != null)
        {
            _settingsService.UpdateListSearchSettings(SelectedList.Id, value, TagsSearchQuery);
        }
        ApplySearchFilters();
    }

    partial void OnTagsSearchQueryChanged(string value)
    {
        // Save search settings for current list
        if (SelectedList != null)
        {
            _settingsService.UpdateListSearchSettings(SelectedList.Id, TextSearchQuery, value);
        }
        ApplySearchFilters();
    }

    private void ApplySearchFilters()
    {
        if (_allItems == null || _allItems.Count == 0)
        {
            ListItems.Clear();
            return;
        }

        var filteredItems = _allItems.AsEnumerable();

        // Apply text search filter
        if (!string.IsNullOrWhiteSpace(TextSearchQuery))
        {
            var textTerms = ParseSearchTerms(TextSearchQuery);
            if (textTerms.Count > 0)
            {
                filteredItems = filteredItems.Where(item =>
                {
                    var searchableText = $"{item.Name} {item.Comment}".ToLowerInvariant();
                    // OR condition: item matches if it contains ANY of the search terms
                    return textTerms.Any(term => searchableText.Contains(term.ToLowerInvariant()));
                });
            }
        }

        // Apply tags search filter  
        if (!string.IsNullOrWhiteSpace(TagsSearchQuery))
        {
            var tagTerms = ParseSearchTerms(TagsSearchQuery);
            if (tagTerms.Count > 0)
            {
                filteredItems = filteredItems.Where(item =>
                {
                    if (item.Tags == null || item.Tags.Count == 0)
                        return false;
                    
                    var itemTags = item.Tags.Select(tag => tag.ToLowerInvariant()).ToList();
                    // OR condition: item matches if any of its tags contains any of the search terms (partial match)
                    return tagTerms.Any(term => 
                        itemTags.Any(tag => tag.Contains(term.ToLowerInvariant()))
                    );
                });
            }
        }

        // Update the ListItems collection (preserve original DisplayPosition)
        ListItems.Clear();
        foreach (var item in filteredItems)
        {
            try
            {
                // Keep original DisplayPosition - don't reassign
                ListItems.Add(item);
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Warning, $"Error adding filtered item to list: {ex.Message}");
            }
        }
        
        // Update item count status
        UpdateItemCountStatus();
    }

    private void UpdateItemCountStatus()
    {
        if (_allItems == null || _allItems.Count == 0)
        {
            _previouslyFiltered = false;
            return;
        }

        var totalItems = _allItems.Count;
        var filteredItems = ListItems.Count;
        
        // Check if any filters are active
        var hasTextFilter = !string.IsNullOrWhiteSpace(TextSearchQuery);
        var hasTagsFilter = !string.IsNullOrWhiteSpace(TagsSearchQuery);
        var currentlyFiltered = hasTextFilter || hasTagsFilter;
        
        if (currentlyFiltered)
        {
            // Show notification when filters are active
            ShowNotification($"Showing {filteredItems} of {totalItems} items", "LimeGreen", 4000);
        }
        else if (_previouslyFiltered)
        {
            // Show notification when filters were just cleared (showing all items)
            ShowNotification($"Showing all {totalItems} items", "LimeGreen", 3000);
        }
        
        // Update the previous filter state for next time
        _previouslyFiltered = currentlyFiltered;
    }

    private List<string> ParseSearchTerms(string searchQuery)
    {
        var terms = new List<string>();
        if (string.IsNullOrWhiteSpace(searchQuery))
            return terms;

        var inQuotes = false;
        var currentTerm = new System.Text.StringBuilder();
        
        for (int i = 0; i < searchQuery.Length; i++)
        {
            var c = searchQuery[i];
            
            if (c == '"')
            {
                if (inQuotes)
                {
                    // End of quoted term
                    if (currentTerm.Length > 0)
                    {
                        terms.Add(currentTerm.ToString());
                        currentTerm.Clear();
                    }
                    inQuotes = false;
                }
                else
                {
                    // Start of quoted term - first save any current non-quoted term
                    if (currentTerm.Length > 0)
                    {
                        terms.Add(currentTerm.ToString().Trim());
                        currentTerm.Clear();
                    }
                    inQuotes = true;
                }
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                // Space outside quotes - end current term
                if (currentTerm.Length > 0)
                {
                    terms.Add(currentTerm.ToString().Trim());
                    currentTerm.Clear();
                }
            }
            else
            {
                // Regular character or space inside quotes
                currentTerm.Append(c);
            }
        }
        
        // Add final term if any
        if (currentTerm.Length > 0)
        {
            terms.Add(currentTerm.ToString().Trim());
        }
        
        // Remove empty terms
        return terms.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
    }

    /// <summary>
    /// Cleanup resources when the ViewModel is disposed
    /// </summary>
    public void Dispose()
    {
        _memoryTimer?.Stop();
        _memoryTimer?.Dispose();
    }

    /// <summary>
    /// Shows a confirmation dialog for item deletion
    /// </summary>
    private async Task<bool> ShowDeleteConfirmationDialog(GeListItem item)
    {
        ConfirmationMessage = $"Are you sure you want to delete the item '{item.Name}'?";
        ItemToDelete = item;
        ConfirmationDialogVisible = true;
        
        _confirmationTaskSource = new TaskCompletionSource<bool>();
        return await _confirmationTaskSource.Task;
    }

    [RelayCommand]
    private void ConfirmDelete()
    {
        ConfirmationDialogVisible = false;
        _confirmationTaskSource?.SetResult(true);
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ConfirmationDialogVisible = false;
        _confirmationTaskSource?.SetResult(false);
    }
}
