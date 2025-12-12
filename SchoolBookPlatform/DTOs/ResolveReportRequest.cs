namespace SchoolBookPlatform.DTOs;

public class ResolveReportRequest
{
    public Guid ReportId { get; set; }
    public string Action { get; set; } = string.Empty; //DeleteMessage, WarnUser, Deny
    public string? Notes { get; set; }
}