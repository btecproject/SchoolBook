namespace SchoolBookPlatform.ViewModels.Post;

/// <summary>
/// ViewModel hiển thị thông tin bài đăng
/// </summary>
public class PostViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
    public int CommentCount { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsVisible { get; set; }
    public string? VisibleToRoles { get; set; }
    
    /// <summary>
    /// True nếu user hiện tại là chủ sở hữu bài đăng
    /// </summary>
    public bool IsOwner { get; set; }
    
    /// <summary>
    /// True nếu user hiện tại có quyền xóa bài đăng này
    /// </summary>
    public bool CanDelete { get; set; }

    /// <summary>
    /// Danh sách file đính kèm của bài đăng
    /// </summary>
    public List<AttachmentViewModel> Attachments { get; set; } = new();
    
}




