using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace GeFeSLE.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GeListVisibility
{
    Public,          // anyone can view the list's html page, json, rss etc. 
    Contributors,    // restricted to contributors and list owners (and list creator and SU)
    ListOwners,      // restricted to only list owners (and creator and SU)
    Private          // restricted to only creator and SU
}

public class GeListDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Comment { get; set; }
    public GeListVisibility Visibility { get; set; } = GeListVisibility.Public;
}

public class GeList
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Comment { get; set; }
    public GeListVisibility Visibility { get; set; } = GeListVisibility.Public;
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
}

public class GeListItem : INotifyPropertyChanged
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public string? Name { get; set; }
    public string? Comment { get; set; }
    public bool IsComplete { get; set; }
    public bool Visible { get; set; } = true;
    public List<string> Tags { get; set; } = new List<string>();
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    
    // UI-specific properties
    private bool _isExpanded = false;
    public bool IsExpanded 
    { 
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }
    
    // Display position in the list (1-based, not the database ID)
    public int DisplayPosition { get; set; } = 1;
    
    public bool IsNameClickable => !string.IsNullOrEmpty(Name) && 
                                   (Name.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                    Name.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class GeListWithItems
{
    public GeList? List { get; set; }
    public List<GeListItem> Items { get; set; } = new List<GeListItem>();
}
