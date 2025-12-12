namespace SchoolBookPlatform.ViewModels.MessageReport;

public class PaginatedReportViewModel
{
    public List<Models.MessageReport> Reports { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }

}