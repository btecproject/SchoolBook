namespace SchoolBookPlatform.Models;

public class Follower
{
    public Guid UserId { get; set; }       // Người được follow
    public Guid FollowerId { get; set; }   // Người follow
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public User FollowerUser { get; set; } = null!;
}