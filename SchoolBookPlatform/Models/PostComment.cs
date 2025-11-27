using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

/// <summary>
/// Model đại diện cho comment của bài đăng
/// Hỗ trợ nested comments (comment trả lời comment)
/// </summary>
public class PostComment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID của bài đăng mà comment thuộc về
    /// </summary>
    [Required]
    public Guid PostId { get; set; }

    /// <summary>
    /// ID của người dùng tạo comment
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Nội dung comment
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Thời gian tạo comment
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID của comment cha (nếu là comment trả lời)
    /// null nếu là comment gốc
    /// </summary>
    public Guid? ParentCommentId { get; set; }

    // Navigation properties
    /// <summary>
    /// Bài đăng mà comment thuộc về
    /// </summary>
    [ForeignKey("PostId")]
    public virtual Post Post { get; set; } = null!;

    /// <summary>
    /// Người dùng tạo comment
    /// </summary>
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Comment cha (nếu có)
    /// </summary>
    [ForeignKey("ParentCommentId")]
    public virtual PostComment? ParentComment { get; set; }

    /// <summary>
    /// Danh sách comment trả lời (nested comments)
    /// </summary>
    public virtual ICollection<PostComment> Replies { get; set; } = new List<PostComment>();
}




