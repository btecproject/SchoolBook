using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Hubs;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels;
using SchoolBookPlatform.ViewModels.Admin;
using SchoolBookPlatform.ViewModels.Post;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace SchoolBookPlatform.Controllers;

[Authorize] // Chỉ yêu cầu đăng nhập, policy sẽ kiểm tra ở từng action
public class AdminController(
    AppDbContext db,
    UserManagementService userManagementService,
    IConfiguration config,
    AvatarService avatarService,
    PostService postService,
    ILogger<AdminController> logger)
    : Controller
{
    // Suppress unused parameter warning - postService may be used in future
    private readonly PostService _postService = postService;

    // GET: Users
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> Index()
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var users = await userManagementService.GetManageableUsersAsync(currentUserId);

        var viewModels = users.Select(u => new UserListViewModel
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            Roles = u.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>(),
            IsActive = u.IsActive,
            FaceRegistered = u.FaceRegistered,
            CreatedAt = u.CreatedAt,
            AvatarUrl = avatarService.GetAvatar(u)
        }).ToList();

        return View(viewModels);
    }

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "AdminOrHigher")]
public async Task<IActionResult> ImportStudentsFromExcel(IFormFile? excelFile,
    [FromServices] IHubContext<ImportExcelHub> hubContext)
{
    logger.LogInformation("START IMPORT");
    
    try
    {
        if (excelFile == null || excelFile.Length == 0)
        {
            logger.LogWarning("Không có file được upload");
            return Json(new { success = false, message = "Vui lòng chọn file Excel" });
        }

        logger.LogInformation("File nhận được: {FileName}, Size: {Size} bytes", excelFile.FileName, excelFile.Length);

        if (Path.GetExtension(excelFile.FileName) != ".xlsx")
        {
            return Json(new { success = false, message = "Chỉ hỗ trợ file .xlsx" });
        }

        var user = await HttpContext.GetCurrentUserAsync(db);
        if (user == null)
        {
            logger.LogWarning("User không tồn tại");
            return Json(new { success = false, message = "Vui lòng đăng nhập!" });
        }

        logger.LogInformation("User {UserId} đang import", user.Id);

        var roles = await db.GetUserRolesAsync(user.Id);
        if (!roles.Contains("HighAdmin") && !roles.Contains("Admin"))
        {
            logger.LogWarning("User {UserId} không có quyền import", user.Id);
            return Json(new { success = false, message = "Không có quyền!" });
        }

        int successCount = 0;
        int totalProcessed = 0;

        using var stream = new MemoryStream();
        await excelFile.CopyToAsync(stream);
        using var package = new OfficeOpenXml.ExcelPackage(stream);
        var ws = package.Workbook.Worksheets.First();
        
        if (ws.Dimension == null)
        {
            logger.LogWarning("File Excel trống");
            return Json(new { success = false, message = "File Excel trống hoặc không hợp lệ." });
        }

        int rowCount = ws.Dimension.Rows;
        int colCount = ws.Dimension.Columns;
        
        logger.LogInformation("File có {RowCount} dòng, {ColCount} cột", rowCount, colCount);

        var headerRow = ws.Cells[1, 1, 1, colCount];
        var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        //Đọc header
        foreach (var cell in headerRow)
        {
            var header = cell.Text?.Trim();
            if (string.IsNullOrEmpty(header)) continue;
            
            logger.LogInformation("Header cột {Col}: '{Header}'", cell.Start.Column, header);
            
            var normalized = header.RemoveDiacritics().ToLower().Replace(" ", "");
            logger.LogInformation("Test normalize: Số điện thoại: "+ "Số điện thoại".RemoveDiacritics().ToLower().Replace(" ",""));
            
            if (normalized.Contains("ten") || normalized.Contains("hovaten") || normalized.Contains("fullname"))
                colMap["FullName"] = cell.Start.Column;
            if (normalized.Contains("ma") && (normalized.Contains("sinh vien")
                                              || normalized.Contains("sv")
                                              || normalized.Contains("mssv"))
                                                || normalized.Contains("masinhvien"))
                colMap["StudentCode"] = cell.Start.Column;
            if (normalized.Contains("email") || normalized.Contains("mail"))
                colMap["Email"] = cell.Start.Column;
            if (normalized.Contains("phone") 
                || normalized.Contains("dienthoai") 
                || normalized.Contains("sodienthoai")
                || normalized.Contains("sođienthoai")
                || normalized.Contains("sdt")
                || (normalized.Contains("dien") && normalized.Contains("thoai")) 
                || (normalized.Contains("so") && (normalized.Contains("dien") || 
                                                  normalized.Contains("dt"))))
            {
                colMap["Phone"] = cell.Start.Column;
                logger.LogInformation("  → Mapped to Phone");
            }
        }

        logger.LogInformation("Đã map {Count} cột: {Cols}", colMap.Count, string.Join(", ", colMap.Keys));

        //Kiểm tra cột bắt buộc
        if (!colMap.ContainsKey("FullName") || !colMap.ContainsKey("StudentCode") || !colMap.ContainsKey("Email")
            || !colMap.ContainsKey("Phone"))
        {
            var missing = new List<string>();
            if (!colMap.ContainsKey("FullName")) missing.Add("Họ và tên");
            if (!colMap.ContainsKey("StudentCode")) missing.Add("Mã sinh viên");
            if (!colMap.ContainsKey("Email")) missing.Add("Email");
            if (!colMap.ContainsKey("Phone")) missing.Add("Phone");
            
            logger.LogWarning("Thiếu cột: {Missing}", string.Join(", ", missing));
            return Json(new { success = false, message = $"Không tìm thấy cột bắt buộc: {string.Join(", ", missing)}" });
        }

        //Lấy role Student
        var studentRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Student");
        logger.LogInformation("Student Role ID: {RoleId}", studentRole?.Id);
        
        //Xử lý từng dòng
        for (int row = 2; row <= rowCount; row++)
        {
            totalProcessed++;
            
            var fullName = ws.Cells[row, colMap["FullName"]].GetValue<string>()?.Trim();
            var studentCode = ws.Cells[row, colMap["StudentCode"]].GetValue<string>()?.Trim();
            var email = ws.Cells[row, colMap["Email"]].GetValue<string>()?.Trim();
            var phone = colMap.ContainsKey("Phone") 
                ? ws.Cells[row, colMap["Phone"]].Value?.ToString()?.Trim() 
                : null;

            logger.LogInformation("Row {Row}: {FullName} - {StudentCode} - {Email}", row, fullName, studentCode, email);

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning("Row {Row}: Bỏ qua vì thiếu dữ liệu", row);
                continue;
            }

            var username = GenerateUsername(fullName, studentCode);
            logger.LogInformation("Generated username: {Username}", username);
            
            if (await db.Users.AnyAsync(u => u.Username == username))
            {
                logger.LogWarning("Username {Username} đã tồn tại", username);
                continue;
            }
            
            if (!string.IsNullOrEmpty(email) && await db.Users.AnyAsync(u => u.Email == email))
            {
                logger.LogWarning("Email {Email} đã tồn tại", email);
                continue;
            }
            
            if (!string.IsNullOrEmpty(phone) && await db.Users.AnyAsync(u => u.PhoneNumber == phone))
            {
                logger.LogWarning("Phone {Phone} đã tồn tại", phone);
                continue;
            }

            var password = GenerateRandomPassword(12);
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Email = email,
                PhoneNumber = phone,
                MustChangePassword = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddHours(7)
            };

            db.Users.Add(newUser);

            if (studentRole != null)
            {
                db.UserRoles.Add(new UserRole { UserId = newUser.Id, RoleId = studentRole.Id });
            }

            await db.SaveChangesAsync();
            successCount++;
            
            logger.LogInformation("Đã tạo user {Username} (#{Count})", username, successCount);
            
            if (!string.IsNullOrEmpty(email))
            {
                _ = SendLoginInfoToEmail(email, username, password);
            }

            // Realtime progress qua SignalR
            logger.LogInformation("Gửi SignalR progress: {Current}/{Total}", totalProcessed, rowCount - 1);
            await hubContext.Clients.All.SendAsync("ImportProgress", new
            {
                Current = totalProcessed,
                Total = rowCount - 1,
                Success = successCount,
                Username = username,
                Email = email ?? ""
            });
        }

        logger.LogInformation("KẾT THÚC IMPORT: {Success}/{Total}", successCount, totalProcessed);

        return Json(new
        {
            success = true,
            count = successCount,
            message = $"Import thành công {successCount} tài khoản!"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "LỖI IMPORT: {Message}", ex.Message);
        return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
    }
}


    // Helper: Tạo username từ tên + mã SV
    private string GenerateUsername(string fullName, string studentCode)
    {
        var cleanName = fullName.RemoveDiacritics().Trim();
        var parts = cleanName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return studentCode.ToLowerInvariant();

        // Tên chính (từ cuối cùng): Vinh → vinh
        var lastName = parts.Last();
            
        var middleName = string.Concat(parts.Take(parts.Length - 1).Select(p => p[0]));

        // Ghép lại: vinh + nt + bh01523 → vinhntbh01523
        var username = $"{lastName}{middleName}{studentCode}".ToLowerInvariant();

        // Loại bỏ mọi ký tự không phải chữ cái hoặc số
        return Regex.Replace(username, @"[^a-z0-9]", "");
    }

    // Helper: Tạo mật khẩu ngẫu nhiên
    private string GenerateRandomPassword(int length = 12)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";   
        const string lower = "abcdefghijkmnpqrstuvwxyz";    
        const string digits = "23456789";                 
        const string special = "!@#$%^&*_-+=";

        var random = new Random();
        var password = new char[length];
        var chars = new List<char>();
        
        password[0] = upper[random.Next(upper.Length)];
        password[1] = lower[random.Next(lower.Length)];
        password[2] = digits[random.Next(digits.Length)];
        password[3] = special[random.Next(special.Length)];
        
        var allChars = upper + lower + digits + special;
        for (int i = 4; i < length; i++)
        {
            password[i] = allChars[random.Next(allChars.Length)];
        }
        
        for (int i = password.Length - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }
    
    // GET: Users/Create
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> Create()
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentUserRoles = await db.GetUserRolesAsync(currentUserId);

        var availableRoles = await db.Roles.ToListAsync();
        
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
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền tạo user với các roles này
        if (!await userManagementService.CanCreateUserWithRolesAsync(currentUserId, model.RoleIds))
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
        var highAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "HighAdmin");
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
        if (await db.Users.AnyAsync(u => u.Username == model.Username))
        {
            ModelState.AddModelError("Username", "Username đã tồn tại.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }
        
        //Kiểm tra trùng emial
        if (!string.IsNullOrWhiteSpace(model.Email) && await db.Users.AnyAsync(u => u.Email == model.Email))
        {
            ModelState.AddModelError("Email", "Email is existed");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }
        
        //Kiển tra trùng sdt
        if (!string.IsNullOrWhiteSpace(model.PhoneNumber) &&
            await db.Users.AnyAsync(u => u.PhoneNumber == model.PhoneNumber))
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
                CreatedAt = DateTime.UtcNow.AddHours(7)
            };

            db.Users.Add(user);

            // Thêm roles
            foreach (var roleId in model.RoleIds)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId
                });
            }
            db.UserProfiles.Add(new UserProfile
            {
                UserId = user.Id,
                FullName = user.Username, // tạm dùng username
                AvatarUrl = null,
                Bio = null,
                Gender = null,
                BirthDate = null
            });


            await db.SaveChangesAsync();
            logger.LogInformation("User {UserId} created by {CurrentUserId}", user.Id, currentUserId);

            if (model.SendEmail && !string.IsNullOrEmpty(model.Email))
            {
                await SendLoginInfoToEmail(model.Email, model.Username,  model.Password);
            }
            
            TempData["SuccessMessage"] = "Tạo user thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating user");
            ModelState.AddModelError("", "Có lỗi xảy ra khi tạo user. Vui lòng thử lại.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            return View(model);
        }
    }

    public async Task SendLoginInfoToEmail(string email, string username, string password)
    {
        var apiKey = config["SendGrid:ApiKey"];
        var fromEmail = config["SendGrid:FromEmail"];
        var fromName = config["SendGrid:FromName"];

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
            logger.LogError("SendGrid lỗi {Status}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"SendGrid lỗi: {response.StatusCode}");
        }

        logger.LogInformation("Email Login Info gửi thành công đến {Email}",email);
    }
    
    // GET: Users/Edit
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id == null)
            return NotFound();

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await userManagementService.CanManageUserAsync(currentUserId, id.Value))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa user này.";
            return RedirectToAction(nameof(Index));
        }

        var user = await db.Users
            .Include(u => u.UserRoles!)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound();

        var currentUserRoles = await db.GetUserRolesAsync(currentUserId);
        var availableRoles = await db.Roles.ToListAsync();
        
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
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> Edit(Guid id, EditUserViewModel model)
    {
        if (id != model.Id)
            return NotFound();

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await userManagementService.CanManageUserAsync(currentUserId, id))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa user này.";
            return RedirectToAction(nameof(Index));
        }
        
        //it nhất 1 role
        if (model.RoleIds == null || !model.RoleIds.Any())
        {
            ModelState.AddModelError("RoleIds", "Phải chọn ít nhất một vai trò.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await db.GetUserRolesAsync(id);
            return View(model);
        }
        
        //HighAdmin chỉ 1 role
        var highAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "HighAdmin");
        if (highAdminRole != null && model.RoleIds.Contains(highAdminRole.Id))
        {
            if (model.RoleIds.Count > 1)
            {
                ModelState.AddModelError("RoleIds", "HighAdmin chỉ được có duy nhất role HighAdmin, không được có thêm role khác.");
                model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
                model.CurrentRoles = await db.GetUserRolesAsync(id);
                return View(model);
            }
        }
        
        // Kiểm tra quyền tạo user với các roles này
        if (!await userManagementService.CanCreateUserWithRolesAsync(currentUserId, model.RoleIds))
        {
            ModelState.AddModelError("", "Bạn không có quyền gán các vai trò đã chọn.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await db.GetUserRolesAsync(id);
            return View(model);
        }

        // Kiểm tra username đã tồn tại (trừ user hiện tại)
        if (await db.Users.AnyAsync(u => u.Username == model.Username && u.Id != id))
        {
            ModelState.AddModelError("Username", "Username đã tồn tại.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await db.GetUserRolesAsync(id);
            return View(model);
        }
        //Kiểm tra email
        if (!string.IsNullOrEmpty(model.Email) && await db.Users.AnyAsync(u => u.Email == model.Email && u.Id != id))
        {
            ModelState.AddModelError("Email", "Email is existed");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await db.GetUserRolesAsync(id);
            return View(model);
        }
        //kiểm tra sdt
        if (!string.IsNullOrEmpty(model.PhoneNumber) && await db.Users.AnyAsync(u => u.PhoneNumber == model.PhoneNumber && u.Id != id))
        {
            ModelState.AddModelError("PhoneNumber", "Phone Number is existed");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await db.GetUserRolesAsync(id);
            return View(model);
        }
        
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await db.GetUserRolesAsync(id);
            return View(model);
        }

        try
        {
            var user = await db.Users
                .Include(u => u.UserRoles!)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            user.Username = model.Username;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            user.MustChangePassword = model.MustChangePassword;
            user.IsActive = model.IsActive;
            user.UpdatedAt = DateTime.UtcNow.AddHours(7);

            // Cập nhật roles
            var currentRoleIds = user.UserRoles?.Select(ur => ur.RoleId).ToList() ?? new List<Guid>();
            
            // Xóa roles không còn trong danh sách
            var rolesToRemove = user.UserRoles?
                .Where(ur => !model.RoleIds.Contains(ur.RoleId))
                .ToList() ?? new List<UserRole>();
            
            foreach (var roleToRemove in rolesToRemove)
            {
                db.UserRoles.Remove(roleToRemove);
            }

            // Thêm roles mới
            var rolesToAdd = model.RoleIds
                .Where(rid => !currentRoleIds.Contains(rid))
                .ToList();
            foreach (var roleId in rolesToAdd)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = roleId
                });
            }

            await db.SaveChangesAsync();
            logger.LogInformation("User {UserId} updated by {CurrentUserId}", id, currentUserId);
            
            TempData["SuccessMessage"] = "Cập nhật user thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user {UserId}", id);
            ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật user. Vui lòng thử lại.");
            model.AvailableRoles = await GetAvailableRolesForCurrentUserAsync();
            model.CurrentRoles = await db.GetUserRolesAsync(id);
            return View(model);
        }
    }

    // GET: Users/Delete
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id == null)
            return NotFound();

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await userManagementService.CanManageUserAsync(currentUserId, id.Value))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xóa user này.";
            return RedirectToAction(nameof(Index));
        }

        var user = await db.Users
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
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await userManagementService.CanManageUserAsync(currentUserId, id))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xóa user này.";
            return RedirectToAction(nameof(Index));
        }
        try{
            // Revoke all tokens
            await userManagementService.RevokeAllTokensAsync(id);
            var user = await db.Users
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
                db.UserRoles.RemoveRange(user.UserRoles);
            }
            if (user.OtpCodes != null && user.OtpCodes.Any())
            {
                db.OtpCodes.RemoveRange(user.OtpCodes);
            }

            if (user.FaceProfile != null)
            {
                db.FaceProfiles.Remove(user.FaceProfile);
            }
            
            // _db.Users.Remove(user);
            // await _db.SaveChangesAsync();
            // Xóa Avatar trên cloud
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id)
                          ?? new UserProfile { UserId = user.Id };    
            var deleteResult = await avatarService.DeleteAvatar(user, profile, false);
            if (!deleteResult)
            {
                TempData["ErrorMessage"] = "Lỗi khi xóa Avatar";
            }
            //DÙNG STORED PROCEDURE
            await db.Database.ExecuteSqlRawAsync("EXEC usp_DeleteUser @userId", 
                new SqlParameter("@userId", id));
            logger.LogInformation("User {UserId} deleted by {CurrentUserId}", id, currentUserId);
            
            TempData["SuccessMessage"] = "Deleted user completed!";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error Deleting user {UserId}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa user. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Users/DisableUser
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> DisableUser([FromBody] RevokeTokensRequest request)
    {
        if (request == null || request.Id == Guid.Empty)
        {
            return Json(new { success = false, message = "Yêu cầu không hợp lệ." });
        }

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (!await userManagementService.CanManageUserAsync(currentUserId, request.Id))
        {
            return Json(new { success = false, message = "Bạn không có quyền vô hiệu hóa user này." });
        }

        try
        {
            var user = await db.Users.FindAsync(request.Id);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy user." });

            if (!user.IsActive)
                return Json(new { success = false, message = "User đã bị vô hiệu hóa." });

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow.AddHours(7);

            // Revoke all tokens
            await userManagementService.RevokeAllTokensAsync(request.Id);

            await db.SaveChangesAsync();
            logger.LogInformation("User {UserId} disabled by {CurrentUserId}", request.Id, currentUserId);
            return Json(new { success = true, message = "Vô hiệu hóa user thành công! User sẽ không thể đăng nhập." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disabling user {UserId}", request.Id);
            return Json(new { success = false, message = "Có lỗi xảy ra khi vô hiệu hóa user." });
        }
    }

    // POST: Users/EnableUser
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> EnableUser([FromBody] RevokeTokensRequest request)
    {
        if (request == null || request.Id == Guid.Empty)
        {
            return Json(new { success = false, message = "Yêu cầu không hợp lệ." });
        }

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (!await userManagementService.CanManageUserAsync(currentUserId, request.Id))
        {
            return Json(new { success = false, message = "Bạn không có quyền kích hoạt user này." });
        }

        try
        {
            var user = await db.Users.FindAsync(request.Id);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy user." });

            if (user.IsActive)
                return Json(new { success = false, message = "User đã hoạt động." });

            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow.AddHours(7);

            await db.SaveChangesAsync();
            logger.LogInformation("User {UserId} enabled by {CurrentUserId}", request.Id, currentUserId);
            return Json(new { success = true, message = "Kích hoạt user thành công!" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enabling user {UserId}", request.Id);
            return Json(new { success = false, message = "Có lỗi xảy ra khi kích hoạt user." });
        }
    }
    // POST: Users/ResetPassword
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (request == null || request.Id == Guid.Empty)
        {
            return Json(new { success = false, message = "Invalid request." });
        }

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await userManagementService.CanManageUserAsync(currentUserId, request.Id))
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

        var success = await userManagementService.ResetPasswordAsync(request.Id, request.NewPassword);
    
        if (success)
        {
            logger.LogInformation("Password reset for user {UserId} by {CurrentUserId}", request.Id, currentUserId);
            return Json(new { success = true, message = "Reset mật khẩu thành công!" });
        }

        return Json(new { success = false, message = "Có lỗi xảy ra khi reset mật khẩu." });
    }

    // POST: Users/RevokeTokens
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Authorize(Policy = "AdminOrHigher")]
    public async Task<IActionResult> RevokeTokens([FromBody] RevokeTokensRequest request)
    {
        if (request == null || request.Id == Guid.Empty)
        {
            return Json(new { success = false, message = "Invalid request." });
        }

        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra quyền quản lý user này
        if (!await userManagementService.CanManageUserAsync(currentUserId, request.Id))
        {
            return Json(new { success = false, message = "Bạn không có quyền revoke tokens cho user này." });
        }
        var targetUserRoles = await db.GetUserRolesAsync(request.Id);
        if (targetUserRoles.Contains("HighAdmin"))
        {
            return Json(new { success = false, message = "Không thể hủy tokens của HighAdmin." });
        }
        var success = await userManagementService.RevokeAllTokensAsync(request.Id);
        
        if (success)
        {
            logger.LogInformation("Tokens revoked for user {UserId} by {CurrentUserId}", request.Id, currentUserId);
            return Json(new { success = true, message = "Đã hủy tất cả tokens! User sẽ bị đăng xuất." });
        }

        return Json(new { success = false, message = "Có lỗi xảy ra khi revoke tokens." });
    }

    // Helper method
    private async Task<List<RoleOption>> GetAvailableRolesForCurrentUserAsync()
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var currentUserRoles = await db.GetUserRolesAsync(currentUserId);

        var availableRoles = await db.Roles.ToListAsync();
        
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

    #region Post Approval Management

    /// <summary>
    /// GET: Admin/ApprovePosts
    /// Hiển thị danh sách bài đăng chờ duyệt
    /// </summary>
    [Authorize(Policy = "ModeratorOrHigher")]
    public async Task<IActionResult> ApprovePosts()
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userRoles = await db.GetUserRolesAsync(currentUserId);

        // Chỉ HighAdmin và Moderator mới có quyền duyệt bài
        if (!userRoles.Contains("HighAdmin") && !userRoles.Contains("Moderator"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền truy cập trang này.";
            return RedirectToAction("Index");
        }

        var posts = await db.Posts
            .Include(p => p.User)
                .ThenInclude(u => u.UserProfile)
            .Include(p => p.Attachments)
            .Where(p => !p.IsDeleted && !p.IsVisible) // Chỉ hiển thị bài chờ duyệt
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var viewModels = posts.Select(p => new ViewModels.Admin.PendingPostViewModel
        {
            Id = p.Id,
            Title = p.Title,
            Content = p.Content.Length > 200 ? p.Content.Substring(0, 200) + "..." : p.Content,
            AuthorUsername = p.User.Username,
            AuthorId = p.UserId,
            CreatedAt = p.CreatedAt,
            AttachmentCount = p.Attachments.Count
        }).ToList();

        // Thống kê
        var statistics = new ViewModels.Admin.PostApprovalStatisticsViewModel
        {
            PendingCount = await db.Posts.CountAsync(p => !p.IsDeleted && !p.IsVisible),
            TotalCount = await db.Posts.CountAsync(p => !p.IsDeleted)
        };

        ViewBag.Statistics = statistics;

        return View(viewModels);
    }

    /// <summary>
    /// POST: Admin/ApprovePost/{id}
    /// Duyệt bài đăng
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ModeratorOrHigher")]
    public async Task<IActionResult> ApprovePost(Guid id)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userRoles = await db.GetUserRolesAsync(currentUserId);

        // Chỉ HighAdmin và Moderator mới có quyền duyệt
        if (!userRoles.Contains("HighAdmin") && !userRoles.Contains("Moderator"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền duyệt bài đăng.";
            return RedirectToAction(nameof(ApprovePosts));
        }

        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id);

        if (post == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy bài đăng.";
            return RedirectToAction(nameof(ApprovePosts));
        }

        try
        {
            post.IsVisible = true;
            post.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            logger.LogInformation("Post {PostId} approved by {UserId}", id, currentUserId);

            TempData["SuccessMessage"] = "Đã duyệt bài đăng thành công!";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error approving post {PostId}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi duyệt bài đăng.";
        }

        return RedirectToAction(nameof(ApprovePosts));
    }

    /// <summary>
    /// POST: Admin/RejectPost/{id}
    /// Từ chối bài đăng (soft delete)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ModeratorOrHigher")]
    public async Task<IActionResult> RejectPost(Guid id)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userRoles = await db.GetUserRolesAsync(currentUserId);

        // Chỉ HighAdmin và Moderator mới có quyền từ chối
        if (!userRoles.Contains("HighAdmin") && !userRoles.Contains("Moderator"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền từ chối bài đăng.";
            return RedirectToAction(nameof(ApprovePosts));
        }

        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id);

        if (post == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy bài đăng.";
            return RedirectToAction(nameof(ApprovePosts));
        }

        try
        {
            post.IsDeleted = true;
            post.IsVisible = false;
            post.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            logger.LogInformation("Post {PostId} rejected by {UserId}", id, currentUserId);

            TempData["SuccessMessage"] = "Đã từ chối bài đăng thành công!";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rejecting post {PostId}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi từ chối bài đăng.";
        }

        return RedirectToAction(nameof(ApprovePosts));
    }

    #endregion

    #region Report Management

    /// <summary>
    /// GET: Admin/Reports
    /// Hiển thị danh sách báo cáo với filter theo trạng thái
    /// </summary>
    [Authorize(Policy = "ModeratorOrHigher")]
    public async Task<IActionResult> Reports(string? status = null)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userRoles = await db.GetUserRolesAsync(currentUserId);

        // Chỉ HighAdmin và Moderator mới có quyền xử lý báo cáo
        if (!userRoles.Contains("HighAdmin") && !userRoles.Contains("Moderator"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền truy cập trang này.";
            return RedirectToAction("Index");
        }

        var query = db.PostReports
            .Include(r => r.Post)
            .Include(r => r.Reporter)
            .Include(r => r.Reviewer)
            .AsQueryable();

        // Filter theo trạng thái nếu có
        if (!string.IsNullOrEmpty(status) && status != "all")
        {
            query = query.Where(r => r.Status == status);
        }

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var viewModels = reports.Select(r => new ViewModels.Admin.ReportListViewModel
        {
            Id = r.Id,
            PostId = r.PostId,
            PostTitle = r.Post.Title,
            ReporterUsername = r.Reporter.Username,
            ReporterId = r.ReportedBy,
            Reason = r.Reason,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt,
            ReviewerUsername = r.Reviewer?.Username
        }).ToList();

        // Thống kê
        var statistics = new ViewModels.Admin.ReportStatisticsViewModel
        {
            PendingCount = await db.PostReports.CountAsync(r => r.Status == "Pending"),
            ApprovedCount = await db.PostReports.CountAsync(r => r.Status == "Approved"),
            RejectedCount = await db.PostReports.CountAsync(r => r.Status == "Rejected"),
            TotalCount = await db.PostReports.CountAsync()
        };

        ViewBag.Statistics = statistics;
        ViewBag.CurrentFilter = status ?? "all";

        return View(viewModels);
    }

    /// <summary>
    /// GET: Admin/ReportDetails/{id}
    /// Hiển thị chi tiết báo cáo
    /// </summary>
    [Authorize(Policy = "ModeratorOrHigher")]
    public async Task<IActionResult> ReportDetails(Guid id)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userRoles = await db.GetUserRolesAsync(currentUserId);

        // Chỉ HighAdmin và Moderator mới có quyền xem chi tiết báo cáo
        if (!userRoles.Contains("HighAdmin") && !userRoles.Contains("Moderator"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền truy cập trang này.";
            return RedirectToAction("Index");
        }

        var report = await db.PostReports
            .Include(r => r.Post)
                .ThenInclude(p => p.User)
            .Include(r => r.Reporter)
            .Include(r => r.Reviewer)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            return NotFound();
        }

        // Thống kê các báo cáo khác của cùng bài đăng
        var otherReports = await db.PostReports
            .Where(r => r.PostId == report.PostId)
            .ToListAsync();

        var viewModel = new ViewModels.Admin.ReportDetailsViewModel
        {
            Id = report.Id,
            PostId = report.PostId,
            PostTitle = report.Post.Title,
            PostContent = report.Post.Content,
            PostAuthorUsername = report.Post.User.Username,
            PostCreatedAt = report.Post.CreatedAt,
            PostIsDeleted = report.Post.IsDeleted,
            PostIsVisible = report.Post.IsVisible,
            ReporterId = report.ReportedBy,
            ReporterUsername = report.Reporter.Username,
            Reason = report.Reason,
            Status = report.Status,
            CreatedAt = report.CreatedAt,
            ReviewedBy = report.ReviewedBy,
            ReviewerUsername = report.Reviewer?.Username,
            ReviewedAt = report.ReviewedAt,
            PendingReportsCount = otherReports.Count(r => r.Status == "Pending"),
            ApprovedReportsCount = otherReports.Count(r => r.Status == "Approved"),
            RejectedReportsCount = otherReports.Count(r => r.Status == "Rejected"),
            TotalReportsCount = otherReports.Count
        };

        return View(viewModel);
    }

    /// <summary>
    /// POST: Admin/ApproveReport/{id}
    /// Duyệt báo cáo và có thể xóa bài đăng
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ModeratorOrHigher")]
    public async Task<IActionResult> ApproveReport(Guid id, bool deletePost = false)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userRoles = await db.GetUserRolesAsync(currentUserId);

        // Chỉ HighAdmin và Moderator mới có quyền duyệt
        if (!userRoles.Contains("HighAdmin") && !userRoles.Contains("Moderator"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền duyệt báo cáo.";
            return RedirectToAction(nameof(Reports));
        }

        var report = await db.PostReports
            .Include(r => r.Post)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy báo cáo.";
            return RedirectToAction(nameof(Reports));
        }

        if (report.Status != "Pending")
        {
            TempData["ErrorMessage"] = "Báo cáo này đã được xử lý rồi.";
            return RedirectToAction(nameof(ReportDetails), new { id });
        }

        try
        {
            // Cập nhật trạng thái báo cáo
            report.Status = "Approved";
            report.ReviewedBy = currentUserId;
            report.ReviewedAt = DateTime.UtcNow;

            // Nếu chọn xóa bài đăng, soft delete
            if (deletePost)
            {
                report.Post.IsDeleted = true;
                report.Post.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            logger.LogInformation("Report {ReportId} approved by {UserId}, deletePost: {DeletePost}", 
                id, currentUserId, deletePost);

            TempData["SuccessMessage"] = deletePost 
                ? "Đã duyệt báo cáo và xóa bài đăng thành công!" 
                : "Đã duyệt báo cáo thành công!";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error approving report {ReportId}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi duyệt báo cáo.";
        }

        return RedirectToAction(nameof(ReportDetails), new { id });
    }

    /// <summary>
    /// POST: Admin/RejectReport/{id}
    /// Từ chối báo cáo
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ModeratorOrHigher")]
    public async Task<IActionResult> RejectReport(Guid id)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userRoles = await db.GetUserRolesAsync(currentUserId);

        // Chỉ HighAdmin và Moderator mới có quyền từ chối
        if (!userRoles.Contains("HighAdmin") && !userRoles.Contains("Moderator"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền từ chối báo cáo.";
            return RedirectToAction(nameof(Reports));
        }

        var report = await db.PostReports.FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy báo cáo.";
            return RedirectToAction(nameof(Reports));
        }

        if (report.Status != "Pending")
        {
            TempData["ErrorMessage"] = "Báo cáo này đã được xử lý rồi.";
            return RedirectToAction(nameof(ReportDetails), new { id });
        }

        try
        {
            report.Status = "Rejected";
            report.ReviewedBy = currentUserId;
            report.ReviewedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            logger.LogInformation("Report {ReportId} rejected by {UserId}", id, currentUserId);

            TempData["SuccessMessage"] = "Đã từ chối báo cáo thành công!";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rejecting report {ReportId}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi từ chối báo cáo.";
        }

        return RedirectToAction(nameof(ReportDetails), new { id });
    }

    /// <summary>
    /// POST: Admin/DeleteReport/{id}
    /// Xóa báo cáo (chỉ HighAdmin và Moderator)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ModeratorOrHigher")]
    public async Task<IActionResult> DeleteReport(Guid id)
    {
        var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userRoles = await db.GetUserRolesAsync(currentUserId);

        // Chỉ HighAdmin và Moderator mới có quyền xóa
        if (!userRoles.Contains("HighAdmin") && !userRoles.Contains("Moderator"))
        {
            TempData["ErrorMessage"] = "Bạn không có quyền xóa báo cáo.";
            return RedirectToAction(nameof(Reports));
        }

        var report = await db.PostReports.FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy báo cáo.";
            return RedirectToAction(nameof(Reports));
        }

        try
        {
            db.PostReports.Remove(report);
            await db.SaveChangesAsync();
            logger.LogInformation("Report {ReportId} deleted by {UserId}", id, currentUserId);

            TempData["SuccessMessage"] = "Đã xóa báo cáo thành công!";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting report {ReportId}", id);
            TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa báo cáo.";
        }

        return RedirectToAction(nameof(Reports));
    }

    #endregion
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

public static class StringExtensions
{
    public static string RemoveDiacritics(this string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                stringBuilder.Append(c);
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}