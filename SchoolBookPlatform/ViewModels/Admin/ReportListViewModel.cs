namespace SchoolBookPlatform.ViewModels.Admin;

/// <summary>
/// ViewModel cho danh sách báo cáo
/// </summary>
public class ReportListViewModel
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public string PostTitle { get; set; } = string.Empty;
    public string ReporterUsername { get; set; } = string.Empty;
    public Guid ReporterId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewerUsername { get; set; }
}

/// <summary>
/// ViewModel cho thống kê báo cáo
/// </summary>
public class ReportStatisticsViewModel
{
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int TotalCount { get; set; }
}

