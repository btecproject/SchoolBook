using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

[Table("ChatUsers")]
public class ChatUser
{
    /// <summary>
    /// Primary Key, Foreign Key đến Users.Id
    /// </summary>
    [Key]
    public Guid UserId { get; set; }

    /// <summary>
    /// Username giống với Users.Username
    /// Để search dễ dàng mà không cần join
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Tên hiển thị trong chat
    /// User có thể chọn tên khác với username
    /// Dùng để hiển thị trong danh sách chat
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash của PIN code
    /// Không bao giờ lưu PIN gốc, chỉ lưu hash để verify
    /// Hash được tạo ở client: CryptoJS.SHA256(pin).toString()
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string PinCodeHash { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm đăng ký chat
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    /// <summary>
    /// Lần cuối cập nhật thông tin
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}