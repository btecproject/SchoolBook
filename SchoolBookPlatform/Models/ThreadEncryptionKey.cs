namespace SchoolBookPlatform.Models
{
    public class ThreadEncryptionKey
    {
        public int Id { get; set; }
        
        public int ThreadId { get; set; }
        
        public Guid UserId { get; set; }
        
        public string EncryptedThreadKey { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Foreign key navigations
        public ChatThread? Thread { get; set; }
        public User? User { get; set; }
    }
}