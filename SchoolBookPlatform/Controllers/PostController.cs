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
/// Bao gồm: xem, tạo, xóa, vote, comment, report
/// </summary>
[Authorize]
[Route("[controller]")]
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

    /// <summary>
    /// GET: Post/Details/{id}
    /// Hiển thị chi tiết bài đăng và danh sách comment
    /// </summary>
    /// <param name="id">ID của bài đăng</param>
    /// <returns>View chi tiết bài đăng</returns>
    [HttpGet("Details/{id}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetCurrentUserId();
        
        // Kiểm tra quyền xem bài đăng
        if (!await postService.CanViewPostAsync(userId, id))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xem bài đăng này.";
            return RedirectToAction("Home", "Feeds");
        }

        var post = await db.Posts
            .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
            .Include(p => p.Comments)
                .ThenInclude(c => c.User)
                    .ThenInclude(u => u.UserProfile)
            .Include(p => p.Votes)
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post == null)
        {
            return NotFound();
        }

        // Kiểm tra quyền xóa (Owner hoặc Admin/Moderator)
        var userRoles = await db.GetUserRolesAsync(userId);
        var isAdmin = userRoles.Contains("HighAdmin") || 
                     userRoles.Contains("Admin") || 
                     userRoles.Contains("Moderator");

        var viewModel = new PostDetailsViewModel
        {
            Post = new PostViewModel
            {
                Id = post.Id,
                Title = post.Title,
                Content = post.Content,
                AuthorName = post.User.Username,
                AuthorAvatar = post.User.UserProfile?.AvatarUrl,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                UpvoteCount = post.Votes.Count(v => v.VoteType),
                DownvoteCount = post.Votes.Count(v => !v.VoteType),
                CommentCount = post.Comments.Count,
                IsDeleted = post.IsDeleted,
                IsVisible = post.IsVisible,
                VisibleToRoles = post.VisibleToRoles,
                IsOwner = post.UserId == userId,
                CanDelete = post.UserId == userId || isAdmin,
                Attachments = post.Attachments.Select(a => new AttachmentViewModel
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    FileSize = a.FileSize,
                    UploadedAt = a.UploadedAt
                }).ToList()
            },
            Comments = post.Comments
                .Where(c => c.ParentCommentId == null) // Chỉ lấy comment gốc
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CommentViewModel
                {
                    Id = c.Id,
                    Content = c.Content,
                    AuthorName = c.User.Username,
                    AuthorAvatar = c.User.UserProfile?.AvatarUrl,
                    CreatedAt = c.CreatedAt,
                    Replies = c.Replies
                        .OrderBy(r => r.CreatedAt)
                        .Select(r => new CommentViewModel
                        {
                            Id = r.Id,
                            Content = r.Content,
                            AuthorName = r.User.Username,
                            AuthorAvatar = r.User.UserProfile?.AvatarUrl,
                            CreatedAt = r.CreatedAt
                        }).ToList()
                }).ToList()
        };

        return View(viewModel);
    }

    /// <summary>
    /// GET: Post/Create
    /// Hiển thị form tạo bài đăng mới
    /// </summary>
    /// <returns>View form tạo bài đăng</returns>
    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View(new CreatePostViewModel());
    }

    /// <summary>
    /// POST: Post/Create
    /// Xử lý tạo bài đăng mới
    /// </summary>
    /// <param name="model">Dữ liệu từ form</param>
    /// <returns>Redirect về Home feed nếu thành công, trả về View với lỗi nếu thất bại</returns>
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePostViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = GetCurrentUserId();
        var userRoles = await db.GetUserRolesAsync(userId);
        var isModerator = userRoles.Contains("HighAdmin") || userRoles.Contains("Moderator");

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

        // Thông báo khác nhau cho Moderator và user thường
        if (isModerator)
        {
            TempData["SuccessMessage"] = "Đăng bài thành công!";
        }
        else
        {
            TempData["SuccessMessage"] = "Bài đăng của bạn đã được gửi và đang chờ phê duyệt. Bài đăng sẽ hiển thị sau khi được Admin duyệt.";
        }
        return RedirectToAction("Home", "Feeds");
    }

    /// <summary>
    /// GET: Post/Edit/{id}
    /// Hiển thị form sửa bài đăng
    /// </summary>
    /// <param name="id">ID của bài đăng cần sửa</param>
    /// <returns>View form sửa bài đăng</returns>
    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetCurrentUserId();
        var post = await db.Posts
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post == null)
        {
            return NotFound();
        }

        var userRoles = await db.GetUserRolesAsync(userId);
        var isAdmin = userRoles.Contains("HighAdmin") || 
                     userRoles.Contains("Admin") || 
                     userRoles.Contains("Moderator");

        // Kiểm tra quyền: chỉ owner hoặc admin/mod mới được sửa
        if (post.UserId != userId && !isAdmin)
        {
            TempData["ErrorMessage"] = "Bạn không có quyền sửa bài đăng này.";
            return RedirectToAction("Details", new { id });
        }

        var viewModel = new EditPostViewModel
        {
            Id = post.Id,
            Title = post.Title,
            Content = post.Content,
            VisibleToRoles = post.VisibleToRoles ?? "All",
            ExistingAttachments = post.Attachments.Select(a => new AttachmentViewModel
            {
                Id = a.Id,
                FileName = a.FileName,
                FilePath = a.FilePath,
                FileSize = a.FileSize,
                UploadedAt = a.UploadedAt
            }).ToList()
        };

        return View(viewModel);
    }

    /// <summary>
    /// POST: Post/Edit/{id}
    /// Xử lý sửa bài đăng
    /// </summary>
    /// <param name="id">ID của bài đăng cần sửa</param>
    /// <param name="model">Dữ liệu từ form</param>
    /// <returns>Redirect về Details nếu thành công, trả về View với lỗi nếu thất bại</returns>
    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditPostViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            // Load lại existing attachments nếu có lỗi
            var post = await db.Posts
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (post != null)
            {
                model.ExistingAttachments = post.Attachments.Select(a => new AttachmentViewModel
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    FileSize = a.FileSize,
                    UploadedAt = a.UploadedAt
                }).ToList();
            }
            
            return View(model);
        }

        var userId = GetCurrentUserId();
        var postUpdated = await postService.UpdatePostAsync(
            userId,
            id,
            model.Title,
            model.Content,
            model.VisibleToRoles,
            model.Files,
            model.AttachmentIdsToDelete);

        if (postUpdated == null)
        {
            ModelState.AddModelError("", "Không thể sửa bài đăng này. Bạn có thể không có quyền hoặc bài đăng không tồn tại.");
            
            // Load lại existing attachments
            var post = await db.Posts
                .Include(p => p.Attachments)
                .FirstOrDefaultAsync(p => p.Id == id);
            
            if (post != null)
            {
                model.ExistingAttachments = post.Attachments.Select(a => new AttachmentViewModel
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    FileSize = a.FileSize,
                    UploadedAt = a.UploadedAt
                }).ToList();
            }
            
            return View(model);
        }

        TempData["SuccessMessage"] = "Sửa bài đăng thành công!";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// POST: Post/Delete/{id}
    /// Xóa bài đăng (soft delete cho user thường, hard delete cho HighAdmin)
    /// </summary>
    /// <param name="id">ID của bài đăng cần xóa</param>
    /// <returns>Redirect về Home feed</returns>
    [HttpPost("Delete/{id}")]
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
            return RedirectToAction("Home", "Feeds");
        }

        TempData["SuccessMessage"] = isHighAdmin ? 
            "Đã xóa bài đăng và toàn bộ file đính kèm vĩnh viễn!" : 
            "Đã xóa bài đăng thành công!";
        return RedirectToAction("Home", "Feeds");
    }

    /// <summary>
    /// POST: Post/Comment
    /// Tạo comment mới cho bài đăng - AJAX endpoint
    /// </summary>
    /// <param name="postId">ID của bài đăng</param>
    /// <param name="content">Nội dung comment</param>
    /// <param name="parentCommentId">ID của comment cha (nếu là reply), null nếu là comment gốc</param>
    /// <returns>JSON response với thông tin comment mới</returns>
    [HttpPost("Comment")]
    public async Task<IActionResult> Comment(Guid postId, string content, Guid? parentCommentId = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Json(new { success = false, message = "Nội dung comment không được để trống." });
        }

        var userId = GetCurrentUserId();
        var comment = await postService.CreateCommentAsync(userId, postId, content, parentCommentId);

        if (comment == null)
        {
            return Json(new { success = false, message = "Không thể tạo comment." });
        }

        return Json(new
        {
            success = true,
            comment = new
            {
                id = comment.Id,
                content = comment.Content,
                authorName = comment.User.Username,
                authorAvatar = comment.User.UserProfile?.AvatarUrl,
                createdAt = comment.CreatedAt.ToString("g")
            }
        });
    }

    /// <summary>
    /// POST: Post/Report
    /// Tạo báo cáo về bài đăng
    /// </summary>
    /// <param name="postId">ID của bài đăng bị báo cáo</param>
    /// <param name="reason">Lý do báo cáo</param>
    /// <returns>Redirect về Details</returns>
    [HttpPost("Report")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(Guid postId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do báo cáo.";
            return RedirectToAction(nameof(Details), new { id = postId });
        }

        var userId = GetCurrentUserId();
        var report = await postService.CreateReportAsync(userId, postId, reason);

        if (report == null)
        {
            TempData["ErrorMessage"] = "Không thể tạo báo cáo.";
            return RedirectToAction(nameof(Details), new { id = postId });
        }

        TempData["SuccessMessage"] = "Báo cáo đã được gửi thành công!";
        return RedirectToAction(nameof(Details), new { id = postId });
    }

    /// <summary>
    /// GET: Post/ModeratorDelete/{id}
    /// Hiển thị form Moderator xóa bài đăng
    /// </summary>
    /// <param name="id">ID của bài đăng cần xóa</param>
    /// <returns>View form xóa</returns>
    [HttpGet("ModeratorDelete/{id}")]
    [Authorize(Policy = "ModeratorOrHigher")]
    public IActionResult ModeratorDelete(Guid id)
    {
        return View(new ModeratorDeleteViewModel { PostId = id });
    }

    /// <summary>
    /// POST: Post/ModeratorDelete/{id}
    /// Xử lý Moderator xóa bài đăng
    /// </summary>
    /// <param name="id">ID của bài đăng cần xóa</param>
    /// <param name="model">Dữ liệu từ form (lý do xóa)</param>
    /// <returns>Redirect về Home feed nếu thành công</returns>
    [HttpPost("ModeratorDelete/{id}")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ModeratorOrHigher")]
    public async Task<IActionResult> ModeratorDelete(Guid id, ModeratorDeleteViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var moderatorId = GetCurrentUserId();
        var success = await postService.ModeratorDeletePostAsync(moderatorId, id, model.Reason);

        if (!success)
        {
            TempData["ErrorMessage"] = "Không thể xóa bài đăng này.";
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã xóa bài đăng thành công!";
        return RedirectToAction("Home", "Feeds");
    }
    
    /// <summary>
    /// POST: Post/Vote
    /// Vote bài đăng (upvote hoặc downvote) - AJAX endpoint
    /// </summary>
    /// <param name="postId">ID của bài đăng</param>
    /// <param name="isUpvote">True nếu upvote, False nếu downvote</param>
    /// <returns>JSON response với số lượng vote</returns>
    [HttpPost("Vote")]
    public async Task<IActionResult> Vote(Guid postId, bool isUpvote)
    {
        var userId = GetCurrentUserId();
    
        // Gọi service để vote
        var success = await postService.VotePostAsync(userId, postId, isUpvote);

        if (!success)
        {
            return Json(new { success = false, message = "Không thể vote bài đăng này." });
        }

        // Lấy lại số lượng vote mới nhất
        var post = await db.Posts
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.Id == postId);

        var upvoteCount = post?.Votes.Count(v => v.VoteType) ?? 0;
        var downvoteCount = post?.Votes.Count(v => !v.VoteType) ?? 0;
    
        // Tính tổng điểm - QUAN TRỌNG: thêm totalScore và displayScore
        var totalScore = upvoteCount - downvoteCount;
        var displayScore = totalScore < 0 ? 0 : totalScore; // Không hiển thị số âm

        return Json(new
        {
            success = true,
            upvoteCount,
            downvoteCount,
            totalScore,        // Thêm để client dùng
            displayScore       // Thêm để client dùng
        });
    }
    
    /// <summary>
    /// GET: Post/GetUserVoteStatus/{postId}
    /// Lấy trạng thái vote của user cho bài đăng
    /// </summary>
    [HttpGet("GetUserVoteStatus/{postId}")]
    public async Task<IActionResult> GetUserVoteStatus(Guid postId)
    {
        var userId = GetCurrentUserId();
    
        if (userId == Guid.Empty)
        {
            return Json(new { 
                success = true, 
                hasVoted = false,
                voteType = (bool?)null
            });
        }
    
        var vote = await db.PostVotes
            .FirstOrDefaultAsync(v => v.PostId == postId && v.UserId == userId);
    
        return Json(new {
            success = true,
            hasVoted = vote != null,
            voteType = vote?.VoteType // true = upvote, false = downvote, null = chưa vote
        });
    }
}