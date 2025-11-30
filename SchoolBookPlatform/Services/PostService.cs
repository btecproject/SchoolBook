using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services;

/// <summary>
/// Service xử lý logic nghiệp vụ cho chức năng bài đăng
/// Bao gồm: tạo, xóa, xem, vote, comment, report với phân quyền đầy đủ
/// </summary>
public class PostService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PostService> _logger;
    private readonly IWebHostEnvironment _environment;

    public PostService(AppDbContext db, ILogger<PostService> logger, IWebHostEnvironment environment)
    {
        _db = db;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Kiểm tra user có quyền xem bài đăng không
    /// - HighAdmin/Admin/Moderator: Xem tất cả (kể cả deleted, hidden)
    /// - Owner: Luôn xem được bài của mình
    /// - Teacher/Student: Chỉ xem bài visible, không deleted, và có quyền theo VisibleToRoles
    /// </summary>
    /// <param name="userId">ID của user cần kiểm tra</param>
    /// <param name="postId">ID của bài đăng</param>
    /// <returns>True nếu có quyền xem, False nếu không</returns>
    public async Task<bool> CanViewPostAsync(Guid userId, Guid postId)
    {
        var userRoles = await _db.GetUserRolesAsync(userId);
        
        // HighAdmin, Admin, Moderator xem được tất cả (kể cả deleted, hidden)
        if (userRoles.Contains("HighAdmin") || 
            userRoles.Contains("Admin") || 
            userRoles.Contains("Moderator"))
        {
            return true;
        }

        var post = await _db.Posts
            .FirstOrDefaultAsync(p => p.Id == postId);

        if (post == null) return false;

        // Owner luôn xem được bài của mình
        if (post.UserId == userId) return true;

        // Kiểm tra visibility: bài bị xóa hoặc không visible → không được xem
        if (post.IsDeleted || !post.IsVisible) return false;

        // Kiểm tra VisibleToRoles
        // Nếu null hoặc "All" → ai cũng xem được
        if (string.IsNullOrEmpty(post.VisibleToRoles) || post.VisibleToRoles == "All")
        {
            return true;
        }

        // Check role của user có match với VisibleToRoles không
        return userRoles.Contains(post.VisibleToRoles);
    }

    /// <summary>
    /// Lấy danh sách bài đăng user có quyền xem (có phân trang)
    /// - HighAdmin/Admin/Moderator: Xem tất cả
    /// - Teacher/Student: Chỉ xem bài visible, không deleted, và có quyền
    /// </summary>
    /// <param name="userId">ID của user</param>
    /// <param name="page">Số trang (bắt đầu từ 1)</param>
    /// <param name="pageSize">Số bài đăng mỗi trang</param>
    /// <returns>Danh sách bài đăng</returns>
    public async Task<List<Post>> GetVisiblePostsAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        var userRoles = await _db.GetUserRolesAsync(userId);
        var isAdmin = userRoles.Contains("HighAdmin") || 
                     userRoles.Contains("Admin") || 
                     userRoles.Contains("Moderator");

        var query = _db.Posts.AsQueryable();

        if (!isAdmin)
        {
            // Teacher/Student: Chỉ xem bài visible, không deleted, và có quyền
            query = query.Where(p => 
                p.IsVisible && 
                !p.IsDeleted && 
                (p.UserId == userId || // Bài của mình
                 p.VisibleToRoles == null || 
                 p.VisibleToRoles == "All" || 
                 userRoles.Contains(p.VisibleToRoles))
            );
        }
        // Admin/Moderator: Xem tất cả (không filter)

        return await query
            .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
            .Include(p => p.Votes)
            .Include(p => p.Comments)
            .Include(p => p.Attachments)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    /// Tạo bài đăng mới
    /// </summary>
    /// <param name="userId">ID của user tạo bài</param>
    /// <param name="title">Tiêu đề bài đăng</param>
    /// <param name="content">Nội dung bài đăng</param>
    /// <param name="visibleToRoles">Nhóm người xem: 'Student', 'Teacher', 'Admin', 'All' (mặc định: 'All')</param>
    /// <param name="files">Danh sách file đính kèm (optional)</param>
    /// <returns>Post object nếu thành công, null nếu thất bại</returns>
    public async Task<Post?> CreatePostAsync(Guid userId, string title, string content, string? visibleToRoles = "All", IEnumerable<IFormFile>? files = null)
    {
        // Validate visibleToRoles
        var validRoles = new[] { "Student", "Teacher", "Admin", "All" };
        if (!string.IsNullOrEmpty(visibleToRoles) && !validRoles.Contains(visibleToRoles))
        {
            visibleToRoles = "All"; // Mặc định là "All" nếu không hợp lệ
        }

        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Content = content,
            VisibleToRoles = visibleToRoles,
            CreatedAt = DateTime.UtcNow
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        // Xử lý upload file nếu có
        if (files != null && files.Any())
        {
            await SaveAttachmentsAsync(post.Id, files);
        }

        return post;
    }

    /// <summary>
    /// Cập nhật bài đăng
    /// Chỉ owner hoặc Admin/Moderator mới có quyền sửa
    /// </summary>
    /// <param name="editorId">ID của user thực hiện sửa</param>
    /// <param name="postId">ID của bài đăng cần sửa</param>
    /// <param name="title">Tiêu đề mới</param>
    /// <param name="content">Nội dung mới</param>
    /// <param name="visibleToRoles">Nhóm người xem mới</param>
    /// <param name="files">Danh sách file mới (optional)</param>
    /// <param name="attachmentIdsToDelete">Danh sách ID file cần xóa (optional)</param>
    /// <returns>Post object nếu thành công, null nếu thất bại</returns>
    public async Task<Post?> UpdatePostAsync(
        Guid editorId, 
        Guid postId, 
        string title, 
        string content, 
        string? visibleToRoles = "All",
        IEnumerable<IFormFile>? files = null,
        IEnumerable<Guid>? attachmentIdsToDelete = null)
    {
        var post = await _db.Posts
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == postId);

        if (post == null) return null;

        var userRoles = await _db.GetUserRolesAsync(editorId);
        var isAdmin = userRoles.Contains("HighAdmin") || 
                     userRoles.Contains("Admin") || 
                     userRoles.Contains("Moderator");

        // Kiểm tra quyền: chỉ owner hoặc admin/mod mới được sửa
        if (post.UserId != editorId && !isAdmin)
        {
            return null;
        }

        // Validate visibleToRoles
        var validRoles = new[] { "Student", "Teacher", "Admin", "All" };
        if (!string.IsNullOrEmpty(visibleToRoles) && !validRoles.Contains(visibleToRoles))
        {
            visibleToRoles = "All";
        }

        // Cập nhật thông tin bài đăng
        post.Title = title;
        post.Content = content;
        post.VisibleToRoles = visibleToRoles;
        post.UpdatedAt = DateTime.UtcNow;

        // Xóa các file được đánh dấu xóa
        if (attachmentIdsToDelete != null && attachmentIdsToDelete.Any())
        {
            await RemoveAttachmentsAsync(postId, attachmentIdsToDelete);
        }

        // Thêm file mới
        if (files != null && files.Any())
        {
            await SaveAttachmentsAsync(postId, files);
        }

        await _db.SaveChangesAsync();
        return post;
    }

    /// <summary>
    /// Xóa bài đăng
    /// - HighAdmin: Có thể hard delete (xóa vĩnh viễn)
    /// - User thường: Chỉ xóa được bài của mình (soft delete)
    /// </summary>
    /// <param name="userId">ID của user thực hiện xóa</param>
    /// <param name="postId">ID của bài đăng cần xóa</param>
    /// <param name="hardDelete">True nếu muốn hard delete (chỉ HighAdmin), False nếu soft delete</param>
    /// <returns>True nếu thành công, False nếu thất bại</returns>
    public async Task<bool> DeletePostAsync(Guid userId, Guid postId, bool hardDelete = false)
    {
        var post = await _db.Posts.FindAsync(postId);
        if (post == null) return false;

        var userRoles = await _db.GetUserRolesAsync(userId);

        // HighAdmin có thể hard delete
        if (hardDelete && userRoles.Contains("HighAdmin"))
        {
            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();
            return true;
        }

        // User chỉ xóa được bài của mình (soft delete)
        if (post.UserId != userId)
        {
            return false;
        }

        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Moderator/Admin soft delete bài đăng không phù hợp
    /// </summary>
    /// <param name="moderatorId">ID của moderator/admin thực hiện xóa</param>
    /// <param name="postId">ID của bài đăng cần xóa</param>
    /// <param name="reason">Lý do xóa</param>
    /// <returns>True nếu thành công, False nếu thất bại</returns>
    public async Task<bool> ModeratorDeletePostAsync(Guid moderatorId, Guid postId, string reason)
    {
        var userRoles = await _db.GetUserRolesAsync(moderatorId);
        
        // Chỉ Moderator, Admin, HighAdmin mới có quyền
        if (!userRoles.Contains("Moderator") && 
            !userRoles.Contains("Admin") && 
            !userRoles.Contains("HighAdmin"))
        {
            return false;
        }

        var post = await _db.Posts.FindAsync(postId);
        if (post == null) return false;

        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;
        // Note: Có thể thêm DeletedBy, DeletedAt, DeletedReason vào Post model nếu cần tracking chi tiết

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Tạo comment cho bài đăng
    /// </summary>
    /// <param name="userId">ID của user tạo comment</param>
    /// <param name="postId">ID của bài đăng</param>
    /// <param name="content">Nội dung comment</param>
    /// <param name="parentCommentId">ID của comment cha (nếu là reply), null nếu là comment gốc</param>
    /// <returns>PostComment object nếu thành công, null nếu thất bại</returns>
    public async Task<PostComment?> CreateCommentAsync(Guid userId, Guid postId, string content, Guid? parentCommentId = null)
    {
        // Kiểm tra quyền xem bài (phải xem được mới comment được)
        if (!await CanViewPostAsync(userId, postId))
        {
            return null;
        }

        var comment = new PostComment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            UserId = userId,
            Content = content,
            ParentCommentId = parentCommentId,
            CreatedAt = DateTime.UtcNow
        };

        _db.PostComments.Add(comment);
        await _db.SaveChangesAsync();

        // Load lại với navigation properties
        return await _db.PostComments
            .Include(c => c.User)
                .ThenInclude(u => u.UserProfile)
            .FirstOrDefaultAsync(c => c.Id == comment.Id);
    }

    /// <summary>
    /// Vote bài đăng (upvote hoặc downvote)
    /// Nếu đã vote rồi và vote cùng loại → xóa vote (toggle)
    /// Nếu đã vote rồi nhưng vote khác loại → đổi vote
    /// </summary>
    /// <param name="userId">ID của user vote</param>
    /// <param name="postId">ID của bài đăng</param>
    /// <param name="isUpvote">True nếu upvote, False nếu downvote</param>
    /// <returns>True nếu thành công, False nếu thất bại</returns>
    public async Task<bool> VotePostAsync(Guid userId, Guid postId, bool isUpvote)
    {
        // Kiểm tra quyền xem bài (phải xem được mới vote được)
        if (!await CanViewPostAsync(userId, postId))
        {
            return false;
        }

        var existingVote = await _db.PostVotes
            .FirstOrDefaultAsync(v => v.PostId == postId && v.UserId == userId);

        if (existingVote != null)
        {
            // Nếu vote cùng loại → xóa vote (toggle)
            if (existingVote.VoteType == isUpvote)
            {
                _db.PostVotes.Remove(existingVote);
            }
            else
            {
                // Đổi vote
                existingVote.VoteType = isUpvote;
                existingVote.VotedAt = DateTime.UtcNow;
            }
        }
        else
        {
            // Tạo vote mới
            var vote = new PostVote
            {
                PostId = postId,
                UserId = userId,
                VoteType = isUpvote,
                VotedAt = DateTime.UtcNow
            };
            _db.PostVotes.Add(vote);
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Tạo báo cáo về bài đăng
    /// </summary>
    /// <param name="userId">ID của user tạo báo cáo</param>
    /// <param name="postId">ID của bài đăng bị báo cáo</param>
    /// <param name="reason">Lý do báo cáo</param>
    /// <returns>PostReport object nếu thành công, null nếu thất bại</returns>
    public async Task<PostReport?> CreateReportAsync(Guid userId, Guid postId, string reason)
    {
        var report = new PostReport
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            ReportedBy = userId,
            Reason = reason,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.PostReports.Add(report);
        await _db.SaveChangesAsync();

        return report;
    }

    /// <summary>
    /// Admin review báo cáo (approve hoặc reject)
    /// Nếu approve và có actionTaken, có thể tự động soft delete bài đăng
    /// </summary>
    /// <param name="adminId">ID của admin thực hiện review</param>
    /// <param name="reportId">ID của báo cáo</param>
    /// <param name="status">Trạng thái: 'Approved' hoặc 'Rejected'</param>
    /// <param name="actionTaken">Mô tả action đã thực hiện (nếu approve và muốn xóa bài)</param>
    /// <returns>True nếu thành công, False nếu thất bại</returns>
    public async Task<bool> ReviewReportAsync(Guid adminId, Guid reportId, string status, string? actionTaken = null)
    {
        var userRoles = await _db.GetUserRolesAsync(adminId);
        
        // Chỉ Admin và HighAdmin mới có quyền review
        if (!userRoles.Contains("Admin") && !userRoles.Contains("HighAdmin"))
        {
            return false;
        }

        // Validate status
        if (status != "Approved" && status != "Rejected")
        {
            return false;
        }

        var report = await _db.PostReports
            .Include(r => r.Post)
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report == null) return false;

        report.Status = status;
        report.ReviewedBy = adminId;
        report.ReviewedAt = DateTime.UtcNow;

        // Nếu approve và có action, có thể soft delete post
        if (status == "Approved" && !string.IsNullOrEmpty(actionTaken))
        {
            report.Post.IsDeleted = true;
            report.Post.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Lấy thống kê về bài đăng (dùng cho Admin)
    /// </summary>
    /// <returns>PostStatistics object chứa các thống kê</returns>
    public async Task<PostStatistics> GetStatisticsAsync()
    {
        var totalPosts = await _db.Posts.CountAsync();
        var deletedPosts = await _db.Posts.CountAsync(p => p.IsDeleted);
        var hiddenPosts = await _db.Posts.CountAsync(p => !p.IsVisible);
        var activePosts = await _db.Posts.CountAsync(p => !p.IsDeleted && p.IsVisible);
        var totalReports = await _db.PostReports.CountAsync();
        var pendingReports = await _db.PostReports.CountAsync(r => r.Status == "Pending");

        return new PostStatistics
        {
            TotalPosts = totalPosts,
            DeletedPosts = deletedPosts,
            HiddenPosts = hiddenPosts,
            ActivePosts = activePosts,
            TotalReports = totalReports,
            PendingReports = pendingReports
        };
    }

    /// <summary>
    /// Lưu file đính kèm vào thư mục và database
    /// </summary>
    /// <param name="postId">ID của bài đăng</param>
    /// <param name="files">Danh sách file cần lưu</param>
    /// <returns>Task</returns>
    public async Task SaveAttachmentsAsync(Guid postId, IEnumerable<IFormFile> files)
    {
        const long maxFileSize = 10 * 1024 * 1024; // 10MB
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4", ".mov", ".avi" };
        var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "posts", postId.ToString());

        // Tạo thư mục nếu chưa tồn tại
        if (!Directory.Exists(uploadsPath))
        {
            Directory.CreateDirectory(uploadsPath);
        }

        foreach (var file in files)
        {
            if (file == null || file.Length == 0) continue;

            // Kiểm tra kích thước file
            if (file.Length > maxFileSize)
            {
                _logger.LogWarning("File {FileName} vượt quá kích thước cho phép (10MB)", file.FileName);
                continue;
            }

            // Kiểm tra extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("File {FileName} có định dạng không được phép", file.FileName);
                continue;
            }

            // Tạo tên file unique
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsPath, uniqueFileName);

            // Lưu file vào disk
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Lưu thông tin vào database
                var attachment = new PostAttachment
                {
                    Id = Guid.NewGuid(),
                    PostId = postId,
                    FileName = file.FileName,
                    FilePath = $"/uploads/posts/{postId}/{uniqueFileName}",
                    FileSize = (int)file.Length,
                    UploadedAt = DateTime.UtcNow
                };

                _db.PostAttachments.Add(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu file {FileName}", file.FileName);
                // Nếu lưu file thất bại, xóa file đã tạo (nếu có)
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Xóa file đính kèm (xóa cả file trên disk và record trong database)
    /// </summary>
    /// <param name="postId">ID của bài đăng</param>
    /// <param name="attachmentIds">Danh sách ID file cần xóa</param>
    /// <returns>Task</returns>
    public async Task RemoveAttachmentsAsync(Guid postId, IEnumerable<Guid> attachmentIds)
    {
        var attachments = await _db.PostAttachments
            .Where(a => a.PostId == postId && attachmentIds.Contains(a.Id))
            .ToListAsync();

        foreach (var attachment in attachments)
        {
            // Xóa file trên disk
            var filePath = Path.Combine(_environment.WebRootPath, attachment.FilePath.TrimStart('/'));
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xóa file {FilePath}", filePath);
                }
            }

            // Xóa record trong database
            _db.PostAttachments.Remove(attachment);
        }

        await _db.SaveChangesAsync();
    }
}

