namespace SchoolBookPlatform.Models;

public class ChatUser
{
    public Guid UserId { get; set; }
    public string Username { get; set; }
    public string DisplayName { get; set; }
    public string PinCodeHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}