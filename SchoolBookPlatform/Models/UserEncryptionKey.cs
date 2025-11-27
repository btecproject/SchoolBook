namespace SchoolBookPlatform.Models
{
    public class UserEncryptionKey
    {
        public int Id { get; set; }
        
        public Guid UserId { get; set; }
    
        public string PublicKey { get; set; }
        

        public string EncryptedPrivateKey { get; set; }
        
 
        public byte[] PrivateKeySalt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
        
        // Foreign key navigation
        public User User { get; set; }
    }
}