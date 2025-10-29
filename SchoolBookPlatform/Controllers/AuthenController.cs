using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels;

namespace SchoolBookPlatform.Controllers;

public class AuthenController(
    ILogger<AuthenController> logger,
    TrustedService trustedService,
    AppDbContext db,
    OtpService otpService,
    TokenService tokenService,
    FaceService _faceService) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Home", "Feeds");
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            logger.LogError("Login Model invalid: " + string.Join(",", errors));
            return View(model);
        }

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == model.Username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Username or password is incorrect");
            return View(model);
        }

        if (user.MustChangePassword)
        {
            TempData["UserId"] = user.Id.ToString();
            return RedirectToAction(nameof(ChangePassword));
        }

        if (user.FaceRegistered)
        {
            TempData["UserId"] = user.Id.ToString();
            TempData["ReturnUrl"] = returnUrl;
            return RedirectToAction(nameof(FaceVerification));
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        var isTrustedDevice = await trustedService.IsTrustedAsync(user.Id, ipAddress, deviceInfo);

        if (!isTrustedDevice)
        {
            var otpType = model.OtpType;
            try
            {
                await otpService.GenerateOtpAsync(user, otpType);
                
                TempData["UserId"] = user.Id.ToString();
                TempData["OtpType"] = otpType;
                TempData["ReturnUrl"] = returnUrl;
                
                logger.LogInformation("OTP sent to user {UserId} via {Type}", user.Id, otpType);
                return RedirectToAction(nameof(VerifyOtp));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        await tokenService.SignInAsync(HttpContext, user);
        return LocalRedirect(returnUrl ?? Url.Action("Home", "Feeds"));
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        var userId = TempData.Peek("UserId")?.ToString();
        if (string.IsNullOrEmpty(userId)) return RedirectToAction(nameof(Login));
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData.Keep("UserId");
            return View(model);
        }

        var userId = TempData.Peek("UserId")?.ToString();
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            return RedirectToAction(nameof(Login));

        var user = await db.Users.FindAsync(userGuid);
        if (user == null) return RedirectToAction(nameof(Login));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        
        try
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Password changed successfully for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving new password for user {UserId}", userId);
            ModelState.AddModelError("", "Có lỗi xảy ra. Vui lòng thử lại.");
            TempData.Keep("UserId");
            return View(model);
        }

        if (user.FaceRegistered)
        {
            TempData["UserId"] = user.Id.ToString();
            return RedirectToAction(nameof(FaceVerification));
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var device = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        var isTrusted = await trustedService.IsTrustedAsync(user.Id, ip, device);
        
        if (!isTrusted)
        {
            await otpService.GenerateOtpAsync(user, "Email");
            TempData["UserId"] = user.Id.ToString();
            TempData["OtpType"] = "Email";
            return RedirectToAction(nameof(VerifyOtp));
        }

        await tokenService.SignInAsync(HttpContext, user);
        return RedirectToAction("Home", "Feeds");
    }

    [HttpGet]
    public IActionResult VerifyOtp()
    {
        var userId = TempData.Peek("UserId")?.ToString();
        var otpType = TempData.Peek("OtpType")?.ToString();
        
        logger.LogInformation("VerifyOtp GET - UserId: {UserId}, OtpType: {OtpType}", userId, otpType);
        
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(otpType))
        {
            logger.LogWarning("VerifyOtp GET failed - Missing TempData");
            return RedirectToAction(nameof(Login));
        }

        ViewData["OtpType"] = otpType;
        return View(new OtpViewModel { Type = otpType });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(OtpViewModel model)
    {
        logger.LogInformation("VerifyOtp POST - Code: {Code}, Type: {Type}", model.Code, model.Type);
        
        if (!ModelState.IsValid)
        {
            logger.LogWarning("VerifyOtp POST - ModelState invalid");
            var otpType = TempData.Peek("OtpType")?.ToString() ?? model.Type;
            ViewData["OtpType"] = otpType;
            model.Type = otpType;
            TempData.Keep("UserId");
            TempData.Keep("OtpType");
            TempData.Keep("ReturnUrl");
            return View(model);
            // ViewData["OtpType"] = model.Type;
            // TempData.Keep("UserId");
            // TempData.Keep("OtpType");
            // TempData.Keep("ReturnUrl");
            // return View(model);
        }

        var userId = TempData.Peek("UserId")?.ToString();
        logger.LogInformation("VerifyOtp POST - UserId from TempData: {UserId}", userId);
        
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            logger.LogError("VerifyOtp POST - UserId invalid or missing");
            return RedirectToAction(nameof(Login));
        }

        var user = await db.Users.FindAsync(userGuid);
        if (user == null)
        {
            logger.LogError("VerifyOtp POST - User not found: {UserId}", userGuid);
            return RedirectToAction(nameof(Login));
        }
        
        var isValid = await otpService.VerifyOtpAsync(user.Id, model.Code, model.Type);
        logger.LogInformation("OTP verification result: {IsValid} for user {UserId}", isValid, user.Id);
        
        if (!isValid)
        {
            ModelState.AddModelError("Code", "Mã OTP không đúng hoặc đã hết hạn.");
            var otpType = TempData.Peek("OtpType")?.ToString() ?? model.Type;
            ViewData["OtpType"] = otpType;
            model.Type = otpType;
            TempData.Keep("UserId");
            TempData.Keep("OtpType");
            TempData.Keep("ReturnUrl");
            return View(model);
        }
        
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var device = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        await trustedService.AddTrustedDeviceAsync(user.Id, ip, device);
        logger.LogInformation("Trusted device added for user {UserId}", user.Id);
        
        await tokenService.SignInAsync(HttpContext, user);
        logger.LogInformation("User {UserId} signed in successfully", user.Id);

        var returnUrl = TempData["ReturnUrl"]?.ToString() ?? Url.Action("Home", "Feeds");
        return LocalRedirect(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp()
    {
        var userIdStr = TempData.Peek("UserId")?.ToString();
        var otpType = TempData.Peek("OtpType")?.ToString();

        logger.LogInformation("ResendOtp - UserId: {UserId}, OtpType: {OtpType}", userIdStr, otpType);

        if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(otpType) ||
            !Guid.TryParse(userIdStr, out var userId))
        {
            logger.LogWarning("ResendOtp failed - Invalid TempData");
            return Json(new { success = false, message = "Phiên hết hạn. Vui lòng đăng nhập lại." });
        }

        var user = await db.Users.FindAsync(userId);
        if (user == null)
        {
            logger.LogWarning("ResendOtp failed - User not found: {UserId}", userId);
            return Json(new { success = false, message = "Không tìm thấy người dùng." });
        }

        try
        {
            await otpService.GenerateOtpAsync(user, otpType);
            logger.LogInformation("OTP resent successfully for user {UserId} via {Type}", userId, otpType);
            
            TempData.Keep("UserId");
            TempData.Keep("OtpType");
            TempData.Keep("ReturnUrl");

            return Json(new { success = true, message = "Gửi lại OTP thành công!" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resending OTP for user {UserId}", userId);
            return Json(new { success = false, message = "Không thể gửi OTP: " + ex.Message });
        }
    }

    [HttpGet]
    public IActionResult FaceVerification()
    {
        var userId = TempData["UserId"]?.ToString();
        if (string.IsNullOrEmpty(userId)) return RedirectToAction(nameof(Login));
        return View(new FaceVerificationViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task FaceVerification(FaceVerificationViewModel model)
    {
        // Implementation pending
    }

    [HttpPost] 
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await tokenService.SignOutAsync(HttpContext);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
