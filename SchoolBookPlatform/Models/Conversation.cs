namespace SchoolBookPlatform.Models;

public class Conversation
{
    public Guid Id { get; set; }
    public byte Type { get; set; } //0: chat 1 1      // 1: chat group
    public string? Name { get; set; } //bắt buộc nếu là chat group

    public string? Avatar { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);

    public Guid CreatorId { get; set; }
    
    
    // Navigation properties
    public User? Creator { get; set; }
    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}