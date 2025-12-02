namespace SchoolBookPlatform.Models
{
    public class UserEncryptionKey
    {
        public int Id { get; set; }
        
        public Guid UserId { get; set; }
    
        public string PublicKey { get; set; } = string.Empty;
        
        public string? EncryptedPrivateKey { get; set; }
        
        public byte[]? PrivateKeySalt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
        public User? User { get; set; }
    }
}