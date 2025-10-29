using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

public class OtpCode
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public Guid UserId { get; set; }

    [Required] [MaxLength(10)] public string Code { get; set; }

    public string Type { get; set; } // 'SMS' or 'Email'
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))] public User User { get; set; }
}