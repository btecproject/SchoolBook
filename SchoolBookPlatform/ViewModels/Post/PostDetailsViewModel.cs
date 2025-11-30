namespace SchoolBookPlatform.ViewModels.Post;

/// <summary>
/// ViewModel cho trang chi tiết bài đăng
/// </summary>
public class PostDetailsViewModel
{
    /// <summary>
    /// Thông tin bài đăng
    /// </summary>
    public PostViewModel Post { get; set; } = null!;
    
    /// <summary>
    /// Danh sách comment (chỉ comment gốc, không bao gồm replies)
    /// </summary>
    public List<CommentViewModel> Comments { get; set; } = new();
}




