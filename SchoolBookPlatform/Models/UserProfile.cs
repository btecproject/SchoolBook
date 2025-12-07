using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.Models;

public class UserProfile
{
    [Key]
    public Guid UserId { get; set; }

    public string? FullName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; } = "~/images/avatars/default.png";
    public string? Gender { get; set; } // "Male", "Female", "Other"
    public DateTime? BirthDate { get; set; }

    public bool IsEmailPublic { get; set; } = false;
    public bool IsPhonePublic { get; set; } = false;
    public bool IsBirthDatePublic { get; set; } = false;
    public bool IsFollowersPublic { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow.AddHours(7);

    public User User { get; set; } = null!;
}