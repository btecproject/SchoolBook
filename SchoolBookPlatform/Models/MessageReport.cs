namespace SchoolBookPlatform.Models;

public class MessageReport
{
    public Guid Id { get; set; }
    public long MessageId { get; set; }
    public Guid ReporterId { get; set; }
    public Guid ReportedUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? DecryptedContent { get; set; } // text
    public string? FileUrl { get; set; }          // url
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }
    
    // Nav
    public Message Message { get; set; } = null!;
    public User Reporter { get; set; } = null!;
    public User ReportedUser { get; set; } = null!;
}