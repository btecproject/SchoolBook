using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.Models;

public class User
{
    [Key] 
    public Guid Id { get; set; }

    [RegularExpression(@"^[a-zA-Z0-9\s\p{L}]+$", 
        ErrorMessage = "Username được chứa chữ cái, số và khoảng trắng")]
    [Required] 
    [MaxLength(50)] 
    public string Username { get; set; } = string.Empty;

    [Required] 
    public string PasswordHash { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Địa chỉ Email không đúng định dạng")]
    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }
    public string? FaceId { get; set; }
    public bool FaceRegistered { get; set; } = false;
    public bool MustChangePassword { get; set; } = true;
    public int TokenVersion { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    public DateTime? UpdatedAt { get; set; }

    public bool? TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorSecret { get; set; } 
    public bool? RecoveryCodesGenerated { get; set; } = false;
    public int? RecoveryCodesLeft { get; set; } = 0;
    
    public virtual ICollection<RecoveryCode> RecoveryCodes { get; set; } = new List<RecoveryCode>();
    public ICollection<UserRole>? UserRoles { get; set; }
    public ICollection<UserToken>? Tokens { get; set; }
    public ICollection<OtpCode>? OtpCodes { get; set; }
    public FaceProfile? FaceProfile { get; set; }
    public UserProfile? UserProfile { get; set; }   
}