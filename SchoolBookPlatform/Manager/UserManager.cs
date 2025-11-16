using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Manager
{
    public static class UserManager
    {
        public static async Task<User?> GetUserByEmailAsync(this AppDbContext db, string email)
        {
            var user  = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return null;
            }
            return user;
        }
        public static async Task<bool> IsUserEmailExistAsync(this AppDbContext db, string email)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                return false;
            }
            return true;
        }
        public static async Task<List<string>> GetUserRolesAsync(this AppDbContext db, Guid userId)
        {
            var roleNames = await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Join(db.Roles, 
                    ur => ur.RoleId, 
                    r => r.Id, 
                   (ur, r) => r.Name)
                .Where(name => name != null)
                .ToListAsync();
            return roleNames;
        }

        public static async Task<bool> IsUserInRoleAsync(this AppDbContext db, Guid userId, string roleName)
        {
            var isInRole = await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Join(db.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => r.Name)
                .AnyAsync(name => name == roleName);
            return isInRole;
        }

        public static async Task<string?> GetTrustedDeviceInfoAsync(this AppDbContext db, Guid userId)
        {
            var deviceInfo = await db.TrustedDevices
                .Where(td => td.UserId == userId)
                .Select(td => td.DeviceInfo)
                .FirstOrDefaultAsync();
            return deviceInfo;
        }
    }
}
