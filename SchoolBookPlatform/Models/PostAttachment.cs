using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

/// <summary>
/// Model đại diện cho file đính kèm của bài đăng
/// </summary>
public class PostAttachment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID của bài đăng mà file đính kèm thuộc về
    /// </summary>
    [Required]
    public Guid PostId { get; set; }

    /// <summary>
    /// Tên file (tối đa 255 ký tự)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn lưu file (tối đa 500 ký tự)
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Kích thước file (bytes)
    /// </summary>
    [Required]
    public int FileSize { get; set; }

    /// <summary>
    /// Thời gian upload file
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>
    /// Bài đăng mà file đính kèm thuộc về
    /// </summary>
    [ForeignKey("PostId")]
    public virtual Post Post { get; set; } = null!;
}




