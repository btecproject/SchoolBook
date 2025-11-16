using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

public class UserProfile
{
    [Key]                           
    [ForeignKey("User")]
    public Guid UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public bool EmailVisibility { get; set; }
    public bool PhoneVisibility { get; set; }

    public User User { get; set; } = null!;
}