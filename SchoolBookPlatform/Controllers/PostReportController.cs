using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Post;

namespace SchoolBookPlatform.Controllers;

[Authorize]
[Route("[controller]")]
public class PostReportController(
    PostService postService,
    AppDbContext db,
    ILogger<PostReportController> logger) : Controller
{
    private Guid GetCurrentUserId() => 
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("Report")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(Guid postId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Json(new { success = false, message = "Vui lòng nhập lý do báo cáo." });
        }

        try 
        {
            var userId = GetCurrentUserId();
            var report = await postService.CreateReportAsync(userId, postId, reason);

            if (report == null)
            {
                return Json(new { success = false, message = "Không thể tạo báo cáo. Bài viết có thể không tồn tại." });
            }

            return Json(new { success = true, message = "Báo cáo đã được gửi thành công!" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reporting post {PostId}", postId);
            return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống khi gửi báo cáo." });
        }
    }

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