namespace SchoolBookPlatform.Models;

public class ConversationMember
{
    public Guid ConversationId { get; set; }
    public Guid ChatUserId { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    
    /// 0 = Member, 1 = Admin (trưởng nhóm)
    public byte Role { get; set; } = 0;

    // Navigation
    public Conversation Conversation { get; set; } = null!;
    public ChatUser ChatUser { get; set; } = null!;
}