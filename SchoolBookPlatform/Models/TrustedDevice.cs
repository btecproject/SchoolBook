namespace SchoolBookPlatform.Models;

public class TrustedDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string IPAddress { get; set; } = null!;
    public string DeviceInfo { get; set; } = null!;
    public DateTime TrustedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
}