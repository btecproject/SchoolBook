namespace SchoolBookPlatform.ViewModels.Post;

/// <summary>
/// ViewModel hiển thị thông tin comment
/// </summary>
public class CommentViewModel
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Danh sách comment trả lời (nested comments)
    /// </summary>
    public List<CommentViewModel> Replies { get; set; } = new();
}




