using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;

namespace SchoolBookPlatform.Manager
{
    public static class UserManager
    {
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

    }
}
