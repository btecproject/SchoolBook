using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.ViewModels.Admin;

public class UserListViewModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
    public bool FaceRegistered { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AvatarUrl { get; set; }
}

