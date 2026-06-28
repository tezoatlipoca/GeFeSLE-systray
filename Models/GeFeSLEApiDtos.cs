using System;
using System.Collections.Generic;
namespace GeFeSLE.Models;

// the DTOs here are as per GeFeSLE-server API v0.1.2 June 28 2026
// the DTOs aren't in an exportable/reuseable namespace yet where we can just
// reuse them here. 

public class GeListResponseDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string? CreatorId { get; set; }
    public string? CreatorName { get; set; }
    public string? ActivityPubId { get; set; }
    public GeListVisibility Visibility { get; set; } = GeListVisibility.Public;
    public bool IsOrdered { get; set; } = false;
    public int VisibleItemCount { get; set; }
}

public class GeListItemResponseDto
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public string? Name { get; set; }
    public string? Comment { get; set; }
    public bool IsComplete { get; set; }
    public bool Visible { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public int? RedirectToItemId { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}

public class GeListItemCreateUpdateDto
{
    public int ListId { get; set; }
    public string? Name { get; set; }
    public string? Comment { get; set; }
    public bool IsComplete { get; set; }
    public bool Visible { get; set; } = true;
    public List<string> Tags { get; set; } = new();
}

public class MoveItemRequestDto
{
    public int listid { get; set; }
}