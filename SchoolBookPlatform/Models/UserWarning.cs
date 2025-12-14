namespace SchoolBookPlatform.Models;

public class UserWarning
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? WarnedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow.AddHours(7);
}