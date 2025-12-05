using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

[Table("ChatUsers")]
public class ChatUser
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    
    [Required]
    [MaxLength(256)]
    
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    [RegularExpression(@"^[a-zA-Z0-9\s\p{L}]+$", 
        ErrorMessage = "Tên hiển thị chỉ được chứa chữ cái, số và khoảng trắng")]
    public string DisplayName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(256)]
    public string PinCodeHash { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    public bool IsActive { get; set; } = true;

    // Navigation property
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}