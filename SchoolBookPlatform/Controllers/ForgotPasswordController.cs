using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Authen;
using SchoolBookPlatform.ViewModels.ForgotPassword;

/*       if (user.TwoFactorEnabled == true && !string.IsNullOrEmpty(user.TwoFactorSecret))
{
    TempData["ReturnUrl"] = Url.Action("ChangePassword", "Authen");
    return RedirectToAction(nameof(VerifyTwoFactor));
}
*/
namespace SchoolBookPlatform.Controllers;

public class ForgotPasswordController(
    ILogger<ForgotPasswordController> logger,
    TokenService tokenService,
    AppDbContext db,
    OtpService otpService) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Index()
    {
        return View();
    }
    
    
    // [HttpPost]
    // [AllowAnonymous]
    // [ValidateAntiForgeryToken]
    // public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    // {
    //     if (!ModelState.IsValid)
    //     {
    //         TempData["error"] = "Please enter a valid email address";
    //         return View(model);
    //     }
    //     var user = await db.GetUserByEmailAsync(model.Email);
    //     if (user == null)
    //     {
    //         TempData["error"] = "Email address not found";
    //         return View(model);
    //     }
    //
    //     try
    //     {
    //         await otpService.GenerateOtpAsync(user, "Email");
    //         TempData["UserId"] = user.Id.ToString();
    //         TempData["OtpType"] = "Email";
    //         TempData["ReturnUrl"] = Url.Action("ChangePassword", "Authen");
    //         logger.LogInformation("OTP sent to user {UserId} via Email", user.Id);
    //         return RedirectToAction(nameof(VerifyOtp));
    //     }
    //     catch (Exception ex)
    //     {
    //         logger.LogError(ex, "Error resending OTP for user {UserId}", user.Id);
    //     }
    //     return View(model);
    // }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["error"] = "Please enter a valid email address";
            return View(model);
        }

        var user = await db.GetUserByEmailAsync(model.Email);
        if (user == null)
        {
            TempData["success"] = "Nếu email tồn tại, chúng tôi đã gửi mã xác thực";
            return View();
        }

        if (!user.IsActive)
        {
            TempData["error"] = "Tài khoản đã bị vô hiệu hóa";
            return View(model);
        }

        try
        {
            await otpService.GenerateOtpAsync(user, "Email");
            
            TempData["ForgotPasswordUserId"] = user.Id.ToString();
            TempData["OtpType"] = "Email";
            logger.LogInformation("OTP sent for password reset to user {UserId}", user.Id);
            TempData["success"] = "Đã gửi mã xác thực đến email của bạn";
            return RedirectToAction(nameof(VerifyOtp));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending OTP for password reset for user {UserId}", user.Id);
            TempData["error"] = "Không thể gửi mã xác thực. Vui lòng thử lại sau.";
            return View(model);
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult VerifyOtp()
    {
        var userId = TempData.Peek("ForgotPasswordUserId")?.ToString();
        var otpType = TempData.Peek("OtpType")?.ToString();

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(otpType))
        {
            TempData["error"] = "Phiên hết hạn. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["OtpType"] = otpType;
        return View(new OtpViewModel { Type = otpType });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(OtpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var otpType = TempData.Peek("OtpType")?.ToString() ?? model.Type;
            ViewData["OtpType"] = otpType;
            model.Type = otpType;
            TempData.Keep();
            return View(model);
        }

        var userId = TempData.Peek("ForgotPasswordUserId")?.ToString();

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            TempData["error"] = "Phiên hết hạn. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }

        var user = await db.Users.FindAsync(userGuid);
        if (user == null)
        {
            TempData["error"] = "Người dùng không tồn tại";
            return RedirectToAction(nameof(Index));
        }

        var isValid = await otpService.VerifyOtpAsync(user.Id, model.Code, model.Type);
        
        if (!isValid)
        {
            ModelState.AddModelError("Code", "Mã OTP không đúng hoặc đã hết hạn.");
            var otpType = TempData.Peek("OtpType")?.ToString() ?? model.Type;
            ViewData["OtpType"] = otpType;
            model.Type = otpType;
            TempData.Keep();
            return View(model);
        }

        TempData["ResetPasswordUserId"] = user.Id.ToString();
        logger.LogInformation("OTP verified for password reset for user {UserId}", user.Id);
        
        return RedirectToAction(nameof(ResetPassword));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword()
    {
        var userId = TempData.Peek("ResetPasswordUserId")?.ToString();
        if (string.IsNullOrEmpty(userId))
        {
            TempData["error"] = "Phiên hết hạn. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }

        return View(new ResetPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData.Keep();
            return View(model);
        }

        var userId = TempData.Peek("ResetPasswordUserId")?.ToString();
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            TempData["error"] = "Phiên hết hạn. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }

        var user = await db.Users.FindAsync(userGuid);
        if (user == null)
        {
            TempData["error"] = "Người dùng không tồn tại";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.MustChangePassword = false;
            user.UpdatedAt = DateTime.UtcNow;
            // user.TokenVersion++;
            await tokenService.RevokeAllTokensAsync(user.Id);

            await db.SaveChangesAsync();

            logger.LogInformation("Password reset successfully for user {UserId}", user.Id);
            TempData["success"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại.";

            return RedirectToAction("Login", "Authen");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting password for user {UserId}", user.Id);
            TempData["error"] = "Có lỗi xảy ra khi đặt lại mật khẩu. Vui lòng thử lại.";
            TempData.Keep();
            return View(model);
        }
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp()
    {
        var userIdStr = TempData.Peek("ForgotPasswordUserId")?.ToString();
        var otpType = TempData.Peek("OtpType")?.ToString();

        if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(otpType) ||
            !Guid.TryParse(userIdStr, out var userId))
        {
            return Json(new { success = false, message = "Phiên hết hạn. Vui lòng thử lại." });
        }

        var user = await db.Users.FindAsync(userId);
        if (user == null)
        {
            return Json(new { success = false, message = "Không tìm thấy người dùng." });
        }

        try
        {
            await otpService.GenerateOtpAsync(user, otpType);
            logger.LogInformation("OTP resent for password reset for user {UserId}", userId);

            TempData.Keep();
            return Json(new { success = true, message = "Đã gửi lại mã OTP thành công!" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resending OTP for password reset for user {UserId}", userId);
            return Json(new { success = false, message = "Không thể gửi lại OTP: " + ex.Message });
        }
    }
}