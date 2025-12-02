using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

[Table("ChatUsers")]
public class ChatUser
{
    [Key]
    public Guid UserId { get; set; }
    
    [Required]
    [MaxLength(256)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(256)]
    public string PinCodeHash { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.AddHours(7);

    // Navigation property
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}