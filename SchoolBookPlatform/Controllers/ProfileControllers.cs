using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManagementService _userManagementService;
    private readonly ILogger<ProfileController> _logger;
    private readonly CloudinaryService _cloudinaryService;

    public ProfileController(
        AppDbContext db,
        UserManagementService userManagementService,
        ILogger<ProfileController> logger,
        CloudinaryService cloudinaryService)
    {
        _db = db;
        _userManagementService = userManagementService;
        _logger = logger;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return NotFound();
        var viewModel = new ProfileViewModel
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            CreatedAt = user.CreatedAt,
            // From UserProfile
            AvatarUrl = user.Profile?.AvatarUrl,
            FullName = user.Profile?.FullName,
            Bio = user.Profile?.Bio,
            Gender = user.Profile?.Gender,
            BirthDate = user.Profile?.BirthDate
        };
        ViewData["Title"] = $"Hồ sơ của {user.Username}";
        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateField([FromBody] UpdateFieldRequest req)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return NotFound();
        switch (req.Field)
        {
            case "FullName":
                user.Profile!.FullName = req.Value;
                break;
            case "Gender":
                user.Profile!.Gender = req.Value;
                break;
            case "Bio":
                user.Profile!.Bio = req.Value;
                break;
            case "PhoneNumber":
                user.PhoneNumber = req.Value;
                break;
            case "BirthDate":
                if (DateTime.TryParse(req.Value, out var date))
                    user.Profile!.BirthDate = date;
                break;
            default:
                return BadRequest("Invalid field");
        }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> UploadAvatar(IFormFile avatar)
    {
        if (avatar == null || avatar.Length == 0)
            return Json(new { success = false, message = "Không có tệp tin được chọn." });

        // Kiểm tra kích thước (giới hạn 5MB)
        if (avatar.Length > 5 * 1024 * 1024)
            return Json(new { success = false, message = "Kích thước tệp vượt quá 5MB." });

        // Kiểm tra loại tệp (chỉ cho phép JPEG/PNG)
        var allowedContentTypes = new[] { "image/jpeg", "image/png" };
        if (!allowedContentTypes.Contains(avatar.ContentType.ToLower()))
            return Json(new { success = false, message = "Loại tệp không hợp lệ. Chỉ cho phép JPEG và PNG." });

        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return Json(new { success = false, message = "Không tìm thấy người dùng." });

        try
        {
            // --- Xóa ảnh cũ nếu tồn tại ---
            if (!string.IsNullOrEmpty(user.Profile?.AvatarUrl))
            {
                try
                {
                    var uri = new Uri(user.Profile.AvatarUrl);
                    
                    // Lấy phần sau "upload/" trong URL để xác định publicId
                    var pathAfterUpload = uri.AbsolutePath.Split("/upload/").ElementAtOrDefault(1);
                    if (!string.IsNullOrEmpty(pathAfterUpload))
                    {
                        var publicId = pathAfterUpload.Substring(0, pathAfterUpload.LastIndexOf('.'));
                        if (!string.IsNullOrEmpty(publicId))
                        {
                            _logger.LogInformation("Public ID to delete: {PublicId}", publicId);
                            var deleteResult = await _cloudinaryService.DeleteImageAsync(publicId);
                            _logger.LogInformation("Delete result: {Status}", deleteResult?.StatusCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể xóa ảnh cũ, nhưng vẫn tiếp tục upload ảnh mới.");
                }
            }

            // --- Tải lên ảnh mới ---
            var avatarUrl = await _cloudinaryService.UploadImageAsync(avatar);

            // Cập nhật URL vào hồ sơ người dùng
            user.Profile!.AvatarUrl = avatarUrl;
            await _db.SaveChangesAsync();

            return Json(new { success = true, url = user.Profile.AvatarUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình tải lên ảnh đại diện");
            return Json(new { success = false, message = "Đã xảy ra lỗi trong quá trình tải lên: " + ex.Message });
        }
    }


    public class UpdateFieldRequest
    {
        public string Field { get; set; } = "";
        public string Value { get; set; } = "";
    }
}