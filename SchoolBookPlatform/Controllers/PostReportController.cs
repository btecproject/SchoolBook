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
/// Controller xử lý các request liên quan đến báo cáo bài đăng
/// </summary>
[Authorize]
[Route("[controller]")]
public class PostReportController(
    PostService postService,
    AppDbContext db,
    ILogger<PostReportController> logger) : Controller
{
    /// <summary>
    /// Lấy ID của user hiện tại từ Claims
    /// </summary>
    private Guid GetCurrentUserId() => 
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// POST: PostReport/Report
    /// Tạo báo cáo về bài đăng
    /// </summary>
    /// <param name="postId">ID của bài đăng bị báo cáo</param>
    /// <param name="reason">Lý do báo cáo</param>
    /// <returns>Redirect về Details của PostController</returns>
    [HttpPost("Report")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(Guid postId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do báo cáo.";
            return RedirectToAction("Details", "Post", new { id = postId });
        }

        var userId = GetCurrentUserId();
        var report = await postService.CreateReportAsync(userId, postId, reason);

        if (report == null)
        {
            TempData["ErrorMessage"] = "Không thể tạo báo cáo.";
            return RedirectToAction("Details", "Post", new { id = postId });
        }

        TempData["SuccessMessage"] = "Báo cáo đã được gửi thành công!";
        return RedirectToAction("Details", "Post", new { id = postId });
    }

    /// <summary>
    /// GET: PostReport/ReportForm/{postId}
    /// Hiển thị form báo cáo dưới dạng Partial View
    /// </summary>
    [HttpGet("ReportForm/{postId}")]
    public async Task<IActionResult> ReportForm(Guid postId)
    {
        var post = await db.Posts
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == postId);

        if (post == null)
        {
            return NotFound();
        }

        var viewModel = new ReportFormViewModel
        {
            PostId = postId,
            PostTitle = post.Title,
            AuthorName = post.User.Username
        };

        return PartialView("_ReportFormPartial", viewModel);
    }
}