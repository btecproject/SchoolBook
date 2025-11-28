using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.Models;

public class UserRsaKey
{
    public Guid UserId { get; set; }
    public string PublicKey { get; set; }
    public string PrivateKeyEncrypted { get; set; }
    public DateTime Created { get; set; } =  DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } =  DateTime.UtcNow.AddDays(30);
    public bool IsActive { get; set; } = true;
    
    public User user { get; set; }
}