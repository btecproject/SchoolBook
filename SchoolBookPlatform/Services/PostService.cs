using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace SchoolBookPlatform.Services;

/// <summary>
/// Service xử lý logic nghiệp vụ cho chức năng bài đăng
/// Bao gồm: tạo, xóa, xem, vote, comment, report với phân quyền đầy đủ
/// </summary>
public class PostService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PostService> _logger;
    private readonly Cloudinary _cloudinary;

    public PostService(AppDbContext db, ILogger<PostService> logger, Cloudinary cloudinary)
    {
        _db = db;
        _logger = logger;
        _cloudinary = cloudinary;

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
/// <summary>
/// Lấy danh sách bài đăng user có quyền xem (có phân trang)
/// - HighAdmin/Admin/Moderator: Xem tất cả
/// - Teacher/Student: Chỉ xem bài visible, không deleted, và có quyền
/// </summary>
// Trong file SRC/Services/PostService.cs

public async Task<IQueryable<Post>> GetVisiblePostsAsync(Guid userId, int page = 1, int pageSize = 20, 
    string sortBy = "newest", string filterRole = "All",
    string filterTime = "all", DateTime? startDate = null, DateTime? endDate = null)
{
    var userRoles = await _db.GetUserRolesAsync(userId);
    var isAdmin = userRoles.Contains("HighAdmin") || 
                  userRoles.Contains("Admin") || 
                  userRoles.Contains("Moderator");

    IQueryable<Post> query;
    
    if (isAdmin)
    {
        // Admin: xem tất cả bài đăng
        query = _db.Posts.AsQueryable();
    }
    else
    {
        // User thường: chỉ xem bài có quyền
        var myPosts = _db.Posts.Where(p => p.UserId == userId);
        
        var othersPosts = _db.Posts.Where(p => 
            p.UserId != userId && 
            p.IsVisible && 
            !p.IsDeleted &&
            (p.VisibleToRoles == null || 
             p.VisibleToRoles == "All" || 
             userRoles.Contains(p.VisibleToRoles)));
        
        // Kết hợp cả hai
        query = myPosts.Union(othersPosts);
    }
    //sort role
    if (filterRole != "All")
    {
        if (filterRole == "Admin")
        {
            // Nếu chọn "Admin": Lấy bài của Admin, HighAdmin và Moderator
            query = query.Where(p => p.User.UserRoles.Any(ur => 
                ur.Role.Name == "Admin" || 
                ur.Role.Name == "HighAdmin" || 
                ur.Role.Name == "Moderator"));
        }
        else
        {
            // Nếu chọn Student hoặc Teacher: Lấy đúng theo role đó
            query = query.Where(p => p.User.UserRoles.Any(ur => ur.Role.Name == filterRole));
        }
    }
    //sort time
    if (filterTime != "all")
    {
        var nowVn = DateTime.UtcNow.AddHours(7); // Giờ hiện tại VN
        DateTime fromDateUtc = DateTime.MinValue;
        DateTime toDateUtc = DateTime.MaxValue;

        switch (filterTime.ToLower())
        {
            case "today":
                // Từ 00:00 hôm nay (VN) -> đổi sang UTC (-7h)
                fromDateUtc = nowVn.Date.AddHours(-7);
                break;
            case "week":
                // 7 ngày gần nhất
                fromDateUtc = DateTime.UtcNow.AddDays(-7);
                break;
            case "month":
                // 30 ngày gần nhất
                fromDateUtc = DateTime.UtcNow.AddDays(-30);
                break;
            case "custom":
                if (startDate.HasValue)
                    fromDateUtc = startDate.Value.Date.AddHours(-7); // Bắt đầu ngày VN -> UTC

                if (endDate.HasValue)
                    // Hết ngày VN (23:59:59) -> UTC. 
                    // endDate.Value.Date là 0h, cộng 1 ngày là 0h hôm sau, trừ 1 tick
                    toDateUtc = endDate.Value.Date.AddDays(1).AddHours(-7).AddTicks(-1);
                break;
        }

        query = query.Where(p => p.CreatedAt >= fromDateUtc);

        // Chỉ áp dụng cận trên nếu là custom (để chặn ngày tương lai hoặc khoảng custom)
        if (filterTime.ToLower() == "custom" && endDate.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= toDateUtc);
        }
    }

    switch (sortBy.ToLower())
    {
        case "best":
            var bestQuery = query
                .Select(p => new
                {
                    Post = p,
                    BestScore = p.Votes.Count(v => v.VoteType) - p.Votes.Count(v => !v.VoteType)
                })
                .OrderByDescending(x => x.BestScore)
                .ThenByDescending(x => x.Post.CreatedAt)
                .Select(x => x.Post);
                
            return bestQuery
                .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
                .Include(p => p.Votes)
                .Include(p => p.Comments)
                .Include(p => p.Attachments)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
            
        case "hot":
            var hotQuery = query
                .Select(p => new
                {
                    Post = p,
                    HotScore = p.Votes.Count()
                })
                .OrderByDescending(x => x.HotScore)
                .ThenByDescending(x => x.Post.CreatedAt)
                .Select(x => x.Post);
                
            return hotQuery
                .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
                .Include(p => p.Votes)
                .Include(p => p.Comments)
                .Include(p => p.Attachments)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
        
        case "newest":
        default:
            var newestQuery = query
                .OrderByDescending(p => p.CreatedAt);
                
            return newestQuery
                .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
                .Include(p => p.Votes)
                .Include(p => p.Comments)
                .Include(p => p.Attachments)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
    }
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

        // Kiểm tra quyền user - HighAdmin/Moderator thì auto approve (IsVisible = true)
        var userRoles = await _db.GetUserRolesAsync(userId);
        var isModerator = userRoles.Contains("HighAdmin") || userRoles.Contains("Moderator")||userRoles.Contains("Admin");

        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Content = content,
            VisibleToRoles = visibleToRoles,
            CreatedAt = DateTime.UtcNow,
            // HighAdmin/Moderator: auto approve, user thường: chờ duyệt
            IsVisible = isModerator
        };

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();

        // Xử lý upload file lên Cloudinary nếu có
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
    /// Xóa bài đăng (cập nhật để xóa cả trên Cloudinary)
    /// - HighAdmin: Có thể hard delete (xóa vĩnh viễn)
    /// - User thường: Chỉ xóa được bài của mình (soft delete)
    /// </summary>
    /// <param name="userId">ID của user thực hiện xóa</param>
    /// <param name="postId">ID của bài đăng cần xóa</param>
    /// <param name="hardDelete">True nếu muốn hard delete (chỉ HighAdmin), False nếu soft delete</param>
    /// <returns>True nếu thành công, False nếu thất bại</returns>
    public async Task<bool> DeletePostAsync(Guid userId, Guid postId, bool hardDelete = false)
    {
        var post = await _db.Posts
            .Include(p => p.Attachments)
            .FirstOrDefaultAsync(p => p.Id == postId);
            
        if (post == null) return false;

        var userRoles = await _db.GetUserRolesAsync(userId);

        // HighAdmin có thể hard delete
        if (hardDelete && userRoles.Contains("HighAdmin"))
        {
            // Xóa toàn bộ thư mục trên Cloudinary trước
            await DeletePostFolderFromCloudinaryAsync(postId);
            
            // Xóa từ database
            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("Đã hard delete post {PostId} và xóa folder Cloudinary", postId);
            return true;
        }

        // User chỉ xóa được bài của mình (soft delete)
        if (post.UserId != userId)
        {
            return false;
        }

        // Soft delete - không xóa folder Cloudinary
        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Đã soft delete post {PostId}", postId);
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

    /// Lưu file đính kèm lên Cloudinary
    /// </summary>
    /// <param name="postId">ID của bài đăng</param>
    /// <param name="files">Danh sách file cần lưu</param>
    /// <returns>Task</returns>
    public async Task SaveAttachmentsAsync(Guid postId, IEnumerable<IFormFile> files)
    {
        const long maxFileSize = 10 * 1024 * 1024; // 10MB
        var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
        var allowedVideoExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv" };
        
        var allowedExtensions = allowedImageExtensions.Concat(allowedVideoExtensions).ToArray();

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

            try
            {
                // Xác định resource type dựa trên extension
                bool isImage = allowedImageExtensions.Contains(extension);
                
                // Tạo public ID cho file trên Cloudinary
                var publicId = $"SchoolBook/Post/{postId}/PostAttachment/{Guid.NewGuid()}";

                RawUploadResult uploadResult;

                if (isImage)
                {
                    // Upload image
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, file.OpenReadStream()),
                        PublicId = publicId,
                        Folder = $"SchoolBook/Post/{postId}/PostAttachment"
                    };

                    uploadResult = await _cloudinary.UploadAsync(uploadParams);
                }
                else
                {
                    // Upload video
                    var uploadParams = new VideoUploadParams
                    {
                        File = new FileDescription(file.FileName, file.OpenReadStream()),
                        PublicId = publicId,
                        Folder = $"SchoolBook/Post/{postId}/PostAttachment"
                    };

                    uploadResult = await _cloudinary.UploadAsync(uploadParams);
                }

                if (uploadResult.Error != null)
                {
                    _logger.LogError("Lỗi Cloudinary khi upload file {FileName}: {Error}", 
                        file.FileName, uploadResult.Error.Message);
                    continue;
                }

                // Lưu thông tin vào database
                var attachment = new PostAttachment
                {
                    Id = Guid.NewGuid(),
                    PostId = postId,
                    FileName = file.FileName,
                    FilePath = uploadResult.SecureUrl.ToString(), // Lưu secure URL
                    FileSize = (int)file.Length,
                    UploadedAt = DateTime.UtcNow
                };

                _db.PostAttachments.Add(attachment);
                _logger.LogInformation("Đã upload file {FileName} lên Cloudinary thành công", file.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi upload file {FileName} lên Cloudinary", file.FileName);
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
            try
            {
                // Extract public ID từ URL (nếu có) hoặc tạo từ file path
                // Trong trường hợp này, chúng ta cần lưu public ID khi upload
                // Vì chúng ta không lưu public ID trong database, nên cần xác định public ID từ URL
                
                // Tạm thời xóa record từ database
                // Để triển khai đầy đủ, cần lưu public ID trong bảng PostAttachment
                _db.PostAttachments.Remove(attachment);
                
                _logger.LogInformation("Đã xóa attachment {AttachmentId} từ database", attachment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa attachment {AttachmentId} từ Cloudinary", attachment.Id);
            }
        }

        await _db.SaveChangesAsync();
    }
    
    public async Task DeletePostFolderFromCloudinaryAsync(Guid postId)
    {
        var folderPath = $"SchoolBook/Post/{postId}";

        try
        {
            _logger.LogInformation("Bắt đầu xóa folder Cloudinary: {Folder}", folderPath);

            // 1. Xóa toàn bộ resources trong folder
            while (true)
            {
                var listResult = await _cloudinary.ListResourcesByPrefixAsync(folderPath);

                if (listResult.Resources == null || listResult.Resources.Length == 0)
                    break;

                var publicIds = listResult.Resources.Select(r => r.PublicId).ToArray();

                var deleteResult = await _cloudinary.DeleteResourcesAsync(publicIds);

                if (deleteResult.Error != null)
                {
                    _logger.LogError("Lỗi khi xóa resources: {Error}", deleteResult.Error.Message);
                    break;
                }

                _logger.LogInformation("Đã xóa {Count} resources trong folder {Folder}",
                    publicIds.Length, folderPath);
            }

            // 2. Xóa folder
            var deleteFolder = await _cloudinary.DeleteFolderAsync(folderPath);

            if (deleteFolder.Error != null)
            {
                _logger.LogError("Lỗi khi xóa folder Cloudinary: {Error}", deleteFolder.Error.Message);
            }
            else
            {
                _logger.LogInformation("Đã xóa thành công folder {Folder}", folderPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi thực thi xóa folder Cloudinary {Folder}", folderPath);
        }
    }
    
    /// <summary>
    /// Lấy danh sách bài đăng từ những người đã follow (có phân trang)
    /// </summary>
    /// <param name="userId">ID của user</param>
    /// <param name="page">Số trang (bắt đầu từ 1)</param>
    /// <param name="pageSize">Số bài đăng mỗi trang</param>
    /// <returns>Danh sách bài đăng</returns>
   public async Task<IQueryable<Post>> GetFollowingPostsAsync(Guid userId, int page = 1, int pageSize = 20,
    string sortBy = "newest", string filterRole = "All",
    string filterTime = "all", DateTime? startDate = null, DateTime? endDate = null)
{
    // 1. Lấy danh sách người đang follow
    var followingIds = await _db.Following
        .Where(f => f.UserId == userId)
        .Select(f => f.FollowingId)
        .ToListAsync();

    if (!followingIds.Any())
    {
        return _db.Posts.Where(p => false);
    }

    var userRoles = await _db.GetUserRolesAsync(userId);
    var isAdmin = userRoles.Contains("HighAdmin") || 
                  userRoles.Contains("Admin") || 
                  userRoles.Contains("Moderator");

    // Query lấy bài đăng từ những người đã follow
    var query = _db.Posts.Where(p => followingIds.Contains(p.UserId));

    if (!isAdmin)
    {
        // User thường: chỉ xem bài có quyền
        query = query.Where(p => 
            p.IsVisible && 
            !p.IsDeleted &&
            (p.VisibleToRoles == null || 
             p.VisibleToRoles == "All" || 
             userRoles.Contains(p.VisibleToRoles)));
    }
    
    // Lọc theo role nếu không phải "All"
    if (filterRole != "All")
    {
        if (filterRole == "Admin")
        {
            query = query.Where(p => p.User.UserRoles.Any(ur =>
                ur.Role.Name == "Admin" ||
                ur.Role.Name == "HighAdmin" ||
                ur.Role.Name == "Moderator"));
        }
        else
        {
            query = query.Where(p => p.User.UserRoles.Any(ur => ur.Role.Name == filterRole));
        }
    }

    //sort time
    if (filterTime != "all")
    {
        var nowVn = DateTime.UtcNow.AddHours(7); // Giờ hiện tại VN
        DateTime fromDateUtc = DateTime.MinValue;
        DateTime toDateUtc = DateTime.MaxValue;

        switch (filterTime.ToLower())
        {
            case "today":
                // Từ 00:00 hôm nay (VN) -> đổi sang UTC (-7h)
                fromDateUtc = nowVn.Date.AddHours(-7);
                break;
            case "week":
                // 7 ngày gần nhất
                fromDateUtc = DateTime.UtcNow.AddDays(-7);
                break;
            case "month":
                // 30 ngày gần nhất
                fromDateUtc = DateTime.UtcNow.AddDays(-30);
                break;
            case "custom":
                if (startDate.HasValue)
                    fromDateUtc = startDate.Value.Date.AddHours(-7); // Bắt đầu ngày VN -> UTC

                if (endDate.HasValue)
                    // Hết ngày VN (23:59:59) -> UTC. 
                    // endDate.Value.Date là 0h, cộng 1 ngày là 0h hôm sau, trừ 1 tick
                    toDateUtc = endDate.Value.Date.AddDays(1).AddHours(-7).AddTicks(-1);
                break;
        }

        query = query.Where(p => p.CreatedAt >= fromDateUtc);

        // Chỉ áp dụng cận trên nếu là custom (để chặn ngày tương lai hoặc khoảng custom)
        if (filterTime.ToLower() == "custom" && endDate.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= toDateUtc);
        }
    }
    
    // Áp dụng sắp xếp
    switch (sortBy.ToLower())
    {
        case "best":
            // Best: sắp xếp theo upvote nhiều nhất
            var bestQuery = query
                .Select(p => new
                {
                    Post = p,
                    BestScore = p.Votes.Count(v => v.VoteType) - p.Votes.Count(v => !v.VoteType)
                })
                .OrderByDescending(x => x.BestScore)
                .ThenByDescending(x => x.Post.CreatedAt)
                .Select(x => x.Post);
                
            return bestQuery
                .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
                .Include(p => p.Votes)
                .Include(p => p.Comments)
                .Include(p => p.Attachments)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
            
        case "hot":
            // Hot: sắp xếp theo tổng tương tác nhiều nhất
            var hotQuery = query
                .Select(p => new
                {
                    Post = p,
                    HotScore = p.Votes.Count() // Tổng số vote
                })
                .OrderByDescending(x => x.HotScore)
                .ThenByDescending(x => x.Post.CreatedAt)
                .Select(x => x.Post);
                
            return hotQuery
                .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
                .Include(p => p.Votes)
                .Include(p => p.Comments)
                .Include(p => p.Attachments)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
            
        case "newest":
        default:
            // MỚI NHẤT: Sắp xếp chính xác theo thời gian
            var newestQuery = query
                .OrderByDescending(p => p.CreatedAt);
                
            return newestQuery
                .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
                .Include(p => p.Votes)
                .Include(p => p.Comments)
                .Include(p => p.Attachments)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
    }
}
    
}