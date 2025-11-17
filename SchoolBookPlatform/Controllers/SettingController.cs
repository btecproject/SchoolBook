using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Setting;
using SchoolBookPlatform.ViewModels.TwoFactorA;

namespace SchoolBookPlatform.Controllers;
[Authorize]
public class SettingController(
    ILogger<AuthenController> logger,
    TwoFactorService twoFactorService,
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
    public async Task<IActionResult> SettingChangePassword(SettingChangePasswordViewModel model,
        bool logoutOtherDevices = true)
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
                if (logoutOtherDevices)
                {
                    await tokenService.RevokeAllTokensAsync(userGuid);
                    logger.LogInformation("All tokens revoked for user {UserId}", userId);
                    TempData["success"] =  "Password changed successfully!, All other devices will be logged out!";
                }
                else
                {
                    TempData["success"] =  "Password changed successfully!";
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving new password for user {UserId}", userId);
                ModelState.AddModelError("", "Có lỗi xảy ra. Vui lòng thử lại.");
                TempData.Keep();
                return View(model);
            }
            return RedirectToAction("Index");
        }
        return View(model);
    }
    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : Guid.Empty;
    }
    
    [HttpGet]
    public async Task<IActionResult> EnableGoogleAuthenticator()
    {
        var userId = GetCurrentUserId();
        var user = await db.Users.FindAsync(userId);

        if (user == null)
        {
            return RedirectToAction("Login", "Authen");
        }
        if (user.TwoFactorEnabled == true)
        {
            TempData["error"] = "2FA đã được bật!";
            return RedirectToAction("Index");
        }

        var secret = twoFactorService.GenerateSecret();
        var qrCodeUri = twoFactorService.GenerateQrCodeUri(user.Username, secret);
        var qrCodeImage = twoFactorService.GenerateQrCodeImage(qrCodeUri);
        
        //Lưu secret tạm vào TempData
        //Chưa lưu vào database vì user chưa verify
        TempData["TwoFactorSecret"] = secret;

        var model = new TwoFactorSetupViewModel
        {
            QrCodeBase64 = qrCodeImage,
            ManualEntryKey = twoFactorService.FormatSecretKey(secret)
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableGoogleAuthenticator(TwoFactorSetupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData.Keep("TwoFactorSecret");
            return View(model);
        }
        
        var userId = GetCurrentUserId();
        var user = await db.Users.FindAsync(userId);
        var secret = TempData["TwoFactorSecret"]?.ToString();
        if (user == null || string.IsNullOrEmpty(secret))
        {
            TempData["error"] = "Phiên làm việc hết hạn. Vui lòng thử lại.";
            return RedirectToAction(nameof(EnableGoogleAuthenticator));
        }
        
        //Verify mã user nhập
        var isValid = twoFactorService.VerifyCode(secret, model.VerificationCode);
        
        if (!isValid)
        {
            // Giữ secret trong TempData để không phải tạo lại QR code
            TempData.Keep("TwoFactorSecret");
            
            ModelState.AddModelError("VerificationCode", "Mã xác thực không đúng");
            
            // Tạo lại QR code để hiển thị
            var qrCodeUri = twoFactorService.GenerateQrCodeUri(user.Username, secret);
            model.QrCodeBase64 = twoFactorService.GenerateQrCodeImage(qrCodeUri);
            model.ManualEntryKey = twoFactorService.FormatSecretKey(secret);
            
            return View(model);
        }
        
        //save và dtb
        user.TwoFactorEnabled = true;
        user.UpdatedAt = DateTime.UtcNow;
        user.TwoFactorSecret = secret;
        
        await db.SaveChangesAsync();
        logger.LogInformation("Google Authenticator enabled for user {UserId}", userId);
        TempData["success"] = "Đã bật Google Authenticator thành công!";
        return RedirectToAction("Index");
    }
    
    [HttpGet]
    public async Task<IActionResult> DisableGoogleAuthenticator()
    {
        var userId = GetCurrentUserId();
        var user = await db.Users.FindAsync(userId);
        if (user == null)
        {
            return RedirectToAction("Login", "Authen");
        }
        if (user.TwoFactorEnabled == false)
        {
            TempData["error"] = "2FA đã tắt!";
            return RedirectToAction("Index");
        }

        return View(new TwoFactorSetupViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableGoogleAuthenticator(TwoFactorSetupViewModel model)
    {
        var userId = GetCurrentUserId();
        var user = await db.Users.FindAsync(userId);
        
        if (user == null || user.TwoFactorEnabled == false|| string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            TempData["error"] = "2FA chưa được bật";
            return RedirectToAction("Index");
        }
        //xac thuc trc khi tawts
        var isValid = twoFactorService.VerifyCode( user.TwoFactorSecret,model.VerificationCode);
        if (!isValid)
        {
            TempData["error"] = "Mã xác thực không đúng";
            // return RedirectToAction("Index");
            return View(model);
        }
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.UpdatedAt = DateTime.UtcNow;
        
        await db.SaveChangesAsync();
        
        logger.LogInformation("Google Authenticator disabled for user {UserId}", userId);
        TempData["success"] = "Đã tắt Google Authenticator, sau khi tắt 2FA, " +
                              "entry \"SchoolBook\" vẫn sẽ hiển thị trong ứng dụng Google Authenticator. " +
                              "\nĐể bảo mật, vui lòng xóa entry này thủ công.";

        return RedirectToAction("Index");
    }
    
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}