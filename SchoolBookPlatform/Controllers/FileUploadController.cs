using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SchoolBookPlatform.Controllers
{
    [Route("api/files")]
    [Authorize]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileUploadController> _logger;

        // Allowed file types
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly string[] AllowedVideoExtensions = { ".mp4", ".webm", ".mov", ".avi" };
        private static readonly string[] AllowedFileExtensions = { ".pdf", ".doc", ".docx", ".txt", ".xlsx", ".xls", ".zip", ".rar" };

        // Max file sizes (in bytes)
        private const long MaxImageSize = 10 * 1024 * 1024; // 10MB
        private const long MaxVideoSize = 100 * 1024 * 1024; // 100MB
        private const long MaxFileSize = 50 * 1024 * 1024; // 50MB

        public FileUploadController(IWebHostEnvironment env, ILogger<FileUploadController> logger)
        {
            _env = env;
            _logger = logger;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(100_000_000)] // 100MB
        public async Task<IActionResult> Upload([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No file uploaded" });

                var extension = Path.GetExtension(file.FileName).ToLower();
                var fileType = GetFileType(extension);

                if (fileType == null)
                    return BadRequest(new { error = "File type not allowed" });

                // Validate file size
                if (!ValidateFileSize(file.Length, fileType))
                    return BadRequest(new { error = $"File too large. Max size for {fileType}: {GetMaxSizeText(fileType)}" });

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{extension}";
                var uploadPath = Path.Combine(_env.WebRootPath, "uploads", fileType);

                // Create directory if not exists
                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var fileUrl = $"/uploads/{fileType}/{fileName}";

                _logger.LogInformation($"File uploaded: {fileName}, Type: {fileType}, Size: {file.Length}");

                return Ok(new
                {
                    success = true,
                    url = fileUrl,
                    type = fileType,
                    name = file.FileName,
                    size = file.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file: {ex.Message}");
                return StatusCode(500, new { error = "Failed to upload file" });
            }
        }

        private string? GetFileType(string extension)
        {
            if (AllowedImageExtensions.Contains(extension))
                return "image";
            if (AllowedVideoExtensions.Contains(extension))
                return "video";
            if (AllowedFileExtensions.Contains(extension))
                return "file";
            return null;
        }

        private bool ValidateFileSize(long size, string fileType)
        {
            return fileType switch
            {
                "image" => size <= MaxImageSize,
                "video" => size <= MaxVideoSize,
                "file" => size <= MaxFileSize,
                _ => false
            };
        }

        private string GetMaxSizeText(string fileType)
        {
            return fileType switch
            {
                "image" => "10MB",
                "video" => "100MB",
                "file" => "50MB",
                _ => "Unknown"
            };
        }
    }
}