using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                return RedirectToAction("SetupKeys");
            }

            // Chuyển đến trang chat chính
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> VerifyPinCode()
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
            return View();
        }
        
        // POST: xác thực mã Pin
        [HttpPost]
        public async Task<IActionResult> VerifyPinCode([FromBody] string pinCodeHash)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }
            
            if (string.IsNullOrWhiteSpace(pinCodeHash))
            {
                return Json(new { success = false, message = "Mã PIN không hợp lệ" });
            }
            
            logger.LogInformation("VerifyPinCode for user {Username} with hash: {Hash}", 
                currentUser.Username, pinCodeHash);
            
            try
            {
                // Tìm ChatUser với UserId và PinCodeHash khớp
                var chatUser = await db.ChatUsers
                    .FirstOrDefaultAsync(u => u.UserId == currentUser.Id && u.PinCodeHash == pinCodeHash);
                
                if (chatUser == null)
                {
                    logger.LogWarning("PIN verification failed for user {Username}", currentUser.Username);
                    return Json(new { success = false, message = "Mã PIN không đúng" });
                }
                
                logger.LogInformation("PIN verified successfully for user {Username}", currentUser.Username);
                return Json(new { success = true, message = "Xác thực thành công" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error verifying PIN for user {Username}", currentUser.Username);
                return Json(new { success = false, message = "Đã xảy ra lỗi khi xác thực" });
            }
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
                return RedirectToAction("VerifyPinCode");
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
                return RedirectToAction("VerifyPinCode");
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

        // API: /Chat/SearchUsers - Tìm kiếm user
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 3)
            {
                return BadRequest(new { message = "Search term must be at least 3 characters" });
            }

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            try
            {
                var results = await chatService.SearchChatUsersAsync(searchTerm, currentUser.Id);
                return Ok(results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error searching users with term: {SearchTerm}", searchTerm);
                return StatusCode(500, new { message = "Error searching users" });
            }
        }

        // API: /Chat/GetUserPublicKey - Lấy public key của user
        [HttpGet]
        public async Task<IActionResult> GetUserPublicKey(Guid userId)
        {
            try
            {
                var publicKey = await chatService.GetUserPublicKeyAsync(userId);

                if (string.IsNullOrEmpty(publicKey))
                {
                    return NotFound(new { message = "User không có public key hoặc key đã hết hạn" });
                }

                return Ok(new { publicKey });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting public key for user: {UserId}", userId);
                return StatusCode(500, new { message = "Error getting public key" });
            }
        }

        // API: /Chat/GetOrCreateConversation - Lấy hoặc tạo conversation 1-1
        [HttpPost]
        public async Task<IActionResult> GetOrCreateConversation(Guid recipientId)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            try
            {
                var result = await chatService.GetOrCreateConversationAsync(currentUser.Id, recipientId);

                return Ok(new
                {
                    conversationId = result.ConversationId,
                    isNew = result.IsNew,
                    hasPinExchange = result.HasPinExchange
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting/creating conversation between {User1} and {User2}",
                    currentUser.Id, recipientId);
                return StatusCode(500, new { message = "Error creating conversation" });
            }
        }

        // API: /Chat/SendPinExchange - Gửi PIN exchange message
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPinExchange([FromBody] PinExchangeModel model)
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
                var result = await chatService.SendPinExchangeMessageAsync(
                    model.ConversationId,
                    currentUser.Id,
                    model.RecipientId,
                    model.EncryptedPin
                );

                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }

                return Ok(new { message = "PIN exchange sent successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending PIN exchange");
                return StatusCode(500, new { message = "Error sending PIN exchange" });
            }
        }

        // API: /Chat/GetMessages - Lấy tin nhắn
        [HttpGet]
        public async Task<IActionResult> GetMessages(Guid conversationId, int count = 20)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            try
            {
                var messages = await chatService.GetMessagesAsync(conversationId, currentUser.Id, count);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting messages for conversation: {ConversationId}", conversationId);
                return StatusCode(500, new { message = "Error loading messages" });
            }
        }

        // API: /Chat/SendMessage - Gửi tin nhắn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageModel model)
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
                var result = await chatService.SendMessageAsync(
                    model.ConversationId,
                    currentUser.Id,
                    model.CipherText,
                    model.MessageType
                );

                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }

                return Ok(new { messageId = result.Data, message = "Message sent successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { message = "Error sending message" });
            }
        }

        // API: /Chat/UploadFile - Upload file attachment
        [HttpPost]
        [RequestSizeLimit(52428800)] // 50MB
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] Guid conversationId)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "File không hợp lệ" });
            }

            try
            {
                // Validate file type
                var allowedExtensions = new[]
                {
                    ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
                    ".mp4", ".avi", ".mov", ".wmv", ".flv", ".webm",
                    ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                    ".txt", ".zip", ".rar"
                };

                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new { message = "Loại file không được hỗ trợ" });
                }

                // Create temporary message
                var result = await chatService.CreateMessageAsync(
                    conversationId,
                    currentUser.Id,
                    "[Uploading...]",
                    CloudinaryService.GetMessageType(file.FileName)
                );

                if (!result.Success)
                {
                    return BadRequest(new { message = result.Message });
                }

                var messageId = result.Data;

                // Upload to Cloudinary
                var uploadResult = await chatService.UploadFileAsync(
                    file,
                    currentUser.Id,
                    conversationId,
                    messageId
                );

                if (!uploadResult.Success)
                {
                    await chatService.DeleteMessageAsync(messageId);
                    return BadRequest(new { message = uploadResult.Message });
                }

                return Ok(new
                {
                    messageId,
                    url = uploadResult.Url,
                    fileName = uploadResult.FileName,
                    fileSize = uploadResult.FileSize,
                    resourceType = uploadResult.ResourceType,
                    format = uploadResult.Format
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new { message = "Error uploading file" });
            }
        }
    }
    // Request Models
    public class PinExchangeModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid ConversationId { get; set; }
        
        [System.ComponentModel.DataAnnotations.Required]
        public Guid RecipientId { get; set; }
        
        [System.ComponentModel.DataAnnotations.Required]
        public string EncryptedPin { get; set; } = string.Empty;
    }

    public class SendMessageModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid ConversationId { get; set; }
        
        [System.ComponentModel.DataAnnotations.Required]
        public string CipherText { get; set; } = string.Empty;
        
        [System.ComponentModel.DataAnnotations.Required]
        public byte MessageType { get; set; }
    }
}