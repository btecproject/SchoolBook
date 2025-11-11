using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services;

public class UserManagementService
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserManagementService> _logger;
    private readonly TokenService _tokenService;

    public UserManagementService(AppDbContext db, ILogger<UserManagementService> logger,  TokenService tokenService)
    {
        _db = db;
        _logger = logger;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Kiểm tra xem currentUser có quyền quản lý targetUser không
    /// HighAdmin: quản lý tất cả users
    /// Admin: chỉ quản lý Moderator, Teacher, Student (không quản lý HighAdmin, Admin)
    /// </summary>
    public async Task<bool> CanManageUserAsync(Guid currentUserId, Guid targetUserId)
    {
        if (currentUserId == targetUserId)
            return false; // Không được quản lý chính mình

        var currentUserRoles = await _db.GetUserRolesAsync(currentUserId);
    
        //HighAdmin có thể quản lý tất cả trừ chính mình
        if (currentUserRoles.Contains("HighAdmin"))
            return true;

        //Admin chỉ quản lý Moderator, Teacher, Student
        if (currentUserRoles.Contains("Admin"))
        {
            var targetUserRoles = await _db.GetUserRolesAsync(targetUserId);
            if (targetUserRoles.Contains("HighAdmin") || targetUserRoles.Contains("Admin"))
                return false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra xem currentUser có quyền tạo user với các roles này không
    /// </summary>
    public async Task<bool> CanCreateUserWithRolesAsync(Guid currentUserId, List<Guid> roleIds)
    {
        var currentUserRoles = await _db.GetUserRolesAsync(currentUserId);
        // HighAdmin có thể tạo user với bất kỳ role nào
        if (currentUserRoles.Contains("HighAdmin"))
            return true;

        // Admin chỉ có thể tạo user với roles: Moderator, Teacher, Student
        if (currentUserRoles.Contains("Admin"))
        {
            var roles = await _db.Roles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToListAsync();

            // Không được tạo HighAdmin hoặc Admin
            if (roles.Contains("HighAdmin") || roles.Contains("Admin"))
                return false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Lấy danh sách users mà currentUser có quyền quản lý
    /// </summary>
    public async Task<List<User>> GetManageableUsersAsync(Guid currentUserId)
    {
        var currentUserRoles = await _db.GetUserRolesAsync(currentUserId);
        
        // HighAdmin: lấy tất cả users
        if (currentUserRoles.Contains("HighAdmin"))
        {
            return await _db.Users
                .Include(u => u.UserRoles!)
                .ThenInclude(ur => ur.Role)
                .ToListAsync();
        }

        // Admin: chỉ lấy users có role Moderator, Teacher, Student
        if (currentUserRoles.Contains("Admin"))
        {
            var manageableRoleIds = await _db.Roles
                .Where(r => r.Name == "Moderator" || r.Name == "Teacher" || r.Name == "Student")
                .Select(r => r.Id)
                .ToListAsync();

            return await _db.Users
                .Include(u => u.UserRoles!)
                .ThenInclude(ur => ur.Role)
                .Where(u => u.UserRoles!.Any(ur => manageableRoleIds.Contains(ur.RoleId)))
                .ToListAsync();
        }

        return new List<User>();
    }

    /// <summary>
    /// Reset password cho user
    /// </summary>
    public async Task<bool> ResetPasswordAsync(Guid userId, string newPassword)
    {
        try
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = true;
            user.UpdatedAt = DateTime.UtcNow;
            user.TokenVersion++; // Invalidate all existing tokens

            await _db.SaveChangesAsync();
            _logger.LogInformation("Password reset for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Revoke tất cả tokens của user (force logout)
    /// </summary>
    public async Task<bool> RevokeAllTokensAsync(Guid userId)
    {
        if (await _tokenService.RevokeAllTokensAsync(userId))
        {
            return true;
        }
        return false;
        // try
        // {
        //     var user = await _db.Users.FindAsync(userId);
        //     if (user == null)
        //         return false;
        //
        //     // Tăng TokenVersion để invalidate tất cả tokens hiện tại
        //     user.TokenVersion++;
        //     user.UpdatedAt = DateTime.UtcNow;
        //
        //     // Revoke tất cả tokens trong database
        //     var tokens = await _db.UserTokens
        //         .Where(t => t.UserId == userId && !t.IsRevoked)
        //         .ToListAsync();
        //
        //     foreach (var token in tokens)
        //     {
        //         token.IsRevoked = true;
        //     }
        //
        //     await _db.SaveChangesAsync();
        //     _logger.LogInformation("All tokens revoked for user {UserId}", userId);
        //     return true;
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "Error revoking tokens for user {UserId}", userId);
        //     return false;
        // }
    }
}

