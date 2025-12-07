using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Post;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class FeedsController(
    TrustedService trustedService,
    PostService postService,
    AppDbContext db,
    ILogger<FeedsController> logger) : Controller
{
    /// <summary>
    /// Lấy ID của user hiện tại từ Claims
    /// </summary>
    private Guid GetCurrentUserId() => 
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// GET: Feeds/Home
    /// Hiển thị trang feed chính (bài đăng từ tất cả mọi người)
    /// </summary>
    /// <param name="page">Số trang (bắt đầu từ 1)</param>
    /// <param name="pageSize">Số bài đăng mỗi trang</param>
    /// <returns>View danh sách bài đăng</returns>
    public async Task<IActionResult> Home(int page = 1, int pageSize = 10)  // Đổi từ 20 thành 10
    {
        var userId = GetCurrentUserId();
        
        // Lấy bài đăng từ tất cả mọi người
        var posts = await postService.GetVisiblePostsAsync(userId, page, pageSize);

        // Nếu là AJAX request, trả về Partial View
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var partialViewModel = new PostListViewModel
            {
                Posts = posts.Select(p => new PostViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Content = p.Content,
                    AuthorName = p.User.Username,
                    AuthorAvatar = p.User.UserProfile?.AvatarUrl,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    UpvoteCount = p.Votes.Count(v => v.VoteType),
                    DownvoteCount = p.Votes.Count(v => !v.VoteType),
                    CommentCount = p.Comments.Count,
                    IsDeleted = p.IsDeleted,
                    IsVisible = p.IsVisible,
                    VisibleToRoles = p.VisibleToRoles,
                    IsOwner = p.UserId == userId,
                    CanDelete = p.UserId == userId,
                    Attachments = p.Attachments.Select(a => new AttachmentViewModel
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        FilePath = a.FilePath,
                        FileSize = a.FileSize,
                        UploadedAt = a.UploadedAt
                    }).ToList()
                }).ToList(),
                CurrentPage = page,
                PageSize = pageSize,
                HasMorePosts = posts.Count() == pageSize,
                ViewType = "home"
            };

            return PartialView("_PostListPartial", partialViewModel);
        }

        var viewModel = new PostListViewModel
        {
            Posts = posts.Select(p => new PostViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Content = p.Content,
                AuthorName = p.User.Username,
                AuthorAvatar = p.User.UserProfile?.AvatarUrl,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                UpvoteCount = p.Votes.Count(v => v.VoteType),
                DownvoteCount = p.Votes.Count(v => !v.VoteType),
                CommentCount = p.Comments.Count,
                IsDeleted = p.IsDeleted,
                IsVisible = p.IsVisible,
                VisibleToRoles = p.VisibleToRoles,
                IsOwner = p.UserId == userId,
                CanDelete = p.UserId == userId,
                Attachments = p.Attachments.Select(a => new AttachmentViewModel
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    FileSize = a.FileSize,
                    UploadedAt = a.UploadedAt
                }).ToList()
            }).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            HasMorePosts = posts.Count() == pageSize,
            ViewType = "home"
        };

        return View(viewModel);
    }

    /// <summary>
    /// GET: Feeds/Following
    /// Hiển thị danh sách bài đăng từ những người đã follow
    /// </summary>
    /// <param name="page">Số trang (bắt đầu từ 1)</param>
    /// <param name="pageSize">Số bài đăng mỗi trang</param>
    /// <returns>View danh sách bài đăng</returns>
    public async Task<IActionResult> Following(int page = 1, int pageSize = 10)  // Đổi từ 20 thành 10
    {
        var userId = GetCurrentUserId();

        // Gọi service để lấy bài đăng từ những người đã follow
        var posts = await postService.GetFollowingPostsAsync(userId, page, pageSize);

        // Nếu là AJAX request, trả về Partial View
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var partialViewModel = new PostListViewModel
            {
                Posts = posts.Select(p => new PostViewModel
                {
                    Id = p.Id,
                    Title = p.Title,
                    Content = p.Content,
                    AuthorName = p.User.Username,
                    AuthorAvatar = p.User.UserProfile?.AvatarUrl,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    UpvoteCount = p.Votes.Count(v => v.VoteType),
                    DownvoteCount = p.Votes.Count(v => !v.VoteType),
                    CommentCount = p.Comments.Count,
                    IsDeleted = p.IsDeleted,
                    IsVisible = p.IsVisible,
                    VisibleToRoles = p.VisibleToRoles,
                    IsOwner = p.UserId == userId,
                    CanDelete = p.UserId == userId,
                    Attachments = p.Attachments.Select(a => new AttachmentViewModel
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        FilePath = a.FilePath,
                        FileSize = a.FileSize,
                        UploadedAt = a.UploadedAt
                    }).ToList()
                }).ToList(),
                CurrentPage = page,
                PageSize = pageSize,
                HasMorePosts = posts.Count() == pageSize,
                ViewType = "following"
            };

            return PartialView("_PostListPartial", partialViewModel);
        }

        var viewModel = new PostListViewModel
        {
            Posts = posts.Select(p => new PostViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Content = p.Content,
                AuthorName = p.User.Username,
                AuthorAvatar = p.User.UserProfile?.AvatarUrl,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                UpvoteCount = p.Votes.Count(v => v.VoteType),
                DownvoteCount = p.Votes.Count(v => !v.VoteType),
                CommentCount = p.Comments.Count,
                IsDeleted = p.IsDeleted,
                IsVisible = p.IsVisible,
                VisibleToRoles = p.VisibleToRoles,
                IsOwner = p.UserId == userId,
                CanDelete = p.UserId == userId,
                Attachments = p.Attachments.Select(a => new AttachmentViewModel
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    FileSize = a.FileSize,
                    UploadedAt = a.UploadedAt
                }).ToList()
            }).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            HasMorePosts = posts.Count() == pageSize,
            ViewType = "following"
        };

        return View("Home", viewModel);
    }
    /// <summary>
    /// POST: Feeds/Vote
    /// Vote bài đăng (upvote hoặc downvote) - AJAX endpoint
    /// </summary>
    /// <param name="postId">ID của bài đăng</param>
    /// <param name="isUpvote">True nếu upvote, False nếu downvote</param>
    /// <returns>JSON response với số lượng vote</returns>
    [HttpPost]
    public async Task<IActionResult> Vote(Guid postId, bool isUpvote)
    {
        var userId = GetCurrentUserId();
        var success = await postService.VotePostAsync(userId, postId, isUpvote);

        if (!success)
        {
            return Json(new { success = false, message = "Không thể vote bài đăng này." });
        }

        // Lấy lại số lượng vote mới nhất
        var post = await db.Posts
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.Id == postId);

        return Json(new
        {
            success = true,
            upvoteCount = post?.Votes.Count(v => v.VoteType) ?? 0,
            downvoteCount = post?.Votes.Count(v => !v.VoteType) ?? 0
        });
    }

    /// <summary>
    /// Test Lấy IP
    /// </summary>
    public IActionResult GetIp()
    {
        var info = trustedService.GetDeviceInfoAsync(HttpContext);
        var ip = trustedService.GetDeviceIpAsync(HttpContext);
        TempData["Ip + Info"] = ip + " | " + info;
        return View("Home");
    }
}