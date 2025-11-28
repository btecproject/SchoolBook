using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

[Table("UserRsaKeys")]
public class UserRsaKey
{
    /// <summary>
    /// Primary Key, Foreign Key đến Users.Id
    /// </summary>
    [Key]
    public Guid UserId { get; set; }

    /// <summary>
    /// Public key ở dạng PEM format
    /// Ví dụ: "-----BEGIN PUBLIC KEY-----\nMIIBIjA..."
    /// Public key được share với người khác để mã hóa PIN
    /// </summary>
    [Required]
    [Column(TypeName = "NVARCHAR(MAX)")]
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Private key đã được mã hóa AES bằng PIN của user
    /// Mã hóa ở client: CryptoJS.AES.encrypt(privateKey, pin).toString()
    /// Server không thể giải mã vì không có PIN
    /// User cần nhập PIN để giải mã private key khi cần dùng
    /// </summary>
    [Required]
    [Column(TypeName = "NVARCHAR(MAX)")]
    public string PrivateKeyEncrypted { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm tạo cặp key
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thời điểm key hết hạn
    /// Mặc định: CreatedAt + 30 ngày
    /// Sau khi hết hạn, user phải tạo cặp key mới
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Key có đang active không
    /// Chỉ có 1 key active tại 1 thời điểm
    /// Khi tạo key mới, key cũ sẽ bị set IsActive = false
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation property
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}