namespace SchoolBookPlatform.DTOs;

public class ChatUserSearchResult
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}