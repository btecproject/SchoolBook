using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Post;
using SchoolBookPlatform.ViewModels.Profile;

namespace SchoolBookPlatform.Controllers;
public class UpdateFieldRequest
{
    public string Field { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime? DateValue { get; set; }
}
public class FollowRequest
{
    public Guid UserId { get; set; }
}

public class UpdatePrivacyRequest
{
    public string Field {get; set;} = string.Empty;
    public bool IsPublic { get; set; }
}
[Authorize]
public class ProfileController(
    AppDbContext db,
    AvatarService  avatarService,
    ILogger<ProfileController> logger,
    Cloudinary  cloudinary) : Controller
{
    // GET
    [Authorize]
    [Authorize]
    public async Task<IActionResult> Index(string username)
    {
        var targetUser = await db.GetUserWithProfileAsync(username);
        if (targetUser == null) return NotFound();
        
        if (targetUser.UserProfile == null)
        {
            targetUser.UserProfile = await db.EnsureProfileAsync(targetUser.Id);
        }
        
        var currentUser = await HttpContext.GetCurrentUserAsync(db);
        if (currentUser == null) return Unauthorized();
        
        var isOwner = currentUser.Id == targetUser.Id;

        var canViewPrivate = await db.CanViewPrivateInfoAsync(HttpContext, targetUser.Id);
        
        // follower/following
        var followerCount = await db.Followers.CountAsync(f => f.UserId == targetUser.Id);
        var followingCount = await db.Following.CountAsync(f => f.UserId == targetUser.Id);

        // đã follow chưa
        var isFollowing = !isOwner && await db.Followers
            .AnyAsync(f => f.UserId == targetUser.Id && f.FollowerId == currentUser.Id);

        // Lấy bài đăng của user và chuyển sang PostViewModel
        var userPosts = await db.Posts
            .Where(p => p.UserId == targetUser.Id && !p.IsDeleted && p.IsVisible)
            .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
            .Include(p => p.Votes)
            .Include(p => p.Comments)
            .Include(p => p.Attachments)
            .OrderByDescending(p => p.CreatedAt)
            .Take(20)
            .Select(p => new PostViewModel
            {
                Id = p.Id,
                Title = p.Title,
                Content = p.Content,
                AuthorName = p.User.Username,
                AuthorAvatar = p.User.UserProfile.AvatarUrl,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                UpvoteCount = p.Votes.Count(v => v.VoteType),
                DownvoteCount = p.Votes.Count(v => !v.VoteType),
                CommentCount = p.Comments.Count,
                IsDeleted = p.IsDeleted,
                IsVisible = p.IsVisible,
                VisibleToRoles = p.VisibleToRoles,
                IsOwner = currentUser.Id == p.UserId,
                CanDelete = currentUser.Id == p.UserId,
                Attachments = p.Attachments.Select(a => new AttachmentViewModel
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    FileSize = a.FileSize,
                    UploadedAt = a.UploadedAt
                }).ToList()
            })
            .ToListAsync();

        var model = new ProfileViewModel
        {
            UserId = targetUser.Id,
            Username = targetUser.Username,
            FullName = targetUser.UserProfile?.FullName,
            AvatarUrl = targetUser.UserProfile?.AvatarUrl ?? "/images/avatars/default.png",
            Bio = targetUser.UserProfile?.Bio ?? "Chưa có mô tả",
            Gender = targetUser.UserProfile?.Gender,
            BirthDate = targetUser.UserProfile?.BirthDate,
            Email = canViewPrivate ? targetUser.Email : (targetUser.UserProfile?.IsEmailPublic == true ? targetUser.Email : null),
            PhoneNumber = canViewPrivate ? targetUser.PhoneNumber : (targetUser.UserProfile?.IsPhonePublic == true ? targetUser.PhoneNumber : null),
            CreatedAt = targetUser.CreatedAt,

            IsEmailPublic = targetUser.UserProfile?.IsEmailPublic ?? false,
            IsPhonePublic = targetUser.UserProfile?.IsPhonePublic ?? false,
            IsBirthDatePublic = targetUser.UserProfile?.IsBirthDatePublic ?? false,
            IsFollowersPublic = targetUser.UserProfile?.IsFollowersPublic ?? true,
            
            FollowerCount = followerCount,
            FollowingCount = followingCount,
            IsFollowing = isFollowing,
            IsOwner = isOwner,
            CanEdit = isOwner,
            
            // Sử dụng PostViewModel
            UserPosts = userPosts,
            PostCount = await db.Posts.CountAsync(p => p.UserId == targetUser.Id && !p.IsDeleted && p.IsVisible)
        };
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrivacy([FromBody] UpdatePrivacyRequest request)
    {
        try
        {
            logger.LogInformation("UpdatePrivacy - Field: {Field}, Value: {Value}", 
                request.Field, request.IsPublic);
            var user = await HttpContext.GetCurrentUserAsync(db);
            if (user == null)
            {
                logger.LogWarning("UpdatePrivacy - User not authenticated");
                return Json(new { success = false, message = "Bạn chưa đăng nhập" });
            }
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                logger.LogInformation("Creating new profile for user {UserId}", user.Id);
                profile = await db.EnsureProfileAsync(user.Id);
            }
            bool updated = false;
            switch (request.Field)
            {
                case "IsEmailPublic":
                    profile.IsEmailPublic = request.IsPublic;
                    updated = true;
                    break;
                
                case "IsPhonePublic":
                    profile.IsPhonePublic = request.IsPublic;
                    updated = true;
                    break;
                
                case "IsBirthDatePublic":
                    profile.IsBirthDatePublic = request.IsPublic;
                    updated = true;
                    break;
                
                case "IsFollowersPublic":
                    profile.IsFollowersPublic = request.IsPublic;
                    updated = true;
                    break;
                
                default:
                    logger.LogWarning("Invalid privacy field: {Field}", request.Field);
                    return Json(new { success = false, message = "Trường không hợp lệ" });
            }

            if (updated)
            {
                profile.UpdatedAt = DateTime.UtcNow.AddHours(7);
                await db.SaveChangesAsync();
            
                logger.LogInformation("Privacy updated: {Field} = {Value} for user {UserId}", 
                    request.Field, request.IsPublic, user.Id);
            
                return Json(new { success = true, isPublic = request.IsPublic });
            }

            return Json(new { success = false, message = "Không có thay đổi" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in UpdatePrivacy");
            return Json(new { success = false, message = "Đã xảy ra lỗi: " + ex.Message });
        }
    }
    [HttpPost]
    public async Task<IActionResult> Follow([FromBody] FollowRequest request)
    {
        try
        {
            logger.LogInformation("Follow request - Target UserId: {UserId}", request.UserId);

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                logger.LogWarning("Follow - User not authenticated");
                return Json(new { success = false, message = "Bạn chưa đăng nhập" });
            }
            if (currentUser.Id == request.UserId)
            {
                logger.LogWarning("User {UserId} tried to follow themselves", currentUser.Id);
                return Json(new { success = false, message = "Không thể theo dõi chính mình" });
            }

            var targetUser = await db.Users.FindAsync(request.UserId);
            if (targetUser == null)
            {
                logger.LogWarning("Target user {UserId} not found", request.UserId);
                return Json(new { success = false, message = "Người dùng không tồn tại" });
            }

            var exists = await db.Followers
                .AnyAsync(f => f.UserId == request.UserId && f.FollowerId == currentUser.Id);

            if (exists)
            {
                logger.LogInformation("User {CurrentUserId} already follows {TargetUserId}", 
                    currentUser.Id, request.UserId);
                return Json(new { success = true, isFollowing = true, message = "Đã theo dõi người này rồi" });
            }

            // Tạo quan hệ follow
            db.Followers.Add(new Follower 
            { 
                UserId = request.UserId, 
                FollowerId = currentUser.Id,
                FollowedAt = DateTime.UtcNow.AddHours(7)
            });

            db.Following.Add(new Following 
            { 
                UserId = currentUser.Id, 
                FollowingId = request.UserId,
                FollowedAt = DateTime.UtcNow.AddHours(7)
            });

            await db.SaveChangesAsync();

            logger.LogInformation("User {CurrentUserId} successfully followed {TargetUserId}", 
                currentUser.Id, request.UserId);

            // Đếm lại số followers
            var followerCount = await db.Followers.CountAsync(f => f.UserId == request.UserId);

            return Json(new { 
                success = true, 
                isFollowing = true,
                followerCount = followerCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Follow action");
            return Json(new { success = false, message = "Đã xảy ra lỗi khi theo dõi" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Unfollow([FromBody] FollowRequest request)
    {
        try
        {
            logger.LogInformation("Unfollow request - Target UserId: {UserId}", request.UserId);

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                logger.LogWarning("Unfollow - User not authenticated");
                return Json(new { success = false, message = "Bạn chưa đăng nhập" });
            }

            // Tìm quan hệ follow trong Followers
            var followerRelation = await db.Followers
                .FirstOrDefaultAsync(f => f.UserId == request.UserId && f.FollowerId == currentUser.Id);

            // Tìm quan hệ follow trong Following
            var followingRelation = await db.Following
                .FirstOrDefaultAsync(f => f.UserId == currentUser.Id && f.FollowingId == request.UserId);

            if (followerRelation == null && followingRelation == null)
            {
                logger.LogWarning("User {CurrentUserId} is not following {TargetUserId}", 
                    currentUser.Id, request.UserId);
                return Json(new { success = true, isFollowing = false, message = "Bạn chưa theo dõi người này" });
            }

            // Xóa cả hai quan hệ
            if (followerRelation != null)
            {
                db.Followers.Remove(followerRelation);
            }

            if (followingRelation != null)
            {
                db.Following.Remove(followingRelation);
            }

            await db.SaveChangesAsync();

            logger.LogInformation("User {CurrentUserId} successfully unfollowed {TargetUserId}", 
                currentUser.Id, request.UserId);

            // Đếm lại số followers
            var followerCount = await db.Followers.CountAsync(f => f.UserId == request.UserId);

            return Json(new { 
                success = true, 
                isFollowing = false,
                followerCount = followerCount
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Unfollow action");
            return Json(new { success = false, message = "Đã xảy ra lỗi khi bỏ theo dõi" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UpdateField([FromBody] UpdateFieldRequest request)
    {
        try 
        {
            logger.LogInformation("UpdateField - Field: {Field}, Value: {Value}, DateValue: {DateValue}", 
                request.Field, request.Value, request.DateValue);

            var user = await HttpContext.GetCurrentUserAsync(db);
            if (user == null)
            {
                logger.LogWarning("UpdateField - User not authenticated");
                return Json(new { success = false, message = "Bạn chưa đăng nhập" });
            }

            if (!new[] { "Bio", "FullName", "Gender", "BirthDate" }.Contains(request.Field))
            {
                logger.LogWarning("UpdateField - Invalid field: {Field}", request.Field);
                return Json(new { success = false, message = "Không được phép chỉnh sửa trường này" });
            }

            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                logger.LogInformation("Creating new profile for user {UserId}", user.Id);
                profile = await db.EnsureProfileAsync(user.Id);
            }

            bool updated = false;
            switch (request.Field)
            {
                case "FullName":
                    profile.FullName = request.Value?.Trim();
                    updated = true;
                    break;
                    
                case "Bio":
                    profile.Bio = request.Value?.Trim();
                    updated = true;
                    break;
                    
                case "Gender":
                    if (new[] { "Male", "Female", "Other", null, "" }.Contains(request.Value))
                    {
                        profile.Gender = string.IsNullOrEmpty(request.Value) ? null : request.Value;
                        updated = true;
                    }
                    else
                    {
                        logger.LogWarning("Invalid gender value: {Value}", request.Value);
                        return Json(new { success = false, message = "Giá trị giới tính không hợp lệ" });
                    }
                    break;
                    
                case "BirthDate":
                    profile.BirthDate = request.DateValue;
                    updated = true;
                    break;
            }

            if (updated)
            {
                profile.UpdatedAt = DateTime.UtcNow.AddHours(7);
                
                var result = await db.SaveChangesAsync();
                logger.LogInformation("Profile updated successfully. Rows affected: {Rows}", result);
                
                return Json(new { success = true });
            }
            else
            {
                logger.LogWarning("No update performed for field: {Field}", request.Field);
                return Json(new { success = false, message = "Không có thay đổi nào được thực hiện" });
            }
        }
        catch (DbUpdateException dbEx)
        {
            logger.LogError(dbEx, "Database error in UpdateField");
            return Json(new { success = false, message = "Lỗi cơ sở dữ liệu: " + dbEx.InnerException?.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in UpdateField");
            return Json(new { success = false, message = "Đã xảy ra lỗi: " + ex.Message });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> UploadAvatar(IFormFile? avatar)
    {
        if (avatar == null || avatar.Length == 0)
            return BadRequest("Không có file");

        var user = await HttpContext.GetCurrentUserAsync(db);
        if(user == null) return RedirectToAction("Login", "Authen");
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id)
                      ?? new UserProfile { UserId = user.Id }; 
        var uploadResult = await avatarService.UploadAvatar(avatar, user, profile);
        if (!uploadResult)
        {
            return BadRequest("Error in UploadAvatar");
        }
        
        if (profile.UserId == Guid.Empty)
            db.UserProfiles.Add(profile);
        else
            db.UserProfiles.Update(profile);

        await db.SaveChangesAsync();

        return RedirectToAction("Index", new { username = user.Username });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAvatar()
    {
        var user = await HttpContext.GetCurrentUserAsync(db);
        if(user == null) return RedirectToAction("Login", "Authen");
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id)
                      ?? new UserProfile { UserId = user.Id };
        var deleteResult = await avatarService.DeleteAvatar(user, profile, true);
        if (!deleteResult)
        {
            return BadRequest("Error in DeleteAvatar");
        }
        return RedirectToAction("Index", new { username = user.Username });
    }
    
}