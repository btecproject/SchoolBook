using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

public class UserToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public User User { get; set; }

    public Guid TokenId { get; set; } = Guid.NewGuid();
    public string DeviceInfo { get; set; }
    public string IPAddress { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiredAt { get; set; }
}