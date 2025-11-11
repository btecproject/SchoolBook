using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels;

namespace SchoolBookPlatform.Controllers;

public class AuthenController : Controller
{
    private readonly ILogger<AuthenController> _logger;
    private readonly TrustedService _trustedService;
    private readonly AppDbContext _db;
    private readonly OtpService _otpService;
    private readonly TokenService _tokenService;
    private readonly FaceService _faceService;

    public AuthenController(
        ILogger<AuthenController> logger,
        TrustedService trustedService,
        AppDbContext db,
        OtpService otpService,
        TokenService tokenService,
        FaceService faceService)
    {
        _logger = logger;
        _trustedService = trustedService;
        _db = db;
        _otpService = otpService;
        _tokenService = tokenService;
        _faceService = faceService;
    }
    
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Home", "Feeds");
        
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == model.Username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản đã bị vô hiệu hóa");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        return await ProcessUserLoginAsync(user, returnUrl);
    }
    
    private async Task<IActionResult> ProcessUserLoginAsync(Models.User user, string returnUrl)
    {
        // Kiểm tra MustChangePassword
        if (user.MustChangePassword)
        {
            TempData["UserId"] = user.Id.ToString();
            TempData["ReturnUrl"] = returnUrl;
            _logger.LogInformation("User {UserId} must change password", user.Id);
            return RedirectToAction(nameof(ChangePassword));
        }

        // Kiểm tra Face verification
        if (user.FaceRegistered)
        {
            TempData["UserId"] = user.Id.ToString();
            TempData["ReturnUrl"] = returnUrl;
            _logger.LogInformation("User {UserId} requires face verification", user.Id);
            return RedirectToAction(nameof(FaceVerification));
        }

        // Kiểm tra trusted device
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        var isTrustedDevice = await _trustedService.IsTrustedAsync(user.Id, ipAddress, deviceInfo);

        if (!isTrustedDevice)
        {
            try
            {
                await _otpService.GenerateOtpAsync(user, "Email");
                
                TempData["UserId"] = user.Id.ToString();
                TempData["OtpType"] = "Email";
                TempData["ReturnUrl"] = returnUrl;
                
                _logger.LogInformation("OTP sent to user {UserId} via Email", user.Id);
                return RedirectToAction(nameof(VerifyOtp));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Failed to generate OTP for user {UserId}", user.Id);
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Login));
            }
        }

        // Sign in user
        await _tokenService.SignInAsync(HttpContext, user, _db);
        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return LocalRedirect(returnUrl ?? Url.Action("Home", "Feeds"));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult VerifyOtp()
    {
        var userId = TempData.Peek("UserId")?.ToString();
        var otpType = TempData.Peek("OtpType")?.ToString();
        
        _logger.LogInformation("VerifyOtp GET - UserId: {UserId}, OtpType: {OtpType}", userId, otpType);
        
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(otpType))
        {
            _logger.LogWarning("VerifyOtp GET failed - Missing TempData");
            return RedirectToAction(nameof(Login));
        }

        ViewData["OtpType"] = otpType;
        return View(new OtpViewModel { Type = otpType });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(OtpViewModel model)
    {
        _logger.LogInformation("VerifyOtp POST - Code: {Code}, Type: {Type}", model.Code, model.Type);
        
        if (!ModelState.IsValid)
        {
            var otpType = TempData.Peek("OtpType")?.ToString() ?? model.Type;
            ViewData["OtpType"] = otpType;
            model.Type = otpType;
            TempData.Keep();
            return View(model);
        }

        var userId = TempData.Peek("UserId")?.ToString();
        
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogError("VerifyOtp POST - UserId invalid or missing");
            return RedirectToAction(nameof(Login));
        }

        var user = await _db.Users.FindAsync(userGuid);
        if (user == null)
        {
            _logger.LogError("VerifyOtp POST - User not found: {UserId}", userGuid);
            return RedirectToAction(nameof(Login));
        }
        
        var isValid = await _otpService.VerifyOtpAsync(user.Id, model.Code, model.Type);
        _logger.LogInformation("OTP verification result: {IsValid} for user {UserId}", isValid, user.Id);
        
        if (!isValid)
        {
            ModelState.AddModelError("Code", "Mã OTP không đúng hoặc đã hết hạn.");
            var otpType = TempData.Peek("OtpType")?.ToString() ?? model.Type;
            ViewData["OtpType"] = otpType;
            model.Type = otpType;
            TempData.Keep();
            return View(model);
        }
        
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var device = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        await _trustedService.AddTrustedDeviceAsync(user.Id, ip, device);
        _logger.LogInformation("Trusted device added for user {UserId}", user.Id);
        
        await _tokenService.SignInAsync(HttpContext, user, _db);
        _logger.LogInformation("User {UserId} signed in successfully", user.Id);

        var returnUrl = TempData["ReturnUrl"]?.ToString() ?? Url.Action("Home", "Feeds");
        return LocalRedirect(returnUrl);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp()
    {
        var userIdStr = TempData.Peek("UserId")?.ToString();
        var otpType = TempData.Peek("OtpType")?.ToString();

        _logger.LogInformation("ResendOtp - UserId: {UserId}, OtpType: {OtpType}", userIdStr, otpType);

        if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(otpType) ||
            !Guid.TryParse(userIdStr, out var userId))
        {
            _logger.LogWarning("ResendOtp failed - Invalid TempData");
            return Json(new { success = false, message = "Phiên hết hạn. Vui lòng đăng nhập lại." });
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("ResendOtp failed - User not found: {UserId}", userId);
            return Json(new { success = false, message = "Không tìm thấy người dùng." });
        }

        try
        {
            await _otpService.GenerateOtpAsync(user, otpType);
            _logger.LogInformation("OTP resent successfully for user {UserId} via {Type}", userId, otpType);
            
            TempData.Keep();

            return Json(new { success = true, message = "Gửi lại OTP thành công!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending OTP for user {UserId}", userId);
            return Json(new { success = false, message = "Không thể gửi OTP: " + ex.Message });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ChangePassword()
    {
        var userId = TempData.Peek("UserId")?.ToString();
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Login));
        
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData.Keep();
            return View(model);
        }

        var userId = TempData.Peek("UserId")?.ToString();
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            return RedirectToAction(nameof(Login));

        var user = await _db.Users.FindAsync(userGuid);
        if (user == null)
            return RedirectToAction(nameof(Login));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        
        try
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("Password changed successfully for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving new password for user {UserId}", userId);
            ModelState.AddModelError("", "Có lỗi xảy ra. Vui lòng thử lại.");
            TempData.Keep();
            return View(model);
        }

        var returnUrl = TempData.Peek("ReturnUrl")?.ToString();

        if (user.FaceRegistered)
        {
            TempData["UserId"] = user.Id.ToString();
            TempData["ReturnUrl"] = returnUrl;
            return RedirectToAction(nameof(FaceVerification));
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var device = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        var isTrusted = await _trustedService.IsTrustedAsync(user.Id, ip, device);
        
        if (!isTrusted)
        {
            await _otpService.GenerateOtpAsync(user, "Email");
            TempData["UserId"] = user.Id.ToString();
            TempData["OtpType"] = "Email";
            TempData["ReturnUrl"] = returnUrl;
            return RedirectToAction(nameof(VerifyOtp));
        }

        await _tokenService.SignInAsync(HttpContext, user, _db);
        return LocalRedirect(returnUrl ?? Url.Action("Home", "Feeds"));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult FaceVerification()
    {
        var userId = TempData.Peek("UserId")?.ToString();
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Login));
        
        return View(new FaceVerificationViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FaceVerification(FaceVerificationViewModel model)
    {
        // TODO: Implement face verification logic
        await Task.CompletedTask;
        return RedirectToAction(nameof(Login));
    }
    

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _tokenService.SignOutAsync(HttpContext);
        _logger.LogInformation("User logged out successfully");
        return RedirectToAction("Index", "Home");
    }
    
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}