using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels;
using SchoolBookPlatform.ViewModels.Admin;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace SchoolBookPlatform.Controllers;

[Authorize(Policy = "AdminOrHigher")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly UserManagementService _userManagementService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext db,
        UserManagementService userManagementService,
        IConfiguration config,
        ILogger<AdminController> logger)
    {
        _db = db;
        _userManagementService = userManagementService;
        _logger = logger;
        _config =  config;
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
        //it nhất 1 role
        if (model.RoleIds == null || !model.RoleIds.Any())
        {
            ModelState.AddModelError("RoleIds", "Phải chọn ít nhất một vai trò.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }
        
        //HighAdmin chỉ 1 role
        var highAdminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "HighAdmin");
        if (highAdminRole != null && model.RoleIds.Contains(highAdminRole.Id))
        {
            if (model.RoleIds.Count > 1)
            {
                ModelState.AddModelError("RoleIds",
                    "HighAdmin chỉ được có duy nhất role HighAdmin, không được có thêm role khác.");
                model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync(); 
                return View(model);
            }
        }

        // Kiểm tra username đã tồn tại
        if (await _db.Users.AnyAsync(u => u.Username == model.Username))
        {
            ModelState.AddModelError("Username", "Username đã tồn tại.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }
        
        //Kiểm tra trùng emial
        if (!string.IsNullOrWhiteSpace(model.Email) && await _db.Users.AnyAsync(u => u.Email == model.Email))
        {
            ModelState.AddModelError("Email", "Email is existed");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }
        
        //Kiển tra trùng sdt
        if (!string.IsNullOrWhiteSpace(model.PhoneNumber) &&
            await _db.Users.AnyAsync(u => u.PhoneNumber == model.PhoneNumber))
        {
            ModelState.AddModelError("PhoneNumber", "Phone number is existed");
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

            if (model.SendEmail && !string.IsNullOrEmpty(model.Email))
            {
                await SendLoginInfoToEmail(model.Email, model.Username,  model.Password);
            }
            
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

    public async Task SendLoginInfoToEmail(string email, string username, string password)
    {
        var apiKey = _config["SendGrid:ApiKey"];
        var fromEmail = _config["SendGrid:FromEmail"];
        var fromName = _config["SendGrid:FromName"];

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("SendGrid API Key chưa cấu hình.");

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, fromName);
        var to = new EmailAddress(email);
        var subject = "Thông tin đăng Nhập - SchoolBook";

        var htmlContent = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
            <h2 style='text-align: center; color: #007bff;'>SchoolBook Platform</h2>
            <p>Xin chào <strong>{email}</strong>,</p>
            <p>Thông tin đăng nhập của bạn:</p>
            <div style='text-align: center; margin: 20px 0;'>
                <span style='font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #007bff;'>
                    Username: {username}<br>
                    Password: {password}<br>
                </span>
            </div>
            <hr>
            <small style='color: #666;'>
                Vui lòng bảo quản kỹ thông tin đăng nhập của mình ! <br>
                Email được gửi tự động, không trả lời.
            </small>
        </div>";

        var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
        var response = await client.SendEmailAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Body.ReadAsStringAsync();
            _logger.LogError("SendGrid lỗi {Status}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"SendGrid lỗi: {response.StatusCode}");
        }

        _logger.LogInformation("Email OTP gửi thành công đến {Email}",email);
    }
    
    // GET: Users/Edit
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
        
        //it nhất 1 role
        if (model.RoleIds == null || !model.RoleIds.Any())
        {
            ModelState.AddModelError("RoleIds", "Phải chọn ít nhất một vai trò.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await _db.GetUserRolesAsync(id);
            return View(model);
        }
        
        //HighAdmin chỉ 1 role
        var highAdminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "HighAdmin");
        if (highAdminRole != null && model.RoleIds.Contains(highAdminRole.Id))
        {
            if (model.RoleIds.Count > 1)
            {
                ModelState.AddModelError("RoleIds", "HighAdmin chỉ được có duy nhất role HighAdmin, không được có thêm role khác.");
                model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
                model.CurrentRoles = await _db.GetUserRolesAsync(id);
                return View(model);
            }
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
        //Kiểm tra email
        if (!string.IsNullOrEmpty(model.Email) && await _db.Users.AnyAsync(u => u.Email == model.Email && u.Id != id))
        {
            ModelState.AddModelError("Email", "Email is existed");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await _db.GetUserRolesAsync(id);
            return View(model);
        }
        //kiểm tra sdt
        if (!string.IsNullOrEmpty(model.PhoneNumber) && await _db.Users.AnyAsync(u => u.PhoneNumber == model.PhoneNumber && u.Id != id))
        {
            ModelState.AddModelError("PhoneNumber", "Phone Number is existed");
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

    // GET: Users/Delete
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

    // POST: Users/Delete
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
        try{
            // Revoke all tokens
            await _userManagementService.RevokeAllTokensAsync(id);
            var user = await _db.Users
                .Include(u => u.UserRoles)
                .Include(u => u.OtpCodes)
                .Include(u => u.FaceProfile)
                // Nếu có bài đăng, bình luận, ..., cần xử lý tùy theo (ví dụ: soft delete hoặc cascade)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy user.";
                return RedirectToAction(nameof(Index));
            }

            if (user.UserRoles != null && user.UserRoles.Any())
            {
                _db.UserRoles.RemoveRange(user.UserRoles);
            }
            if (user.OtpCodes != null && user.OtpCodes.Any())
            {
                _db.OtpCodes.RemoveRange(user.OtpCodes);
            }

            if (user.FaceProfile != null)
            {
                _db.FaceProfiles.Remove(user.FaceProfile);
            }
            
            // _db.Users.Remove(user);
            // await _db.SaveChangesAsync();
            // DÙNG STORED PROCEDURE ĐÃ VIẾT SẴN – XÓA SẠCH HOÀN TOÀN
            await _db.Database.ExecuteSqlRawAsync("EXEC usp_DeleteUser @userId", 
                new SqlParameter("@userId", id));
            _logger.LogInformation("User {UserId} deleted by {CurrentUserId}", id, currentUserId);
            
            TempData["SuccessMessage"] = "Deleted user completed!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Deleting user {UserId}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa user. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Users/DisableUser
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DisableUser([FromBody] RevokeTokensRequest request)
    {
        if (request == null || request.Id == Guid.Empty)
        {
            return Json(new { success = false, message = "Yêu cầu không hợp lệ." });
        }

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (!await _userManagementService.CanManageUserAsync(currentUserId, request.Id))
        {
            return Json(new { success = false, message = "Bạn không có quyền vô hiệu hóa user này." });
        }

        try
        {
            var user = await _db.Users.FindAsync(request.Id);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy user." });

            if (!user.IsActive)
                return Json(new { success = false, message = "User đã bị vô hiệu hóa." });

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;

            // Revoke all tokens
            await _userManagementService.RevokeAllTokensAsync(request.Id);

            await _db.SaveChangesAsync();
            _logger.LogInformation("User {UserId} disabled by {CurrentUserId}", request.Id, currentUserId);
            return Json(new { success = true, message = "Vô hiệu hóa user thành công! User sẽ không thể đăng nhập." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling user {UserId}", request.Id);
            return Json(new { success = false, message = "Có lỗi xảy ra khi vô hiệu hóa user." });
        }
    }

    // POST: Users/EnableUser
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> EnableUser([FromBody] RevokeTokensRequest request)
    {
        if (request == null || request.Id == Guid.Empty)
        {
            return Json(new { success = false, message = "Yêu cầu không hợp lệ." });
        }

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (!await _userManagementService.CanManageUserAsync(currentUserId, request.Id))
        {
            return Json(new { success = false, message = "Bạn không có quyền kích hoạt user này." });
        }

        try
        {
            var user = await _db.Users.FindAsync(request.Id);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy user." });

            if (user.IsActive)
                return Json(new { success = false, message = "User đã hoạt động." });

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _logger.LogInformation("User {UserId} enabled by {CurrentUserId}", request.Id, currentUserId);
            return Json(new { success = true, message = "Kích hoạt user thành công!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling user {UserId}", request.Id);
            return Json(new { success = false, message = "Có lỗi xảy ra khi kích hoạt user." });
        }
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

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Json(new { success = false, message = "Mật khẩu mới không được để trống." });
        }

        // Kiểm tra tiêu chí mật khẩu
        var passwordRegex = new System.Text.RegularExpressions.Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$");
        if (!passwordRegex.IsMatch(request.NewPassword))
        {
            return Json(new { success = false, message = "Mật khẩu phải ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ít nhất một ký tự đặc biệt." });
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
        var targetUserRoles = await _db.GetUserRolesAsync(request.Id);
        if (targetUserRoles.Contains("HighAdmin"))
        {
            return Json(new { success = false, message = "Không thể hủy tokens của HighAdmin." });
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

