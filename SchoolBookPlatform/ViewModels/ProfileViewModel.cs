namespace SchoolBookPlatform.ViewModels;

public class ProfileViewModel
{
    // From Users table
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? AvatarUrl { get; set; }

    // From UserProfile table
    public string? FullName { get; set; }
    public string? Bio { get; set; }
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
}
