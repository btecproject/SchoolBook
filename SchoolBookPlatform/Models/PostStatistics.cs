namespace SchoolBookPlatform.Models;

/// <summary>
/// Model chứa thống kê về bài đăng (dùng cho Admin)
/// </summary>
public class PostStatistics
{
    /// <summary>
    /// Tổng số bài đăng
    /// </summary>
    public int TotalPosts { get; set; }

    /// <summary>
    /// Số bài đăng đã bị xóa (soft delete)
    /// </summary>
    public int DeletedPosts { get; set; }

    /// <summary>
    /// Số bài đăng bị ẩn (IsVisible = false)
    /// </summary>
    public int HiddenPosts { get; set; }

    /// <summary>
    /// Số bài đăng đang hoạt động (không bị xóa và đang hiển thị)
    /// </summary>
    public int ActivePosts { get; set; }

    /// <summary>
    /// Tổng số báo cáo
    /// </summary>
    public int TotalReports { get; set; }

    /// <summary>
    /// Số báo cáo đang chờ xử lý
    /// </summary>
    public int PendingReports { get; set; }
}




