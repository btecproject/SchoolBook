namespace SchoolBookPlatform.Models
{
    public class ChatMessage
    {
        public string UserId { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        
        public int? AttachmentId { get; set; }
        public string? AttachmentType { get; set; }
        public string? AttachmentName { get; set; }
        public long? AttachmentSize { get; set; }
    }
}