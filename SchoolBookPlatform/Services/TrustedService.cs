using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services;

public class TrustedService
{
    private readonly AppDbContext _db;

    public TrustedService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsTrustedAsync(Guid userId, string ip, string device)
    {
        return await _db.TrustedDevices.AnyAsync(t =>
            t.UserId == userId &&
            t.IPAddress == ip &&
            t.DeviceInfo == device &&
            !t.IsRevoked &&
            t.ExpiresAt > DateTime.UtcNow);
    }

    public async Task AddTrustedDeviceAsync(Guid userId, string ip, string device)
    {
        var existing = await _db.TrustedDevices.FirstOrDefaultAsync(t =>
            t.UserId == userId && t.IPAddress == ip && t.DeviceInfo == device);

        if (existing != null)
        {
            existing.ExpiresAt = DateTime.UtcNow.AddDays(30);
            existing.IsRevoked = false;
        }
        else
        {
            var trusted = new TrustedDevice
            {
                UserId = userId,
                IPAddress = ip.Truncate(50),
                DeviceInfo = device.Truncate(200),
                ExpiresAt = DateTime.UtcNow.AddDays(3)
            };
            _db.TrustedDevices.Add(trusted);
        }

        await _db.SaveChangesAsync();
    }
}

public static class StringExtensions
{
    public static string Truncate(this string? value, int maxLength)
    {
        return value?.Length > maxLength ? value[..maxLength] : value ?? "";
    }
}