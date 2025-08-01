using System.Text.Json;

namespace GeFeSLE.Models;

public class UserDto 
{
    // this is the same as the Identity User ID
    public string? Id { get; set; }
    // this is the Identity Claims username from the session Claims
    public string? UserName { get; set; }
    // this is the highest Role that gets saved in the session Claims
    public string? Role { get; set; }

    public bool IsAuthenticated { get; set; } = false;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public string? ErrorMessage { get; set; }
}

public class LoginDto
{
    public string? OAuthProvider { get; set; }
    public string? Instance { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(OAuthProvider) && 
           string.IsNullOrWhiteSpace(Instance) && 
           string.IsNullOrWhiteSpace(Username) && 
           string.IsNullOrWhiteSpace(Password))
        {
            return false;
        }
        else
        {
            return true;
        }
    }
}

public class AddTagDto
{
    public int ListId { get; set; }
    public int ItemId { get; set; }
    public string? Tag { get; set; }
}

public class RemoveTagDto
{
    public int ListId { get; set; }
    public int ItemId { get; set; }
    public string? Tag { get; set; }
}

public class MoveItemDto
{
    public int SourceListId { get; set; }
    public int DestinationListId { get; set; }
    public int ItemId { get; set; }
}
