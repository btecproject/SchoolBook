using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.Models;

public class MessageAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public long MessageId { get; set; }

    [Required]
    public string CloudinaryUrl { get; set; } = string.Empty;

    [Required]
    public string ResourceType { get; set; } = string.Empty; // image, video, file raw

    public string? Format { get; set; } // jpg, mp4, pdf,...
    public string? FileName { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow.AddHours(7);

    // Navigation
    public Message Message { get; set; } = null!;
}