using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

public class FaceProfile
{
    [Key] [ForeignKey(nameof(User))] public Guid UserId { get; set; }

    public string? PersonId { get; set; } // Azure PersonId
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow.AddHours(7);
    public DateTime? LastVerifiedAt { get; set; }
    public double? ConfidenceLast { get; set; }
    public bool IsLivenessVerified { get; set; } = false;
    public User User { get; set; }
}