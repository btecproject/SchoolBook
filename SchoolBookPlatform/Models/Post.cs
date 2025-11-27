using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

/// <summary>
/// Model đại diện cho bài đăng trong hệ thống
/// </summary>
public class Post
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID của người dùng tạo bài đăng
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Tiêu đề bài đăng (tối đa 300 ký tự)
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung bài đăng
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Thời gian tạo bài đăng
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thời gian cập nhật bài đăng (null nếu chưa cập nhật)
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Đánh dấu bài đăng đã bị xóa (soft delete)
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Đánh dấu bài đăng có hiển thị hay không
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Nhóm người dùng được phép xem bài đăng
    /// Giá trị: 'Student', 'Teacher', 'Admin', 'All' (null = All)
    /// </summary>
    [MaxLength(50)]
    public string? VisibleToRoles { get; set; }

    // Navigation properties
    /// <summary>
    /// Người dùng tạo bài đăng
    /// </summary>
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
    
    /// <summary>
    /// Danh sách comment của bài đăng
    /// </summary>
    public virtual ICollection<PostComment> Comments { get; set; } = new List<PostComment>();
    
    /// <summary>
    /// Danh sách vote (upvote/downvote) của bài đăng
    /// </summary>
    public virtual ICollection<PostVote> Votes { get; set; } = new List<PostVote>();
    
    /// <summary>
    /// Danh sách báo cáo về bài đăng
    /// </summary>
    public virtual ICollection<PostReport> Reports { get; set; } = new List<PostReport>();
    
    /// <summary>
    /// Danh sách file đính kèm của bài đăng
    /// </summary>
    public virtual ICollection<PostAttachment> Attachments { get; set; } = new List<PostAttachment>();
}




