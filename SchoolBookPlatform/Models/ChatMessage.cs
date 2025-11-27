namespace SchoolBookPlatform.Models
{
    public class ChatMessage
    {
        public string UserId { get; set; }
        
        // ENCRYPTED: Content đã được mã hóa trên client
        public string Content { get; set; }
        
        public DateTime Timestamp { get; set; }
        
        // Encryption metadata
        public string? EncryptionIV { get; set; }        
        public string? EncryptedKey { get; set; }      
        public bool IsEncrypted { get; set; } = false;     
        

        public int? AttachmentId { get; set; }
        public string? AttachmentType { get; set; }
        public string? AttachmentName { get; set; }
        public long? AttachmentSize { get; set; }
    }
}