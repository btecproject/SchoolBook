using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Post;

namespace SchoolBookPlatform.Controllers;

/// <summary>
/// Controller xử lý các request liên quan đến bài đăng
/// Bao gồm: tạo, xóa, vote, comment, report (CHỈ QUẢN LÝ)
/// </summary>
[Authorize]
public class PostController(
    PostService postService,
    AppDbContext db,
    ILogger<PostController> logger) : Controller
{
    /// <summary>
    /// Lấy ID của user hiện tại từ Claims
    /// </summary>
    private Guid GetCurrentUserId() => 
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // XÓA PHẦN Index VÀ Following VÌ ĐÃ CHUYỂN SANG FeedsController

    /// <summary>
    /// GET: Post/Create
    /// Hiển thị form tạo bài đăng mới
    /// </summary>
    /// <returns>View form tạo bài đăng</returns>
    public IActionResult Create()
    {
        return View(new CreatePostViewModel());
    }

    /// <summary>
    /// POST: Post/Create
    /// Xử lý tạo bài đăng mới
    /// </summary>
    /// <param name="model">Dữ liệu từ form</param>
    /// <returns>Redirect về Home feed nếu thành công</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePostViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = GetCurrentUserId();
        var post = await postService.CreatePostAsync(
            userId, 
            model.Title, 
            model.Content, 
            model.VisibleToRoles,
            model.Files);

        if (post == null)
        {
            ModelState.AddModelError("", "Không thể tạo bài đăng.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đăng bài thành công!";
        return RedirectToAction("Home", "Feeds"); // Chuyển hướng về trang feed
    }

    // GIỮ NGUYÊN CÁC ACTION KHÁC (Details, Edit, Delete, Comment, Report, etc.)
    // CHỈ THAY ĐỔI REDIRECT URL Ở MỘT SỐ NƠI:
    
    // Trong action Delete, đổi redirect từ Index sang Home
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        var userRoles = await db.GetUserRolesAsync(userId);
        var isHighAdmin = userRoles.Contains("HighAdmin");

        var success = await postService.DeletePostAsync(userId, id, hardDelete: isHighAdmin);

        if (!success)
        {
            TempData["ErrorMessage"] = "Không thể xóa bài đăng này.";
            return RedirectToAction("Home", "Feeds"); // Đổi redirect
        }

        TempData["SuccessMessage"] = isHighAdmin ? 
            "Đã xóa bài đăng và toàn bộ file đính kèm vĩnh viễn!" : 
            "Đã xóa bài đăng thành công!";
        return RedirectToAction("Home", "Feeds"); // Đổi redirect
    }

    // ... GIỮ NGUYÊN CÁC ACTION KHÁC, CHỈ SỬA REDIRECT URL KHI CẦN
}