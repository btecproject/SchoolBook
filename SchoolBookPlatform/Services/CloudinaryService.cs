using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services;

public class CloudinaryUploadResult
{
    public bool Status { get; set; } = false; // true: success, false: failed
    public string? Url { get; set; }
    public string? ResourceType { get; set; }
    public string? Format { get; set; }
    public string? FileName { get; set; }
    public DateTime? UploadedAt { get; set; }
}
public class CloudinaryService(
    AppDbContext db,
    Cloudinary cloudinary,
    ILogger<CloudinaryService> logger
    )
{
    private const string SERVICE_NAME = "cloudinary: ";

    public async Task<CloudinaryUploadResult> UploadMessageAttachmentAsync(IFormFile file, Guid userId,Guid conversationId, Guid messageId)
    {
        var uploadParam = new RawUploadParams()
        {
            Overwrite = false,
            UniqueFilename = false,
            Folder = "schoolbook",
            PublicId = $"messages/{userId}/{conversationId}/{messageId}",
            UseFilename = true
        };
        try
        {
            var uploadResult = await cloudinary.UploadAsync(uploadParam);

            return new CloudinaryUploadResult()
            {
                Status = true,
                Url = uploadResult.SecureUrl.ToString(),
                ResourceType = uploadResult.ResourceType,
                Format = uploadResult.Format,
                FileName = uploadResult.DisplayName,
                UploadedAt = DateTime.UtcNow
            };
        }
        catch (Exception e)
        {
            logger.LogError(SERVICE_NAME + e.Message);
            return new CloudinaryUploadResult()
            {
                Status = false,
                Url = null,
                ResourceType = null,
                Format = null,
                FileName = null,
                UploadedAt = null
            };
        }
    }

    public async Task<bool> DeleteMessageAttachmentAsync(Guid userId, Guid conversationId, Guid messageId)
    {
        string publicId = $"messages/{userId}/{conversationId}/{messageId}";
        var deleteParam = new DeletionParams(publicId)
        {
            ResourceType = ResourceType.Auto
        };
        try
        {
            var deleteResult = await cloudinary.DestroyAsync(deleteParam);
            if (deleteResult.Result == "ok")
            {
                logger.LogInformation(SERVICE_NAME+"Deleted message attachment");
                return true;
            }
            logger.LogInformation(SERVICE_NAME +"Error :" + deleteResult.Result);
            logger.LogError(SERVICE_NAME + "Error : " + deleteResult.Error);
            return false;
        }
        catch(Exception e)
        {
            logger.LogError(SERVICE_NAME + e.Message);
        }
        return true;
    }
}