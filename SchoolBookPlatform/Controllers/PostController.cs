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

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class PostController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public PostController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
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
        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "posts");
        
        // Tạo thư mục nếu chưa tồn tại
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        foreach (var file in attachments)
        {
            if (file.Length > 0)
            {
                // Tạo tên file unique
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Lưu file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Tạo post attachment
                var attachment = new PostAttachment
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    FileName = file.FileName,
                    FilePath = $"/uploads/posts/{fileName}",
                    FileSize = (int)file.Length,
                    UploadedAt = DateTime.UtcNow
                };

                _context.PostAttachments.Add(attachment);
            }
        }
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

            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Bài viết và tất cả dữ liệu liên quan đã được xóa thành công." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Có lỗi xảy ra khi xóa bài viết: " + ex.Message });
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