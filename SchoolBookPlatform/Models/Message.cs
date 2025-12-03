using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.Models;

public class Message
{
    public long Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public byte MessageType { get; set; } //0 =text, 1=image, 2=video, 3=file
    [Required]  
    public string CipherText { get; set; } = string.Empty;
    public string? PinExchange { get; set; }
    public long? ReplyToId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    
    //nav
    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public Message? ReplyTo { get; set; }
    public ICollection<Message> Replies { get; set; } = new List<Message>();
    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();

    // Dùng cho thông báo (LastMessageId)
    public ICollection<MessageNotification> NotificationsAsLastMessage { get; set; } = new List<MessageNotification>();
}