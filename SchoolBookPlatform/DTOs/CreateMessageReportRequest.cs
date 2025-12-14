namespace SchoolBookPlatform.DTOs;

public class CreateMessageReportRequest
{
    public long MessageId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? DecryptedContent { get; set; } 
    public string? DecryptedFileUrl { get; set; } 
}