namespace SchoolBookPlatform.DTOs;
public class ContactDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
    public DateTime LastSentAt { get; set; }
    public string LastMessagePreview { get; set; } = string.Empty;
    
    public Guid ConversationId { get; set; }
}
