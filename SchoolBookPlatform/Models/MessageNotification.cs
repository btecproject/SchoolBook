namespace SchoolBookPlatform.Models;

public class MessageNotification
{
    public Guid RecipientId { get; set; }
    public Guid SenderId { get; set; }

    public int UnreadCount { get; set; } = 1;

    public long? LastMessageId { get; set; }

    public DateTime LastSentAt { get; set; } = DateTime.UtcNow.AddHours(7);

    // Navigation
    public User Recipient { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public Message? LastMessage { get; set; }
}