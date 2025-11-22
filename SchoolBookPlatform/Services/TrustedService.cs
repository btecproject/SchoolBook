using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
namespace SchoolBookPlatform.Services;

public class TrustedService(AppDbContext db, ILogger<TrustedService> logger)
{
    public async Task<bool> IsTrustedAsync(Guid userId, string ip, string device)
    {
        return await db.TrustedDevices.AnyAsync(t =>
            t.UserId == userId &&
            t.IPAddress == ip &&
            t.DeviceInfo == device &&
            !t.IsRevoked &&
            t.ExpiresAt > DateTime.UtcNow);
    }
    public string GetDeviceIpAsync(HttpContext context)
    {
        context.Request.Headers.TryGetValue("HTTP-X-FORWARDED-FOR", out var forwardedFor);
        var ip = forwardedFor.ToString();
    
        if (!string.IsNullOrEmpty(ip))
        {
            logger.LogInformation("IP: X-Forwarded-For :"+ ip);
            return ip;
        }
        
        logger.LogInformation("IP RemoteIp: "+context.Connection.RemoteIpAddress);
        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        
    }

    public string GetDeviceInfoAsync(HttpContext context)
    {
        var device = context.Request.Headers["User-Agent"].ToString();
        logger.LogInformation("Info: "+ device);
        return device;
    }
    
    public async Task AddTrustedDeviceAsync(Guid userId, string ip, string device)
    {
        var existing = await db.TrustedDevices.FirstOrDefaultAsync(t =>
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
            db.TrustedDevices.Add(trusted);
        }

        await db.SaveChangesAsync();
    }
}

public static class StringExtensions
{
    public static string Truncate(this string? value, int maxLength)
    {
        return value?.Length > maxLength ? value[..maxLength] : value ?? "";
    }
}