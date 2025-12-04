namespace SchoolBookPlatform.DTOs;

public class MessageDto
{
    public long MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string CipherText { get; set; } = string.Empty;
    public string? PinExchange { get; set; }
    public byte MessageType { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsMine { get; set; }
    public MessageAttachmentDto? Attachment { get; set; }
}
