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
    public IActionResult VerifyTwoFactorForSetting()
    {
        var userId = TempData.Peek("SettingChangePasswordUserId")?.ToString();
        if (string.IsNullOrEmpty(userId))
        {
            TempData["error"] = "Phiên hết hạn. Vui lòng thử lại.";
            return RedirectToAction("Index");
        }

        return View(new TwoFactorVerifyViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTwoFactorForSetting(TwoFactorVerifyViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData.Keep();
            return View(model);
        }

        var userIdStr = TempData.Peek("SettingChangePasswordUserId")?.ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            TempData["error"] = "Phiên hết hạn. Vui lòng thử lại.";
            return RedirectToAction("Index");
        }

        var user = await db.Users.FindAsync(userId);
        if (user == null || user.TwoFactorEnabled == false || string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            TempData["error"] = "2FA chưa được bật";
            return RedirectToAction("Index");
        }
        
        var isValid = twoFactorService.VerifyCode(user.TwoFactorSecret, model.Code);
    
        if (!isValid)
        {
            ModelState.AddModelError("Code", "Mã xác thực không đúng hoặc đã hết hạn");
            TempData.Keep();
            return View(model);
        }

        logger.LogInformation("2FA verified for setting change for user {UserId}", userId);
        
        TempData["2FAVerifiedForSetting"] = "true";
        TempData["SettingChangePasswordVerified"] = "true";
        
        TempData.Keep();
        TempData["success"] = "Xác thực 2FA thành công! Bây giờ bạn có thể đổi mật khẩu.";
        return RedirectToAction("SettingChangePassword");
    }
    [HttpGet]
    public async Task<IActionResult> SettingChangePassword()
    {
        var userId = GetCurrentUserId();
        var user = await db.Users.FindAsync(userId);
        
        if (user == null)
            return RedirectToAction("Login", "Authen");
        
        if (user.TwoFactorEnabled == true && !string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            var is2FAVerified = TempData["2FAVerifiedForSetting"]?.ToString() == "true" 
                             || TempData["SettingChangePasswordVerified"]?.ToString() == "true";
            
            if (!is2FAVerified)
            {
                TempData["ReturnUrl"] = Url.Action(nameof(SettingChangePassword));
                TempData["SettingChangePasswordUserId"] = userId.ToString();
                TempData.Keep();
                
                return RedirectToAction("VerifyTwoFactorForSetting", "Setting");
            }
            else
            {
                ViewData["2FAVerified"] = true;
            }
        }

        return View(new SettingChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SettingChangePassword(SettingChangePasswordViewModel model, bool logoutOtherDevices = true)
    {
        if (!ModelState.IsValid)
        {
            TempData.Keep();
            TempData["error"] = "Lỗi trong quá trình đổi mật khẩu!";
            logger.LogError("Error on Model");
            return View(model);
        }

        var userId = GetCurrentUserId();
        var user = await db.Users.FindAsync(userId);
        
        if (user == null)
        {
            return RedirectToAction("Login", "Authen");
        }

        if (user.TwoFactorEnabled == true && !string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            var is2FAVerified = TempData["2FAVerifiedForSetting"]?.ToString() == "true" 
                             || TempData["SettingChangePasswordVerified"]?.ToString() == "true";
            
            if (!is2FAVerified)
            {
                TempData["ReturnUrl"] = Url.Action(nameof(SettingChangePassword));
                TempData["SettingChangePasswordUserId"] = userId.ToString();
                
                TempData["RecentPassword"] = model.RecentPassword;
                TempData["NewPassword"] = model.NewPassword;
                TempData["ConfirmPassword"] = model.ConfirmNewPassword;
                TempData["LogoutOtherDevices"] = logoutOtherDevices.ToString();
                TempData.Keep();
                
                return RedirectToAction("VerifyTwoFactorForSetting", "Setting");
            }
        }

        if (!string.IsNullOrEmpty(model.RecentPassword))
        {
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
                    await tokenService.RevokeAllTokensAsync(user.Id);
                    logger.LogInformation("All tokens revoked for user {UserId}", userId);
                    TempData["success"] = "Đổi mật khẩu thành công! Tất cả các thiết bị khác sẽ bị đăng xuất.";
                }
                else
                {
                    TempData["success"] = "Đổi mật khẩu thành công!";
                }
                
                TempData.Remove("2FAVerifiedForSetting");
                TempData.Remove("SettingChangePasswordVerified");
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