namespace SchoolBookPlatform.ViewModels.Post;

/// <summary>
/// ViewModel cho trang danh sách bài đăng
/// </summary>
public class PostListViewModel
{
    /// <summary>
    /// Danh sách bài đăng
    /// </summary>
    public List<PostViewModel> Posts { get; set; } = new();
    
    /// <summary>
    /// Trang hiện tại (bắt đầu từ 1)
    /// </summary>
    public int CurrentPage { get; set; }
    
    /// <summary>
    /// Số bài đăng mỗi trang
    /// </summary>
    public int PageSize { get; set; }
    
    public string ViewType { get; set; } = "all"; 
    // Property để hiển thị thông báo khi không có bài đăng
    public bool IsEmptyFollowing => ViewType == "following" && !Posts.Any();
}




