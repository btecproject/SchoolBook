using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

/// <summary>
/// Model đại diện cho vote (upvote/downvote) của bài đăng
/// Composite key: PostId + UserId (mỗi user chỉ vote 1 lần cho 1 post)
/// </summary>
public class PostVote
{
    /// <summary>
    /// ID của bài đăng được vote
    /// </summary>
    [Key]
    [Column(Order = 0)]
    public Guid PostId { get; set; }

    /// <summary>
    /// ID của người dùng vote
    /// </summary>
    [Key]
    [Column(Order = 1)]
    public Guid UserId { get; set; }

    /// <summary>
    /// Loại vote: true = upvote, false = downvote
    /// </summary>
    [Required]
    public bool VoteType { get; set; }

    /// <summary>
    /// Thời gian vote
    /// </summary>
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    /// <summary>
    /// Bài đăng được vote
    /// </summary>
    [ForeignKey("PostId")]
    public virtual Post Post { get; set; } = null!;

    /// <summary>
    /// Người dùng vote
    /// </summary>
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}




