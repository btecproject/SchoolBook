using SchoolBookPlatform.ViewModels.Post;

namespace SchoolBookPlatform.ViewModels.Profile;

public class ProfileViewModel
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = null!;
    public string? FullName { get; set; }
    public string AvatarUrl { get; set; } = "/images/avatars/default.png";
    public string Bio { get; set; } = "Chưa có mô tả";
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsEmailPublic { get; set; }
    public bool IsPhonePublic { get; set; }
    public bool IsBirthDatePublic { get; set; }
    public bool IsFollowersPublic { get; set; }

    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public bool IsFollowing { get; set; }
    public bool IsOwner { get; set; }
    public bool CanEdit { get; set; }
    
    public List<PostViewModel> UserPosts { get; set; } = new();
    public int PostCount { get; set; }
}