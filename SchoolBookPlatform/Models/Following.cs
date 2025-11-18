namespace SchoolBookPlatform.Models;

public class Following
{
    public Guid UserId { get; set; }        // Người đang follow
    public Guid FollowingId { get; set; }   // Người được follow
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public User FollowingUser { get; set; } = null!;
}