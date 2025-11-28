using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Chat;

namespace SchoolBookPlatform.Controllers
{
    [Authorize]
    public class ChatController(AppDbContext db, ChatService chatService, ILogger<ChatController> logger)
        : Controller
    {
        // GET: /Chat/Index - Kiểm tra và chuyển hướng
        public async Task<IActionResult> Index()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Authen");
            }

            // Kiểm tra user đã kích hoạt chat chưa
            var isChatActivated = await chatService.IsChatActivatedAsync(currentUser.Id);

            if (!isChatActivated)
            {
                // Chưa kích hoạt -> chuyển đến trang đăng ký
                return RedirectToAction("Register");
            }

            // Đã kích hoạt -> kiểm tra RSA key
            var rsaStatus = await chatService.CheckRsaKeyStatusAsync(currentUser.Id);
            
            if (!rsaStatus.IsValid)
            {
                // Key hết hạn hoặc không tồn tại
                TempData["RequireNewKey"] = true;
                TempData["KeyMessage"] = rsaStatus.Message;
            }

            // Chuyển đến trang chat chính
            return View("ChatRoom");
        }

        // GET: /Chat/Register - Trang đăng ký chat
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Authen");
            }

            // Nếu đã kích hoạt rồi thì chuyển về trang chat
            var isChatActivated = await chatService.IsChatActivatedAsync(currentUser.Id);
            if (isChatActivated)
            {
                return RedirectToAction("Index");
            }

            var model = new ChatRegistrationViewModel
            {
                Username = currentUser.Username
            };

            return View(model);
        }

        // POST: /Chat/Register - Xử lý đăng ký chat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(ChatRegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Authen");
            }

            try
            {
                // Đăng ký ChatUser với PinCodeHash
                var result = await chatService.RegisterChatUserAsync(
                    currentUser.Id,
                    currentUser.Username,
                    model.DisplayName,
                    model.PinCodeHash
                );

                if (!result.Success)
                {
                    ModelState.AddModelError("", result.Message);
                    return View(model);
                }

                logger.LogInformation("User {UserId} registered for chat successfully", currentUser.Id);
                
                // Chuyển sang trang để client tạo RSA key
                return RedirectToAction("SetupKeys");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error registering chat for user {UserId}", currentUser.Id);
                ModelState.AddModelError("", "Đã xảy ra lỗi khi đăng ký chat. Vui lòng thử lại.");
                return View(model);
            }
        }

        // GET: /Chat/SetupKeys - Trang để client tạo và upload RSA keys
        [HttpGet]
        public async Task<IActionResult> SetupKeys()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Authen");
            }

            // Kiểm tra đã đăng ký ChatUser chưa
            var isChatActivated = await chatService.IsChatActivatedAsync(currentUser.Id);
            if (!isChatActivated)
            {
                return RedirectToAction("Register");
            }

            // Kiểm tra đã có key chưa
            var hasValidKey = await chatService.HasValidRsaKeyAsync(currentUser.Id);
            if (hasValidKey)
            {
                return RedirectToAction("Index");
            }

            return View();
        }

        // POST: /Chat/UploadKeys - Nhận RSA keys từ client
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadKeys([FromBody] RsaKeysUploadModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            try
            {
                var result = await chatService.SaveUserRsaKeysAsync(
                    currentUser.Id,
                    model.PublicKey,
                    model.PrivateKeyEncrypted
                );

                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }

                logger.LogInformation("RSA keys uploaded successfully for user {UserId}", currentUser.Id);
                
                return Ok(new { message = "Keys uploaded successfully", redirectUrl = Url.Action("Index") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading RSA keys for user {UserId}", currentUser.Id);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lưu keys." });
            }
        }

        // API: /Chat/CheckActivation - Kiểm tra trạng thái kích hoạt
        [HttpGet]
        public async Task<IActionResult> CheckActivation()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var isActivated = await chatService.IsChatActivatedAsync(currentUser.Id);
            var rsaStatus = await chatService.CheckRsaKeyStatusAsync(currentUser.Id);

            return Ok(new
            {
                isActivated,
                hasValidKey = rsaStatus.IsValid,
                keyExpiry = rsaStatus.ExpiresAt,
                message = rsaStatus.Message
            });
        }
    }
}