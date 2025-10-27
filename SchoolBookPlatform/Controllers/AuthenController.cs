using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Controllers
{
    public class AuthenController : Controller
    {
        private readonly AppDbContext _db;
        private readonly FaceService _faceService;
        private readonly TokenService _tokenService;

        public AuthenController(AppDbContext db, FaceService faceService, TokenService tokenService)
        {
            _db = db;
            _faceService = faceService;
            _tokenService = tokenService;
        }

        // ---------------- LOGIN ----------------
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View();
            }

            if (user.MustChangePassword)
            {
                TempData["UserId"] = user.Id.ToString();
                return RedirectToAction(nameof(ForceChangePassword));
            }

            // Nếu chưa có khuôn mặt thì yêu cầu đăng ký
            if (!user.FaceRegistered || string.IsNullOrEmpty(user.FaceId))
            {
                TempData["UserId"] = user.Id.ToString();
                return RedirectToAction(nameof(RegisterFace));
            }

            // Nếu đã có thì chuyển sang xác minh
            TempData["UserId"] = user.Id.ToString();
            return RedirectToAction(nameof(VerifyFace));
        }

        // ---------------- FORCE CHANGE PASSWORD ----------------
        [HttpGet]
        public IActionResult ForceChangePassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForceChangePassword(string newPassword)
        {
            if (!TempData.ContainsKey("UserId")) return RedirectToAction(nameof(Login));

            var userId = Guid.Parse(TempData["UserId"].ToString());
            var user = await _db.Users.FindAsync(userId);

            if (user == null) return NotFound();

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = false;
            await _db.SaveChangesAsync();

            TempData.Remove("UserId");
            return RedirectToAction(nameof(Login));
        }

        // ---------------- REGISTER FACE ----------------
        [HttpGet]
        public IActionResult RegisterFace() => View();

        [HttpPost]
        public async Task<IActionResult> RegisterFace(string faceImage)
        {
            var base64 = faceImage.Replace("data:image/png;base64,", "");
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);

            var faceId = await _faceService.DetectFaceAsync(ms);
            if (faceId == null)
            {
                ModelState.AddModelError("", "Không thể nhận diện khuôn mặt từ ảnh.");
                return View();
            }

            var userId = Guid.Parse(TempData["UserId"].ToString());
            var user = await _db.Users.FindAsync(userId);
            user.FaceId = faceId;
            user.FaceRegistered = true;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(VerifyFace));
        }

        // ---------------- VERIFY FACE ----------------
        [HttpGet]
        public IActionResult VerifyFace() => View();
        
        [HttpPost]
        public async Task<IActionResult> VerifyFace(string faceImage)
        {
            var base64 = faceImage.Replace("data:image/png;base64,", "");
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);

            var (isIdentical, confidence) = await _faceService.VerifyFaceAsync(ms, user.FaceId);

            if (isIdentical && confidence >= 0.4)
            {
                await _tokenService.SignInAsync(HttpContext,user);
                return RedirectToAction("Index", "Home");
            }
            ModelState.AddModelError("", $"Xác minh thất bại (độ tin cậy: {confidence:F2})");
            TempData.Keep("UserId");
            return View();
        }

        // ---------------- LOGOUT ----------------
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                await _tokenService.RevokeCurrentAsync(User);
                await HttpContext.SignOutAsync();
            }
            return RedirectToAction(nameof(Login));
        }
    }
}
