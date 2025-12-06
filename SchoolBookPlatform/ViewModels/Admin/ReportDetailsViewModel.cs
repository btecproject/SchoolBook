namespace SchoolBookPlatform.ViewModels.Admin;

/// <summary>
/// ViewModel cho chi tiết báo cáo
/// </summary>
public class ReportDetailsViewModel
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public string PostTitle { get; set; } = string.Empty;
    public string PostContent { get; set; } = string.Empty;
    public string PostAuthorUsername { get; set; } = string.Empty;
    public DateTime PostCreatedAt { get; set; }
    public bool PostIsDeleted { get; set; }
    public bool PostIsVisible { get; set; }
    public Guid ReporterId { get; set; }
    public string ReporterUsername { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewerUsername { get; set; }
    public DateTime? ReviewedAt { get; set; }
    
    // Thống kê các báo cáo khác của cùng bài đăng
    public int PendingReportsCount { get; set; }
    public int ApprovedReportsCount { get; set; }
    public int RejectedReportsCount { get; set; }
    public int TotalReportsCount { get; set; }
}

