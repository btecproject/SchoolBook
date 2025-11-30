using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Models.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class PostController : Controller
{
    private readonly ILogger<PostController> logger;
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly Cloudinary _cloudinary;

    public PostController(AppDbContext context, IWebHostEnvironment webHostEnvironment, IConfiguration configuration, ILogger<PostController> _logger)
    {
        logger = _logger;
        _context = context;
        _webHostEnvironment = webHostEnvironment;
        
        // Khởi tạo Cloudinary
        var cloudinaryConfig = configuration.GetSection("Cloudinary");
        var account = new Account(
            cloudinaryConfig["CloudName"],
            cloudinaryConfig["ApiKey"],
            cloudinaryConfig["ApiSecret"]
        );
        _cloudinary = new Cloudinary(account);
    }

    // GET: Hiển thị form tạo post
    public IActionResult Create()
    {
        var viewModel = new CreatePostViewModel();
        return View(viewModel);
    }

    // POST: Tạo post mới
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePostViewModel viewModel)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Tạo post mới
                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    Title = viewModel.Title,
                    Content = viewModel.Content,
                    VisibleToRoles = viewModel.VisibleToRoles,
                    UserId = GetCurrentUserId(),
                    CreatedAt = DateTime.UtcNow,
                    IsVisible = true,
                    IsDeleted = false
                };

                _context.Posts.Add(post);

                // Xử lý file đính kèm nếu có
                if (viewModel.Attachments != null && viewModel.Attachments.Any())
                {
                    await HandlePostAttachments(post, viewModel.Attachments);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Bài viết đã được tạo thành công!";
                return RedirectToAction("Home", "Feeds");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Có lỗi xảy ra khi tạo bài viết: " + ex.Message);
            }
        }

        // Nếu có lỗi, trả về view với model
        return View(viewModel);
    }

    private async Task HandlePostAttachments(Post post, List<IFormFile> attachments)
    {
        foreach (var file in attachments)
        {
            if (file.Length > 0)
            {
                // Tạo public ID unique cho Cloudinary
                var publicId = $"SchoolBook/Post/{post.Id}/PostAttachment/{Guid.NewGuid()}";

                // Xác định resource type
                var resourceType = GetResourceType(file);
                
                UploadResult uploadResult;

                if (resourceType == ResourceType.Video || resourceType == ResourceType.Raw)
                {
                    // Upload video hoặc file raw
                    var uploadParams = new RawUploadParams()
                    {
                        File = new FileDescription(file.FileName, file.OpenReadStream()),
                        PublicId = publicId,
                        Folder = $"SchoolBook/Post/{post.Id}/PostAttachment",
                        Overwrite = false
                    };

                    uploadResult = await _cloudinary.UploadAsync(uploadParams);
                }
                else
                {
                    // Upload image (mặc định)
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.FileName, file.OpenReadStream()),
                        PublicId = publicId,
                        Folder = $"SchoolBook/Post/{post.Id}/PostAttachment",
                        Overwrite = false
                    };

                    uploadResult = await _cloudinary.UploadAsync(uploadParams);
                }

                if (uploadResult.Error != null)
                {
                    throw new Exception($"Lỗi upload file lên Cloudinary: {uploadResult.Error.Message}");
                }

                // Tạo post attachment với URL từ Cloudinary
                var attachment = new PostAttachment
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    FileName = file.FileName,
                    FilePath = uploadResult.SecureUrl.ToString(), // Sử dụng Secure URL
                    FileSize = (int)file.Length,
                    UploadedAt = DateTime.UtcNow
                };

                _context.PostAttachments.Add(attachment);
            }
        }
    }
    
    private ResourceType GetResourceType(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    
        // Định nghĩa các extension cho video
        var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".flv", ".webm" };
    
        // Định nghĩa các extension cho raw files (document, etc.)
        var rawExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".rar" };
    
        if (videoExtensions.Contains(extension))
            return ResourceType.Video;
        else if (rawExtensions.Contains(extension))
            return ResourceType.Raw;
        else
            return ResourceType.Image; // Mặc định là image
    }

    // Các action khác giữ nguyên...
    public async Task<IActionResult> Index()
    {
        var posts = await _context.Posts
            .Include(p => p.User)
            .Where(p => !p.IsDeleted && p.IsVisible)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return View(posts);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var post = await _context.Posts
            .Include(p => p.User)
            .Include(p => p.Comments)
            .Include(p => p.Attachments)
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted && p.IsVisible);
        if (post == null) return NotFound();
        return View(post);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Post/Delete/{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var post = await _context.Posts
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return Json(new { success = false, message = "Bài viết không tồn tại." });
            }

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("HighAdmin");
        
            if (post.UserId != currentUserId && !isAdmin)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa bài viết này." });
            }

            // Xóa file từ Cloudinary trước
            foreach (var attachment in post.Attachments)
            {
                logger.LogInformation("DeleteFileFromCloudinary post attachment: {id}", post.Attachments.First(a => a.Id == attachment.Id));

                await DeleteFileFromCloudinary(attachment.FilePath, post.Id);
            }

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Bài viết và tất cả dữ liệu liên quan đã được xóa thành công." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Có lỗi xảy ra khi xóa bài viết: " + ex.Message });
        }
    }

    private async Task DeleteFileFromCloudinary(string fileUrl, Guid postId)
    {
        try
        {
            // Extract public ID từ URL
            var uri = new Uri(fileUrl);
            var segments = uri.AbsolutePath.Split('/');
            var publicIdWithExtension = segments.Last();
            var publicId = Path.GetFileNameWithoutExtension(publicIdWithExtension);
            
        
            // Tìm full public id với folder
            var fullPublicId = $"SchoolBook/Post/{postId}/PostAttachment/{publicId}";

            var deleteParams = new DeletionParams(fullPublicId)
            {
                ResourceType = ResourceType.Image // Cloudinary sẽ tự động detect loại resource
            };
            
            var result = await _cloudinary.DestroyAsync(deleteParams);
        
            if (result.Error != null)
            {
                // Log lỗi nhưng không throw exception để không ảnh hưởng đến flow chính
                Console.WriteLine($"Lỗi xóa file từ Cloudinary: {result.Error.Message}");
            }
        }
        catch (Exception ex)
        {
            // Log lỗi nhưng không throw exception
            Console.WriteLine($"Lỗi khi xóa file từ Cloudinary: {ex.Message}");
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out Guid userId))
        {
            return userId;
        }
        
        userIdClaim = User.FindFirst("UserId")?.Value;
        if (Guid.TryParse(userIdClaim, out userId))
        {
            return userId;
        }
        
        return Guid.Empty;
    }
}