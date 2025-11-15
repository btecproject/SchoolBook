using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Setting;

namespace SchoolBookPlatform.Controllers;
[Authorize]
public class SettingController(
    ILogger<AuthenController> logger,
    TrustedService trustedService,
    AppDbContext db,
    OtpService otpService,
    TokenService tokenService,
    FaceService faceService) : Controller
{
    [HttpGet]
    public IActionResult SettingChangePassword()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login", "Authen");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SettingChangePassword(SettingChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData.Keep();
            TempData["error"] = "Error on changing password!";
            logger.LogError("Error on Model");
            return View(model);
        }
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            return RedirectToAction("Login", "Authen");
        if(!string.IsNullOrEmpty(model.RecentPassword)){
            var user = await db.Users.FindAsync(userGuid);
            if (user == null)
            {
                return RedirectToAction("Login", "Authen");
            }

            if (!BCrypt.Net.BCrypt.Verify(model.RecentPassword, user.PasswordHash))
            {
                TempData["error"] = "Mật khẩu hiện tại không đúng!";
                return View(model);
            }

            user.UpdatedAt = DateTime.UtcNow;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            try
            {
                await db.SaveChangesAsync();
                logger.LogInformation("Password changed successfully for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving new password for user {UserId}", userId);
                ModelState.AddModelError("", "Có lỗi xảy ra. Vui lòng thử lại.");
                TempData.Keep();
                return View(model);
            }
            TempData["success"] = "Password changed successfully!";
            return RedirectToAction("Index");
        }
        return View(model);
    }
    
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}