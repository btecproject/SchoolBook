using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class PostController : Controller
{
    private readonly AppDbContext _context;

    public PostController(AppDbContext context)
    {
        _context = context;
    }

    // GET: Danh sách post
    public async Task<IActionResult> Index()
    {
        var posts = await _context.Posts
            .Include(p => p.User)
            .Where(p => !p.IsDeleted && p.IsVisible)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return View(posts);
    }

    // GET: Chi tiết post
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

    // POST: Tạo post mới
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Post post)
    {
        if (ModelState.IsValid)
        {
            post.UserId = GetCurrentUserId();
            post.CreatedAt = DateTime.UtcNow;
            _context.Add(post);
            await _context.SaveChangesAsync();
            return RedirectToAction("Home", "Feeds");
        }
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

            // ✅ CHỈ CẦN XÓA POST - DATABASE SẼ TỰ ĐỘNG XÓA CÁC BẢNG LIÊN QUAN
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