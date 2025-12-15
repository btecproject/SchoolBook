using SchoolBookPlatform.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using SchoolBookPlatform.Data;

namespace SchoolBookPlatform.Services;

public class AvatarService(
    IConfiguration config,
    AppDbContext db,
    Cloudinary cloudinary,
    ILogger<AvatarService> logger)
{
    public async Task<bool> UploadAvatar(IFormFile avatar, User user, UserProfile userProfile)
    {
        var publicId = $"avatars/{user.Id}";
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(avatar.FileName, avatar.OpenReadStream()),
            PublicId = publicId,
            Overwrite = true,
            Transformation = new Transformation()
                .Width(400).Height(400).Crop("thumb").Gravity("face")
                .Quality("auto").FetchFormat("jpg"),
            Folder = "schoolbook"
        };

        try
        {
            var uploadResult = await cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                logger.LogError($"Avatar Service: Upload Avatar Failed. Cloudinary Error: {uploadResult.Error.Message}");
                return false;
            }

            if (uploadResult.SecureUrl == null)
            {
                logger.LogError("Avatar Service: Upload Success but SecureUrl is NULL");
                return false;
            }

            userProfile.AvatarUrl = uploadResult.SecureUrl.ToString();
            userProfile.UpdatedAt = DateTime.UtcNow.AddHours(7);
        
            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            logger.LogError($"Avatar Service Exception: {e.Message}");
            return false;
        }
    
        return true;
    }
    
    public async Task<bool> DeleteAvatar(User user, UserProfile userProfile, bool deleteAvatarOnly)
    {
        var publicId = $"schoolbook/avatars/{user.Id}";
        var deleteParams = new DeletionParams(publicId)
        {
            ResourceType = ResourceType.Image
        };
        try
        {
            var deleteResult = await cloudinary.DestroyAsync(deleteParams);
            if (deleteResult.Result == "ok")
            {
                if (deleteAvatarOnly)
                {
                    userProfile.AvatarUrl = null;
                    userProfile.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync();
                }
                return true;
            }
            logger.LogInformation("Avatar Service Error: " + deleteResult.Result);
            logger.LogError("Avatar Service: Delete Avatar Failed");
            logger.LogError("Error: " + deleteResult.Error);
            return false;
        }
        catch(Exception e)
        {
            logger.LogError("Avatar Service:" + e.Message);
        }
        return true;
    }

    public string GetAvatar(User user)
    {
        var profile = db.UserProfiles.Find(user.Id);
        if (profile != null && !string.IsNullOrEmpty(profile.AvatarUrl))
        {
            logger.LogInformation($"Avatar Service: Get Avatar Success for: {user.Username}: " + profile.AvatarUrl);
            return profile.AvatarUrl;
        }
        logger.LogInformation("Avatar Service: Get Avatar Failed for: "+user.Username + ": "+ profile?.AvatarUrl);
        return "/images/avatars/default.png";
    }
}