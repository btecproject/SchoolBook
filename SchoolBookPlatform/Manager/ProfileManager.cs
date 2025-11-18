using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Manager
{
    public static class ProfileManager
    {
        public static async Task<UserProfile> EnsureProfileAsync(this AppDbContext db, Guid userId)
        {
            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile != null)
                return profile;

            // Chưa có → tạo mới
            profile = new UserProfile
            {
                UserId = userId,
                FullName = null,
                Bio = null,
                AvatarUrl = null,
                Gender = null,
                BirthDate = null,
                IsEmailPublic = false,
                IsPhonePublic = false,
                IsBirthDatePublic = false,
                IsFollowersPublic = true,
                UpdatedAt = DateTime.UtcNow
            };

            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync();

            return profile;
        }
    }
}