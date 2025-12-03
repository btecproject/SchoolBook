namespace SchoolBookPlatform.Models;

public class UserToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime LoginAt { get; set; } = DateTime.UtcNow.AddHours(7);
    public DateTime ExpiredAt { get; set; }
    public bool IsRevoked { get; set; } = false;
}