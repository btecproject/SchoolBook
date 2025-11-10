using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels;

namespace SchoolBookPlatform.Controllers;

[Authorize(Policy = "AdminOrHigher")]
public class UsersController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManagementService _userManagementService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        AppDbContext db,
        UserManagementService userManagementService,
        ILogger<UsersController> logger)
    {
        _db = db;
        _userManagementService = userManagementService;
        _logger = logger;
    }

    // GET: Users
    public async Task<IActionResult> Index()
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var users = await _userManagementService.GetManageableUsersAsync(currentUserId);

        var viewModels = users.Select(u => new UserListViewModel
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            Roles = u.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>(),
            IsActive = u.IsActive,
            FaceRegistered = u.FaceRegistered,
            CreatedAt = u.CreatedAt
        }).ToList();

        return View(viewModels);
    }

    // GET: Users/Create
    public async Task<IActionResult> Create()
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentUserRoles = await _db.GetUserRolesAsync(currentUserId);

        var availableRoles = await _db.Roles.ToListAsync();
        
        // Nếu là Admin, chỉ cho phép chọn Moderator, Teacher, Student
        if (currentUserRoles.Contains("Admin") && !currentUserRoles.Contains("HighAdmin"))
        {
            availableRoles = availableRoles
                .Where(r => r.Name != "HighAdmin" && r.Name != "Admin")
                .ToList();
        }

        var viewModel = new CreateUserViewModel
        {
            AvailableRoles = availableRoles.Select(r => new RoleOption
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description ?? ""
            }).ToList()
        };

        return View(viewModel);
    }

    // POST: Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền tạo user với các roles này
        if (!await _userManagementService.CanCreateUserWithRolesAsync(currentUserId, model.RoleIds))
        {
            ModelState.AddModelError("", "Bạn không có quyền tạo user với các vai trò đã chọn.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }

        // Kiểm tra username đã tồn tại
        if (await _db.Users.AnyAsync(u => u.Username == model.Username))
        {
            ModelState.AddModelError("Username", "Username đã tồn tại.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }

        try
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = model.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                MustChangePassword = model.MustChangePassword,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);

            // Thêm roles
            foreach (var roleId in model.RoleIds)
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("User {UserId} created by {CurrentUserId}", user.Id, currentUserId);
            
            TempData["SuccessMessage"] = "Tạo user thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            ModelState.AddModelError("", "Có lỗi xảy ra khi tạo user. Vui lòng thử lại.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }
    }

    // GET: Users/Edit/5
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id == null)
            return NotFound();

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await _userManagementService.CanManageUserAsync(currentUserId, id.Value))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa user này.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users
            .Include(u => u.UserRoles!)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound();

        var currentUserRoles = await _db.GetUserRolesAsync(currentUserId);
        var availableRoles = await _db.Roles.ToListAsync();
        
        // Nếu là Admin, chỉ cho phép chọn Moderator, Teacher, Student
        if (currentUserRoles.Contains("Admin") && !currentUserRoles.Contains("HighAdmin"))
        {
            availableRoles = availableRoles
                .Where(r => r.Name != "HighAdmin" && r.Name != "Admin")
                .ToList();
        }

        var viewModel = new EditUserViewModel
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            MustChangePassword = user.MustChangePassword,
            IsActive = user.IsActive,
            FaceRegistered = user.FaceRegistered,
            RoleIds = user.UserRoles?.Select(ur => ur.RoleId).ToList() ?? new List<Guid>(),
            CurrentRoles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>(),
            AvailableRoles = availableRoles.Select(r => new RoleOption
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description ?? ""
            }).ToList()
        };

        return View(viewModel);
    }

    // POST: Users/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditUserViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await _userManagementService.CanManageUserAsync(currentUserId, id))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa user này.";
            return RedirectToAction(nameof(Index));
        }

        // Kiểm tra quyền tạo user với các roles này
        if (!await _userManagementService.CanCreateUserWithRolesAsync(currentUserId, model.RoleIds))
        {
            ModelState.AddModelError("", "Bạn không có quyền gán các vai trò đã chọn.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await _db.GetUserRolesAsync(id);
            return View(model);
        }

        // Kiểm tra username đã tồn tại (trừ user hiện tại)
        if (await _db.Users.AnyAsync(u => u.Username == model.Username && u.Id != id))
        {
            ModelState.AddModelError("Username", "Username đã tồn tại.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await _db.GetUserRolesAsync(id);
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await _db.GetUserRolesAsync(id);
            return View(model);
        }

        try
        {
            var user = await _db.Users
                .Include(u => u.UserRoles!)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            user.Username = model.Username;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            user.MustChangePassword = model.MustChangePassword;
            user.IsActive = model.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            // Cập nhật roles
            var currentRoleIds = user.UserRoles?.Select(ur => ur.RoleId).ToList() ?? new List<Guid>();
            
            // Xóa roles không còn trong danh sách
            var rolesToRemove = user.UserRoles?
                .Where(ur => !model.RoleIds.Contains(ur.RoleId))
                .ToList() ?? new List<UserRole>();
            
            foreach (var roleToRemove in rolesToRemove)
            {
                _db.UserRoles.Remove(roleToRemove);
            }

            // Thêm roles mới
            var rolesToAdd = model.RoleIds
                .Where(rid => !currentRoleIds.Contains(rid))
                .ToList();

            foreach (var roleId in rolesToAdd)
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("User {UserId} updated by {CurrentUserId}", id, currentUserId);
            
            TempData["SuccessMessage"] = "Cập nhật user thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật user. Vui lòng thử lại.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await _db.GetUserRolesAsync(id);
            return View(model);
        }
    }

    // GET: Users/Delete/5
    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id == null)
            return NotFound();

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await _userManagementService.CanManageUserAsync(currentUserId, id.Value))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xóa user này.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users
            .Include(u => u.UserRoles!)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound();

        var viewModel = new UserListViewModel
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>(),
            IsActive = user.IsActive,
            FaceRegistered = user.FaceRegistered,
            CreatedAt = user.CreatedAt
        };

        return View(viewModel);
    }

    // POST: Users/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await _userManagementService.CanManageUserAsync(currentUserId, id))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xóa user này.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy user.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            // Soft delete: Set IsActive = false thay vì xóa hoàn toàn
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            
            // Revoke all tokens
            await _userManagementService.RevokeAllTokensAsync(id);

            await _db.SaveChangesAsync();
            _logger.LogInformation("User {UserId} deactivated by {CurrentUserId}", id, currentUserId);
            
            TempData["SuccessMessage"] = "Vô hiệu hóa user thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {UserId}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi vô hiệu hóa user. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Users/ResetPassword
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (request == null || request.Id == Guid.Empty)
        {
            return Json(new { success = false, message = "Invalid request." });
        }

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await _userManagementService.CanManageUserAsync(currentUserId, request.Id))
        {
            return Json(new { success = false, message = "Bạn không có quyền reset mật khẩu cho user này." });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return Json(new { success = false, message = "Mật khẩu phải có ít nhất 6 ký tự." });
        }

        var success = await _userManagementService.ResetPasswordAsync(request.Id, request.NewPassword);
        
        if (success)
        {
            _logger.LogInformation("Password reset for user {UserId} by {CurrentUserId}", request.Id, currentUserId);
            return Json(new { success = true, message = "Reset mật khẩu thành công!" });
        }

        return Json(new { success = false, message = "Có lỗi xảy ra khi reset mật khẩu." });
    }

    // POST: Users/RevokeTokens
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RevokeTokens([FromBody] RevokeTokensRequest request)
    {
        if (request == null || request.Id == Guid.Empty)
        {
            return Json(new { success = false, message = "Invalid request." });
        }

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await _userManagementService.CanManageUserAsync(currentUserId, request.Id))
        {
            return Json(new { success = false, message = "Bạn không có quyền revoke tokens cho user này." });
        }

        var success = await _userManagementService.RevokeAllTokensAsync(request.Id);
        
        if (success)
        {
            _logger.LogInformation("Tokens revoked for user {UserId} by {CurrentUserId}", request.Id, currentUserId);
            return Json(new { success = true, message = "Đã hủy tất cả tokens! User sẽ bị đăng xuất." });
        }

        return Json(new { success = false, message = "Có lỗi xảy ra khi revoke tokens." });
    }

    // Helper method
    private async Task<List<RoleOption>> GetAvailableRolesForCurrentUserAsync()
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentUserRoles = await _db.GetUserRolesAsync(currentUserId);

        var availableRoles = await _db.Roles.ToListAsync();
        
        // Nếu là Admin, chỉ cho phép chọn Moderator, Teacher, Student
        if (currentUserRoles.Contains("Admin") && !currentUserRoles.Contains("HighAdmin"))
        {
            availableRoles = availableRoles
                .Where(r => r.Name != "HighAdmin" && r.Name != "Admin")
                .ToList();
        }

        return availableRoles.Select(r => new RoleOption
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description ?? ""
        }).ToList();
    }
}

// Request models
public class ResetPasswordRequest
{
    public Guid Id { get; set; }
    public string NewPassword { get; set; } = null!;
}

public class RevokeTokensRequest
{
    public Guid Id { get; set; }
}

