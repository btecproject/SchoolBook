using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace SchoolBookPlatform.Services
{
    public class CloudinaryService(Cloudinary cloudinary, ILogger<CloudinaryService> logger)
    {
        // Upload file cho chat message
        public async Task<CloudinaryUploadResult> UploadChatFileAsync(
            IFormFile file,
            Guid userId,
            Guid conversationId,
            long messageId)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return new CloudinaryUploadResult
                    {
                        Success = false,
                        Message = "File không hợp lệ"
                    };
                }

                // Validate file size (max 50MB)
                const long maxFileSize = 50 * 1024 * 1024; // 50MB
                if (file.Length > maxFileSize)
                {
                    return new CloudinaryUploadResult
                    {
                        Success = false,
                        Message = "File vượt quá 50MB"
                    };
                }

                // Determine resource type
                var resourceType = GetResourceType(file.ContentType);

                // Build folder path: messages/{userId}/{conversationId}/
                var folderPath = $"messages/{userId}/{conversationId}";

                // Get file extension
                var fileExtension = Path.GetExtension(file.FileName);

                // Public ID: messages/{userId}/{conversationId}/{messageId}.{ext}
                var publicId = $"{folderPath}/{messageId}{fileExtension}";

                using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = publicId,
                    Folder = folderPath,
                    Overwrite = false,
                    UniqueFilename = false,
                    UseFilename = false
                };

                // Upload based on resource type
                RawUploadResult uploadResult;

                if (resourceType == ResourceType.Video)
                {
                    var videoParams = new VideoUploadParams
                    {
                        File = uploadParams.File,
                        PublicId = uploadParams.PublicId,
                        Folder = uploadParams.Folder,
                        Overwrite = uploadParams.Overwrite,
                        UniqueFilename = uploadParams.UniqueFilename,
                        UseFilename = uploadParams.UseFilename
                    };
                    uploadResult = await cloudinary.UploadAsync(videoParams);
                }
                else if (resourceType == ResourceType.Image)
                {
                    uploadResult = await cloudinary.UploadAsync(uploadParams);
                }
                else // Raw (documents, etc)
                {
                    var rawParams = new RawUploadParams
                    {
                        File = uploadParams.File,
                        PublicId = uploadParams.PublicId,
                        Folder = uploadParams.Folder,
                        Overwrite = uploadParams.Overwrite,
                        UniqueFilename = uploadParams.UniqueFilename,
                        UseFilename = uploadParams.UseFilename
                    };
                    uploadResult = await cloudinary.UploadAsync(rawParams);
                }

                if (uploadResult.Error != null)
                {
                    logger.LogError("Cloudinary upload error: {Error}", uploadResult.Error.Message);
                    return new CloudinaryUploadResult
                    {
                        Success = false,
                        Message = uploadResult.Error.Message
                    };
                }

                logger.LogInformation("File uploaded successfully: {Url}", uploadResult.SecureUrl);

                return new CloudinaryUploadResult
                {
                    Success = true,
                    Url = uploadResult.SecureUrl.ToString(),
                    PublicId = uploadResult.PublicId,
                    ResourceType = resourceType.ToString().ToLower(),
                    Format = uploadResult.Format,
                    FileName = file.FileName,
                    FileSize = file.Length,
                    Message = "Upload thành công"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading file to Cloudinary");
                return new CloudinaryUploadResult
                {
                    Success = false,
                    Message = "Lỗi khi upload file: " + ex.Message
                };
            }
        }

        // Delete file from Cloudinary
        public async Task<bool> DeleteChatFileAsync(string publicId, string resourceType)
        {
            try
            {
                var deleteParams = new DeletionParams(publicId)
                {
                    ResourceType = ParseResourceType(resourceType)
                };

                var result = await cloudinary.DestroyAsync(deleteParams);

                if (result.Result == "ok")
                {
                    logger.LogInformation("File deleted successfully: {PublicId}", publicId);
                    return true;
                }

                logger.LogWarning("Failed to delete file: {PublicId}, Result: {Result}", 
                    publicId, result.Result);
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting file from Cloudinary: {PublicId}", publicId);
                return false;
            }
        }

        // Determine resource type from content type
        private ResourceType GetResourceType(string contentType)
        {
            if (contentType.StartsWith("image/"))
                return ResourceType.Image;
            
            if (contentType.StartsWith("video/"))
                return ResourceType.Video;
            
            return ResourceType.Raw;
        }

        // Parse resource type string
        private ResourceType ParseResourceType(string resourceType)
        {
            return resourceType.ToLower() switch
            {
                "image" => ResourceType.Image,
                "video" => ResourceType.Video,
                _ => ResourceType.Raw
            };
        }

        // Get file type from extension
        public static byte GetMessageType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            
            return extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => 1, // Image
                ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" => 2, // Video
                _ => 3 // File
            };
        }
    }

    // Result model
    public class CloudinaryUploadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? PublicId { get; set; }
        public string? ResourceType { get; set; }
        public string? Format { get; set; }
        public string? FileName { get; set; }
        public long FileSize { get; set; }
    }
}