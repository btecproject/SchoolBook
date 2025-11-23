using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels;
using System.Security.Claims;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class FeedsController : Controller
{
    private readonly TrustedService _trustedService;
    private readonly AppDbContext _context;

    public FeedsController(TrustedService trustedService, AppDbContext context)
    {
        _trustedService = trustedService;
        _context = context;
    }

    // FEED HOME — hiển thị danh sách bài viết với đầy đủ thông tin
    public async Task<IActionResult> Home()
    {
        var currentUserId = GetCurrentUserId();
        
        var posts = await _context.Posts
            .Include(p => p.User)
            .Include(p => p.Comments)
                .ThenInclude(c => c.User)
            .Include(p => p.Attachments)
            .Include(p => p.Votes)
            .Include(p => p.Reports)
            .Where(p => !p.IsDeleted && p.IsVisible)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var postViewModels = posts.Select(p => MapToViewModel(p, currentUserId)).ToList();

        return View(postViewModels);
    }

    private PostViewModel MapToViewModel(Post post, Guid currentUserId)
    {
        var viewModel = new PostViewModel
        {
            Id = post.Id,
            UserId = post.UserId,
            Title = post.Title ?? "[No title]",
            Content = post.Content,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            IsDeleted = post.IsDeleted,
            IsVisible = post.IsVisible,
            VisibleToRoles = post.VisibleToRoles,
            
            // User information
            UserName = post.User?.Username ?? "Unknown",
            UserEmail = post.User?.Email,
            
            // Statistics
            UpvoteCount = post.Votes?.Count(v => v.VoteType) ?? 0,
            DownvoteCount = post.Votes?.Count(v => !v.VoteType) ?? 0,
            CommentCount = post.Comments?.Count ?? 0,
            AttachmentCount = post.Attachments?.Count ?? 0,
            ReportCount = post.Reports?.Count ?? 0,
            
            // Current user's interaction
            CurrentUserVote = post.Votes?.FirstOrDefault(v => v.UserId == currentUserId)?.VoteType,
            CanEdit = post.UserId == currentUserId || User.IsInRole("Admin") || User.IsInRole("Teacher"),
            CanDelete = post.UserId == currentUserId || User.IsInRole("Admin"),
            CanReport = post.UserId != currentUserId, // Không thể report bài của chính mình
            
            // Collections
            Attachments = post.Attachments?.Select(a => new PostAttachmentViewModel
            {
                Id = a.Id,
                FileName = a.FileName,
                FilePath = a.FilePath,
                FileSize = a.FileSize,
                FileSizeFormatted = FormatFileSize(a.FileSize),
                UploadedAt = a.UploadedAt,
                FileType = GetFileType(a.FileName)
            }).ToList() ?? new List<PostAttachmentViewModel>(),
            
            Comments = post.Comments?
                .Where(c => c.ParentCommentId == null) // Chỉ lấy comment gốc
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => MapCommentToViewModel(c, post.Comments?.ToList() ?? new List<PostComment>(), currentUserId))
                .ToList() ?? new List<PostCommentViewModel>(),
            
            RecentVotes = post.Votes?
                .OrderByDescending(v => v.VotedAt)
                .Take(10) // Lấy 10 vote gần nhất
                .Select(v => new PostVoteViewModel
                {
                    UserId = v.UserId,
                    UserName = v.User?.Username ?? "Unknown",
                    VoteType = v.VoteType,
                    VotedAt = v.VotedAt
                })
                .ToList() ?? new List<PostVoteViewModel>(),
            
            Reports = post.Reports?
                .Select(r => new PostReportViewModel
                {
                    Id = r.Id,
                    ReportedBy = r.ReportedBy,
                    ReportedByName = r.ReportedByUser?.Username ?? "Unknown",
                    Reason = r.Reason,
                    Status = r.Status,
                    ReviewedBy = r.ReviewedBy,
                    ReviewedByName = r.ReviewedByUser?.Username,
                    ReviewedAt = r.ReviewedAt,
                    CreatedAt = r.CreatedAt
                })
                .ToList() ?? new List<PostReportViewModel>()
        };

        return viewModel;
    }

    private PostCommentViewModel MapCommentToViewModel(PostComment comment, List<PostComment> allComments, Guid currentUserId)
    {
        var viewModel = new PostCommentViewModel
        {
            Id = comment.Id,
            PostId = comment.PostId,
            UserId = comment.UserId,
            UserName = comment.User?.Username ?? "Unknown",
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            ParentCommentId = comment.ParentCommentId,
            ParentCommentUserName = comment.ParentComment?.User?.Username,
            ReplyCount = allComments.Count(c => c.ParentCommentId == comment.Id),
            CanEdit = comment.UserId == currentUserId || User.IsInRole("Admin") || User.IsInRole("Teacher"),
            CanDelete = comment.UserId == currentUserId || User.IsInRole("Admin"),
            Replies = allComments
                .Where(c => c.ParentCommentId == comment.Id)
                .OrderBy(c => c.CreatedAt)
                .Select(c => MapCommentToViewModel(c, allComments, currentUserId))
                .ToList()
        };

        return viewModel;
    }

    private string FormatFileSize(int fileSize)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double len = fileSize;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private string GetFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLower();
        return extension switch
        {
            ".pdf" => "PDF",
            ".doc" or ".docx" => "Word",
            ".xls" or ".xlsx" => "Excel",
            ".ppt" or ".pptx" => "PowerPoint",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "Image",
            ".mp4" or ".avi" or ".mov" or ".wmv" => "Video",
            ".mp3" or ".wav" or ".wma" => "Audio",
            ".zip" or ".rar" => "Archive",
            _ => "File"
        };
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out Guid userId))
        {
            return userId;
        }
        return Guid.Empty;
    }

    /// <summary>
    /// Test lấy IP
    /// </summary>
    public IActionResult GetIp()
    {
        var info = _trustedService.GetDeviceInfoAsync(HttpContext);
        var ip = _trustedService.GetDeviceIpAsync(HttpContext);

        TempData["Ip + Info"] = ip + " | " + info;

        return RedirectToAction(nameof(Home));
    }
}