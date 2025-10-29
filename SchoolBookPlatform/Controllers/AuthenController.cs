using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SchoolBookPlatform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;
using Twilio.Jwt.AccessToken;

namespace SchoolBookPlatform.Controllers
{
    public class AuthenController(
        ILogger<AuthenController> logger,
        TrustedService _trustedService,
        AppDbContext _db,
        OtpService _otpService,
        TokenService _tokenService,
        FaceService _faceService) : Controller
    {
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Home", "Feeds");
            }

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
                Console.Write("Login Model invalid: " + string.Join(",", errors));
                logger.LogError("Login Model invalid: " + string.Join(",", errors));
                return View(model);
            }

            var user = await _db.Users.Include(u => u.FaceProfile)
                .FirstOrDefaultAsync(u => u.Username == model.Username && u.IsActive);

            //sai mk or username
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
            
            //Ktra Ip/Tb lạ
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString()??"Unknown";
            var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString()??"Unknown";

            var isTrustedDevice = await _trustedService.IsTrustedAsync(user.Id, ipAddress, deviceInfo);

            if (!isTrustedDevice)
            {
                string otpType = model.OtpType;
                try
                {
                    await _otpService.GenerateOtpAsync(user, otpType);
                    TempData["UserId"] = user.Id.ToString();
                    TempData["OtpType"] = otpType;
                    TempData["ReturnUrl"] = returnUrl;
                    return RedirectToAction(nameof(VerifyOtp));
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                    return View(model);
                }
            }
            
            //login
            await _tokenService.SignInAsync(HttpContext, user);
            return LocalRedirect(returnUrl ?? Url.Action("Home", "Feeds"));
        }

        // Đổi Mk lần đầu
        [HttpGet]
        public IActionResult ChangePassword()
        {
            var userId = TempData.Peek("UserId")?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction(nameof(Login));
            }

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
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out Guid userGuid))
            {
                return RedirectToAction(nameof(Login));
            }

            var user = await _db.Users.FindAsync(userGuid);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            //update pw
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.MustChangePassword = false;
            user.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi lưu mật khẩu mới cho user {UserId}", userId);
                ModelState.AddModelError("", "Có lỗi xảy ra. Vui lòng thử lại.");
                TempData.Keep("UserId"); // Giữ lại khi lỗi
                return View(model);
            }

            //nếu có face
            if (user.FaceRegistered)
            {
                TempData["UserId"] = user.Id.ToString();
                return RedirectToAction(nameof(FaceVerification));
            }

            //Ip lạ || thiết bị mới
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var device = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";

            var isTrusted = await _trustedService.IsTrustedAsync(user.Id, ip, device);
            if (!isTrusted)
            {
                await _otpService.GenerateOtpAsync(user, "SMS");
                TempData["UserId"] = user.Id.ToString();
                TempData["OtpType"] = "SMS";
                return RedirectToAction(nameof(VerifyOtp));
            }

            //login
            await _tokenService.SignInAsync(HttpContext, user);
            return RedirectToAction("Home", "Feeds");
        }

        [HttpGet]
        public IActionResult VerifyOtp()
        {
            var userId = TempData["UserId"]?.ToString();
            var otpType = TempData["OtpType"]?.ToString();
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(otpType))
            {
                return RedirectToAction(nameof(Login));
            }

            ViewData["OtpType"] = otpType;
            return View(new OtpViewModel() { Type = otpType });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(OtpViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewData["OtpType"] = model.Type;
                return View(model);
            }

            var userId = TempData["UserId"]?.ToString();
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return RedirectToAction(nameof(Login)); // Phiên hết hạn
            }

            var user = await _db.Users.FindAsync(userGuid);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            // XÁC MINH OTP + CẬP NHẬT IsUsed
            var isValid = await _otpService.VerifyOtpAsync(user.Id, model.Code, model.Type);
            if (!isValid)
            {
                ModelState.AddModelError("Code", "Mã OTP không đúng hoặc đã hết hạn.");
                ViewData["OtpType"] = model.Type;
                TempData.Keep("UserId");
                TempData.Keep("OtpType");
                TempData.Keep("ReturnUrl");
                return View(model); // ← GIỮ TRANG OTP, KHÔNG VỀ LOGIN
            }
            
            // TỰ ĐỘNG THÊM THIẾT BỊ TIN CẬY
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var device = HttpContext.Request.Headers["User-Agent"].ToString() ?? "Unknown";
            await _trustedService.AddTrustedDeviceAsync(user.Id, ip, device);
            
            // ĐĂNG NHẬP THÀNH CÔNG
            await _tokenService.SignInAsync(HttpContext, user);

            var returnUrl = TempData["ReturnUrl"]?.ToString() ?? Url.Action("Home", "Feeds");
            return LocalRedirect(returnUrl);
        }
        
        //Gửi lại OTP
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp()
        {
            var userIdStr = TempData.Peek("UserId")?.ToString();
            var otpType = TempData.Peek("OtpType")?.ToString();

            if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(otpType) || 
                !Guid.TryParse(userIdStr, out var userId))
            {
                return Json(new { success = false, message = "Phiên hết hạn. Vui lòng đăng nhập lại." });
            }

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng." });
            }

            try
            {
                await _otpService.GenerateOtpAsync(user, otpType);
                logger.LogInformation("Gửi lại OTP thành công cho user {UserId} qua {Type}", userId, otpType);

                // Cập nhật lại TempData để giữ trạng thái
                TempData["UserId"] = userId.ToString();
                TempData["OtpType"] = otpType;

                return Json(new { success = true, message = "Gửi lại OTP thành công!" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi gửi lại OTP cho user {UserId}", userId);
                return Json(new { success = false, message = "Không thể gửi OTP: " + ex.Message });
            }
        }
        
        // GET: /Authen/FaceVerification
        [HttpGet]
        public IActionResult FaceVerification()
        {
            var userId = TempData["UserId"]?.ToString();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction(nameof(Login));
            }
            return View(new FaceVerificationViewModel());
        }
        
        //Xác Minh khuôn mặt (chưa test)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task FaceVerification(FaceVerificationViewModel model)
        {
            // var userId = TempData["UserId"]?.ToString();
            // if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            // {
            //     return RedirectToAction(nameof(Login));
            // }
            //
            // var user = await _db.Users.Include(u => u.FaceProfile).FirstOrDefaultAsync(u => u.Id == userGuid);
            // if (user == null || !user.FaceRegistered)
            // {
            //     return RedirectToAction(nameof(Login));
            // }
            //
            // // Gọi FaceService để xác minh khuôn mặt
            // var verificationResult = await _faceService.VerifyFaceAsync(user.FaceProfile.PersonId, model.ImageData);
            // if (verificationResult.IsSuccess && verificationResult.Confidence >= 0.6)
            // {
            //     user.FaceProfile.LastVerifiedAt = DateTime.UtcNow;
            //     user.FaceProfile.ConfidenceLast = verificationResult.Confidence;
            //     await _db.SaveChangesAsync();
            //
            //     // Kiểm tra thiết bị/IP mới
            //     var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            //     var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString();
            //     var isNewDevice = !await _db.UserTokens
            //         .AnyAsync(t => t.UserId == user.Id && t.IPAddress == ipAddress && t.DeviceInfo == deviceInfo && !t.IsRevoked);
            //
            //     if (isNewDevice)
            //     {
            //         var otpCode = await _otpService.GenerateOtpAsync(user, "Email");
            //         TempData["UserId"] = user.Id.ToString();
            //         TempData["OtpType"] = "Email";
            //         TempData["ReturnUrl"] = TempData["ReturnUrl"]?.ToString();
            //         return RedirectToAction(nameof(VerifyOtp));
            //     }
            //
            //     // Đăng nhập người dùng
            //     await _tokenService.SignInAsync(HttpContext, user);
            //     var returnUrl = TempData["ReturnUrl"]?.ToString() ?? Url.Action("Index", "Home");
            //     return LocalRedirect(returnUrl);
            // }
            //
            // // Nếu xác minh khuôn mặt thất bại, yêu cầu OTP
            // var fallbackOtp = await _otpService.GenerateOtpAsync(user, "Email");
            // TempData["UserId"] = user.Id.ToString();
            // TempData["OtpType"] = "Email";
            // TempData["ReturnUrl"] = TempData["ReturnUrl"]?.ToString();
            // ModelState.AddModelError("", "Xác minh khuôn mặt thất bại. Vui lòng xác minh bằng OTP.");
            // return RedirectToAction(nameof(VerifyOtp));
        }
    
        //logout
        [HttpGet]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _tokenService.SignOutAsync(HttpContext);
            return RedirectToAction("Index", "Home");
        }
        
        //access denied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}