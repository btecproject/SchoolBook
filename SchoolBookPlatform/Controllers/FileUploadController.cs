using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Controllers
{
    [Route("api/files")]
    [Authorize]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FileUploadController> _logger;

        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly string[] AllowedVideoExtensions = { ".mp4", ".webm", ".mov", ".avi" };
        private static readonly string[] AllowedFileExtensions = { ".pdf", ".doc", ".docx", ".txt", ".xlsx", ".xls", ".zip", ".rar" };

        private const long MaxImageSize = 10 * 1024 * 1024; // 10MB
        private const long MaxVideoSize = 50 * 1024 * 1024; // 50MB
        private const long MaxFileSize = 25 * 1024 * 1024; // 25MB

        public FileUploadController(AppDbContext context, ILogger<FileUploadController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(50_000_000)] // 50MB
        public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] int segmentId, [FromForm] int messageIndex)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No file uploaded" });

                var extension = Path.GetExtension(file.FileName).ToLower();
                var fileType = GetFileType(extension);

                if (fileType == null)
                    return BadRequest(new { error = "File type not allowed" });

                if (!ValidateFileSize(file.Length, fileType))
                    return BadRequest(new { error = $"File too large. Max: {GetMaxSizeText(fileType)}" });

                // Convert to byte array
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var fileBytes = memoryStream.ToArray();

                // Lưu vào database
                var attachment = new ChatAttachment
                {
                    SegmentId = segmentId,
                    MessageIndex = messageIndex,
                    FileName = file.FileName,
                    FileType = fileType,
                    MimeType = file.ContentType,
                    FileSize = file.Length,
                    FileData = fileBytes,
                    UploadedAt = DateTime.UtcNow
                };

                _context.ChatAttachments.Add(attachment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"File saved to DB: ID={attachment.Id}, Name={file.FileName}, Size={file.Length}");

                return Ok(new
                {
                    success = true,
                    attachmentId = attachment.Id,
                    type = fileType,
                    name = file.FileName,
                    size = file.Length,
                    mimeType = file.ContentType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file: {ex.Message}");
                return StatusCode(500, new { error = "Failed to upload file" });
            }
        }

        // API để lấy file từ database
        [HttpGet("{attachmentId}")]
        public IActionResult GetFile(int attachmentId)
        {
            try
            {
                var attachment = _context.ChatAttachments.Find(attachmentId);
                
                if (attachment == null)
                    return NotFound(new { error = "File not found" });

                return File(attachment.FileData, attachment.MimeType, attachment.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving file: {ex.Message}");
                return StatusCode(500, new { error = "Failed to retrieve file" });
            }
        }

        private string? GetFileType(string extension)
        {
            if (AllowedImageExtensions.Contains(extension)) return "image";
            if (AllowedVideoExtensions.Contains(extension)) return "video";
            if (AllowedFileExtensions.Contains(extension)) return "file";
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
                "video" => "50MB",
                "file" => "25MB",
                _ => "Unknown"
            };
        }
    }
}