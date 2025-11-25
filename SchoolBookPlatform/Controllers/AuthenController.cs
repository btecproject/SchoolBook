using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Authen;
using SchoolBookPlatform.ViewModels.TwoFactorA;

namespace SchoolBookPlatform.Controllers;

public class AuthenController(
    ILogger<AuthenController> logger,
    TrustedService trustedService,
    AppDbContext db,
    RecoveryCodeService recoveryCodeService,
    OtpService otpService,
    TokenService tokenService,
    FaceService faceService)
    : Controller
{
    private readonly FaceService _faceService = faceService;

    public async Task GoogleLogin()
    {
        await HttpContext.ChallengeAsync(GoogleDefaults.AuthenticationScheme,
            new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleResponse"),
                Items =
                {
                    { "prompt", "select_account" }
                }
            });
    }

    public async Task<IActionResult> GoogleResponse()
    {
        try
        {
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            if (result?.Succeeded != true || result?.Principal == null)
            {
                TempData["error"] = "Login with  Google failed";
                return RedirectToAction(nameof(Login));
            }

            var claims = result.Principal.Identities.FirstOrDefault().Claims;
            if (claims == null)
            {
                TempData["error"] = "Cannot get claims from Google";
                return RedirectToAction(nameof(Login));
            }

            var email = claims.FirstOrDefault(c =>
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value;
            if (string.IsNullOrEmpty(email))
            {
                TempData["error"] = "Cannot get email from Google account";
                return RedirectToAction(nameof(Login));
            }

            var isUserExisted = await db.IsUserEmailExistAsync(email);
            if (!isUserExisted)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                logger.LogWarning("Google login failed - Email not registered: {Email}", email);
                TempData["error"] = "Email(Account) not registered!.";
                return RedirectToAction(nameof(Login));
            }

            var user = await db.GetUserByEmailAsync(email);
            if (user != null && !user.IsActive)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                logger.LogWarning("Google login failed - Account disabled: {Email}", email);
                TempData["error"] = "Tài khoản đã bị vô hiệu hóa";
                return RedirectToAction(nameof(Login));
            }

            logger.LogInformation("User {UserId} authenticated via Google successfully", user.Id);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return await ProcessUserLoginAsync(user, null);
            // TempData["success"] = "Login completed";
            // return RedirectToAction("Home", "Feeds");
            // return Json(claims);
        }
        catch (Exception ex)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            logger.LogError(ex, "GoogleResponse Error");
            TempData["error"] = "Có lỗi xảy ra trong quá trình đăng nhập. Vui lòng thử lại.";
            return RedirectToAction(nameof(Login));
        }
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
        try
        {
            if (!ModelState.IsValid)
            {
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Username == model.Username);
            if (user != null)
            {
                logger.LogInformation("User loaded - TwoFactorEnabled: {0}, RecoveryCodesGenerated: {1}", 
                    user.TwoFactorEnabled, user.RecoveryCodesGenerated);
            }
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
        catch (Exception ex)
        {
            logger.LogError("Login failed: "+ex.Message);
        }
        return RedirectToAction(nameof(Login));
    }

    private async Task<IActionResult> ProcessUserLoginAsync(Models.User user, string? returnUrl)
    {
        // Kiểm tra MustChangePassword
        if (user.MustChangePassword)
        {
            TempData["UserId"] = user.Id.ToString();
            TempData["ReturnUrl"] = returnUrl;
            logger.LogInformation("User {UserId} must change password", user.Id);
            return RedirectToAction(nameof(ChangePassword));
        }
        // Kiểm tra 2FA
        if (user.TwoFactorEnabled == true && !string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            TempData["UserId"] = user.Id.ToString();
            TempData["ReturnUrl"] = returnUrl;
            logger.LogInformation("User {UserId} requires 2FA", user.Id);
            return RedirectToAction(nameof(VerifyTwoFactor));
        }
        // Kiểm tra Face verification
        if (user.FaceRegistered)
        {
            TempData["UserId"] = user.Id.ToString();
            TempData["ReturnUrl"] = returnUrl;
            logger.LogInformation("User {UserId} requires face verification", user.Id);
            return RedirectToAction(nameof(FaceVerification));
        }

        // Kiểm tra trusted device
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        var isTrustedDevice = await trustedService.IsTrustedAsync(user.Id, ipAddress, deviceInfo);

        if (!isTrustedDevice)
        {
            try
            {
                await otpService.GenerateOtpAsync(user, "Email");

                TempData["UserId"] = user.Id.ToString();
                TempData["OtpType"] = "Email";
                TempData["ReturnUrl"] = returnUrl;

                logger.LogInformation("OTP sent to user {UserId} via Email", user.Id);
                return RedirectToAction(nameof(VerifyOtp));
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Failed to generate OTP for user {UserId}", user.Id);
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Login));
            }
        }

        // Sign in user
        await tokenService.SignInAsync(HttpContext, user, db);
        logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return LocalRedirect((returnUrl ?? Url.Action("Home", "Feeds"))!);
    }
    [HttpGet]
    [AllowAnonymous]
    public IActionResult VerifyTwoFactor()
    {
        var userId = TempData.Peek("UserId")?.ToString();
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Login));

        return View(new TwoFactorVerifyViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTwoFactor(TwoFactorVerifyViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData.Keep();
            return View(model);
        }

        var userIdStr = TempData.Peek("UserId")?.ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return RedirectToAction(nameof(Login));

        var user = await db.Users.FindAsync(userId);
        if (user == null || user.TwoFactorEnabled==false || string.IsNullOrEmpty(user.TwoFactorSecret))
            return RedirectToAction(nameof(Login));
        
        // DEBUG: Log secret key và code
        logger.LogInformation("2FA Verification - User: {UserId}, Secret: {Secret}, Code: {Code}", 
            userId, user.TwoFactorSecret, model.Code);
        //Verify
        var twoFactorService = HttpContext.RequestServices.GetRequiredService<TwoFactorService>();
        var isValid = twoFactorService.VerifyCode(user.TwoFactorSecret, model.Code);
        
        if (!isValid)
        {
            ModelState.AddModelError("Code", "Mã xác thực không đúng hoặc đã hết hạn");
            TempData.Keep();
            return View(model);
        }

        logger.LogInformation("2FA verified for user {UserId}", userId);
        
        var returnUrl = TempData.Peek("ReturnUrl")?.ToString();
        
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var device = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        var isTrusted = await trustedService.IsTrustedAsync(user.Id, ip, device);

        if (!isTrusted)
        {
            await otpService.GenerateOtpAsync(user, "Email");
            TempData["UserId"] = user.Id.ToString();
            TempData["OtpType"] = "Email";
            TempData["ReturnUrl"] = returnUrl;
            return RedirectToAction(nameof(VerifyOtp));
        }
        
        await tokenService.SignInAsync(HttpContext, user, db);
        return LocalRedirect(returnUrl ?? Url.Action("Home", "Feeds"));
    }
    
    
    [HttpGet]
    [AllowAnonymous]
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
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(OtpViewModel model)
    {
        logger.LogInformation("VerifyOtp POST - Code: {Code}, Type: {Type}", model.Code, model.Type);

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
            TempData.Keep();
            return View(model);
        }
        
        var returnUrl = TempData["ReturnUrl"]?.ToString() ?? Url.Action("Home", "Feeds");
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var device = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        await trustedService.AddTrustedDeviceAsync(user.Id, ip, device);
        logger.LogInformation("Trusted device added for user {UserId}", user.Id);

        await tokenService.SignInAsync(HttpContext, user, db);
        logger.LogInformation("User {UserId} signed in successfully", user.Id);
        return LocalRedirect(returnUrl);
    }

    [HttpPost]
    [AllowAnonymous]
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

            TempData.Keep();

            return Json(new { success = true, message = "Gửi lại OTP thành công!" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resending OTP for user {UserId}", userId);
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

        var user = await db.Users.FindAsync(userGuid);
        if (user == null || user.MustChangePassword == false)
            return RedirectToAction(nameof(Login));
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
            TempData.Keep();
            return View(model);
        }

        var returnUrl = TempData.Peek("ReturnUrl")?.ToString();
        
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var device = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        var isTrusted = await trustedService.IsTrustedAsync(user.Id, ip, device);
        
        if (!isTrusted)
        {
            await otpService.GenerateOtpAsync(user, "Email");
            TempData["UserId"] = user.Id.ToString();
            TempData["OtpType"] = "Email";
            TempData["ReturnUrl"] = returnUrl;
            return RedirectToAction(nameof(VerifyOtp));
        }

        await tokenService.RevokeAllTokensAsync(userGuid);
        await tokenService.SignInAsync(HttpContext, user, db);
        return LocalRedirect(returnUrl ?? Url.Action("Home", "Feeds"));
    }
    
    [HttpGet]
    [AllowAnonymous]
    public IActionResult LoginWithRecoveryCode()
    {
        var userId = TempData.Peek("UserId")?.ToString();
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction("Login");
        var model =  new RecoveryCodeViewModel();
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginWithRecoveryCode(RecoveryCodeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData.Keep();
            return View(model);
        }

        var userIdStr = TempData.Peek("UserId")?.ToString();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return RedirectToAction("Login");
        logger.LogInformation("User {UserId} logged in with Recovery Code", userId);
        
        var user = await db.Users.FindAsync(userId);
        if (user == null || user.TwoFactorEnabled==false)
            return RedirectToAction("Login");
        if(model.RecoveryCode == null) return View(model);
        logger.LogInformation("User logged in with Recovery Code: "+ model.RecoveryCode);
        
        //Validate recovery code
        var isValid = await recoveryCodeService.VerifyCodeAsync(user.Id, model.RecoveryCode!);
        logger.LogInformation("Verifying");
        if (!isValid)
        {
            ModelState.AddModelError("recoveryCode", "Mã khôi phục không đúng hoặc đã sử dụng");
            logger.LogInformation("Wrong Recovery Code or Code is used!");
            return View();
        }

        user.RecoveryCodesLeft = await recoveryCodeService.GetRemainingCountAsync(user.Id);
        logger.LogInformation("Login completed with code: {RecoveryCode}", model.RecoveryCode);
        logger.LogInformation("Remaining: "+user.RecoveryCodesLeft+" codes");
        await db.SaveChangesAsync();

        logger.LogWarning("User {UserId} logged in using Recovery Code", user.Id);

        await tokenService.SignInAsync(HttpContext,user,db);
        TempData["success"] = "Đăng nhập bằng mã khôi phục thành công! Mã vừa dùng đã bị xóa.";
        return RedirectToAction("Home", "Feeds");
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
        // await Task.CompletedTask;
        return RedirectToAction(nameof(Login));
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await tokenService.SignOutAsync(HttpContext);
        logger.LogInformation("User logged out successfully");
        return RedirectToAction("Index", "Home");
    }
    
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
    
}