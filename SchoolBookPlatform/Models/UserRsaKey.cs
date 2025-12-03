using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

[Table("UserRsaKeys")]
public class UserRsaKey
{
    [Key]
    public Guid UserId { get; set; }
    
    [Required]
    [Column(TypeName = "NVARCHAR(MAX)")]
    public string PublicKey { get; set; } = string.Empty;
    
    [Required]
    [Column(TypeName = "NVARCHAR(MAX)")]
    public string PrivateKeyEncrypted { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);


    public DateTime ExpiresAt { get; set; }


    public bool IsActive { get; set; } = true;

    // Navigation property
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}