using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

/// <summary>
/// Model đại diện cho báo cáo về bài đăng
/// </summary>
public class PostReport
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID của bài đăng bị báo cáo
    /// </summary>
    [Required]
    public Guid PostId { get; set; }

    /// <summary>
    /// ID của người dùng tạo báo cáo
    /// </summary>
    [Required]
    public Guid ReportedBy { get; set; }

    /// <summary>
    /// Lý do báo cáo (tối đa 500 ký tự)
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái báo cáo: 'Pending', 'Approved', 'Rejected'
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// ID của admin/moderator đã review báo cáo (null nếu chưa review)
    /// </summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>
    /// Thời gian review báo cáo (null nếu chưa review)
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Thời gian tạo báo cáo
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>
    /// Bài đăng bị báo cáo
    /// </summary>
    [ForeignKey("PostId")]
    public virtual Post Post { get; set; } = null!;

    /// <summary>
    /// Người dùng tạo báo cáo
    /// </summary>
    [ForeignKey("ReportedBy")]
    public virtual User Reporter { get; set; } = null!;

    /// <summary>
    /// Admin/Moderator đã review báo cáo
    /// </summary>
    [ForeignKey("ReviewedBy")]
    public virtual User? Reviewer { get; set; }
}




