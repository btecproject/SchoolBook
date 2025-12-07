namespace SchoolBookPlatform.ViewModels.Admin;

/// <summary>
/// ViewModel cho danh sách bài đăng chờ duyệt
/// </summary>
public class PendingPostViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AttachmentCount { get; set; }
}

/// <summary>
/// ViewModel cho thống kê duyệt bài đăng
/// </summary>
public class PostApprovalStatisticsViewModel
{
    public int PendingCount { get; set; }
    public int TotalCount { get; set; }
}

