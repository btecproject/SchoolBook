using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.DTOs;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.Chat;

namespace SchoolBookPlatform.Controllers
{
    [Authorize]
    public class ChatController(AppDbContext db, ChatService chatService, ILogger<ChatController> logger)
        : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitializeKeys([FromBody] InitializeConversationKeyRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            // Service sẽ tự map currentUser.Id -> ChatUserId
            var result = await chatService.InitializeConversationKeysAsync(currentUser.Id, request);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new { message = "Keys initialized successfully" });
        }

        [HttpGet]
        public async Task<IActionResult> GetConversationKey(Guid conversationId, int version = 1)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            // Lấy key theo version
            var encryptedKey = await chatService.GetConversationKeyAsync(currentUser.Id, conversationId, version);

            if (string.IsNullOrEmpty(encryptedKey))
            {
                // Trả về metadata để client biết cần init lại nếu cần
                return NotFound(new { message = "Key not found", needInit = true });
            }

            return Ok(new { encryptedKey });
        }
        
        [HttpPost]
        public async Task<IActionResult> SaveConversationKey([FromBody] ConversationKeyModel model)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var result = await chatService.SaveConversationKeyAsync(
                    currentUser.Id, 
                    model.ConversationId, 
                    model.EncryptedKey, 
                    model.KeyVersion
                );

                if (!result)
                {
                    return BadRequest(new { message = "Could not save key (User not in conversation or inactive)" });
                }

                return Ok(new { message = "Key saved successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving conversation key");
                return StatusCode(500, new { message = "Error saving key" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetOrCreateConversation(Guid recipientId)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            try
            {
                // RecipientId ở đây là UserId gốc (từ search/contact list)
                // Service sẽ tự tìm ChatUserId active của cả 2
                var result = await chatService.GetOrCreateConversationAsync(currentUser.Id, recipientId);

                return Ok(new
                {
                    conversationId = result.ConversationId,
                    isNew = result.IsNew,
                    isKeyInitialized = result.IsKeyInitialized // Tên mới chuẩn
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating conversation with user {RecipientId}", recipientId);
                return StatusCode(500, new { message = ex.Message }); // Trả về message lỗi (ví dụ: người kia chưa kích hoạt)
            }
        }
        
        [HttpGet]
        [EnableRateLimiting("ChatPolicy")]
        public async Task<IActionResult> GetRecentContacts()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized(new {message = "User not authenticated"});

            try
            {
                var contacts = await chatService.GetRecentContactsAsync(currentUser.Id);
                return Ok(contacts);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting recent contacts");
                return StatusCode(500, new { message = "Error loading contacts" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(Guid conversationId, int count = 20, long? beforeId = null)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            try
            {
                var messages = await chatService.GetMessagesAsync(conversationId, currentUser.Id, count, beforeId);
                return Ok(messages);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting messages for conversation: {ConversationId}", conversationId);
                return StatusCode(500, new { message = "Error loading messages" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead([FromBody] Guid senderUserId)
        {
            // Frontend gửi UserId gốc của sender -> Service cần map sang ChatUserId để update noti
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();
             await chatService.MarkMessagesAsReadAsync(currentUser.Id, senderUserId);
            
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("ChatPolicy")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

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


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMessageWithFile([FromBody] UpdateMessageFileModel model)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var message = await db.Messages.FindAsync(model.MessageId);
                if (message == null) return NotFound(new { message = "Message không tồn tại" });

                // Check quyền: Phải tìm ChatUserId của currentUser trước
                var myChatUser = await db.ChatUsers.FirstOrDefaultAsync(cu => cu.UserId == currentUser.Id && cu.IsActive);
                if (myChatUser == null) return Unauthorized();

                // SenderId trong Message bây giờ là ChatUserId
                if (message.SenderId != myChatUser.Id) 
                {
                    return Forbid();
                }

                message.CipherText = model.EncryptedUrl;
                await db.SaveChangesAsync();

                logger.LogInformation("Message {MessageId} updated with encrypted file URL", model.MessageId);
                return Ok(new { message = "Message updated successfully" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating message {MessageId} with file", model.MessageId);
                return StatusCode(500, new { message = "Lỗi khi cập nhật message" });
            }
        }

        [HttpPost]
        [RequestSizeLimit(52428800)] // 50MB
        [EnableRateLimiting("ChatPolicy")]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] Guid conversationId)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            if (file == null || file.Length == 0) return BadRequest(new { message = "File không hợp lệ" });

            try
            {
                // Validate extension ... (Giữ nguyên)
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif",
                    ".bmp", ".webp", ".mp4", ".avi", 
                    ".mov", ".wmv", ".flv", ".webm", 
                    ".pdf", ".doc", ".docx", ".xls", 
                    ".xlsx", ".ppt", ".pptx", ".txt", 
                    ".zip", ".rar" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension)) return BadRequest(new { message = "Định dạng file không được hỗ trợ" });

                // Create message (Service tự handle ChatUserId)
                var result = await chatService.CreateMessageAsync(
                    conversationId,
                    currentUser.Id,
                    "[Uploading...]",
                    CloudinaryService.GetMessageType(file.FileName)
                );

                if (!result.Success) return BadRequest(new { message = result.Message });

                var messageId = result.Data;

                // Upload
                var uploadResult = await chatService.UploadFileAsync(file, currentUser.Id, conversationId, messageId);

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
        

        [HttpGet]
        public async Task<IActionResult> GetCurrentUserInfo()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            // Lấy Active ChatUser
            var chatUser = await db.ChatUsers
                .Where(cu => cu.UserId == currentUser.Id && cu.IsActive)
                .Select(cu => new
                {
                    userId = cu.UserId, // Trả về UserId gốc để client dùng đồng nhất
                    chatUserId = cu.Id, // Trả thêm ChatUserId nếu client cần debug
                    username = cu.Username,
                    displayName = cu.DisplayName
                })
                .FirstOrDefaultAsync();

            if (chatUser == null)
            {
                return NotFound(new { message = "ChatUser không tồn tại hoặc chưa kích hoạt" });
            }

            return Ok(chatUser);
        }

        [HttpGet]
        public async Task<IActionResult> GetMyPrivateKey()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            try
            {
                // Lấy Active ChatUser -> Lấy Key của ChatUser đó
                var chatUser = await db.ChatUsers.FirstOrDefaultAsync(cu => cu.UserId == currentUser.Id && cu.IsActive);
                if (chatUser == null) return NotFound(new { message = "Chưa kích hoạt chat" });

                var rsaKey = await db.UserRsaKeys
                    .Where(k => k.ChatUserId == chatUser.Id && k.IsActive) // Dùng ChatUserId
                    .Select(k => new
                    {
                        privateKeyEncrypted = k.PrivateKeyEncrypted,
                        expiresAt = k.ExpiresAt
                    })
                    .FirstOrDefaultAsync();

                if (rsaKey == null) return NotFound(new { message = "Không tìm thấy khóa RSA" });

                if (rsaKey.expiresAt < DateTime.UtcNow.AddHours(7))
                    return BadRequest(new { message = "Khóa RSA đã hết hạn." });

                return Ok(new { privateKeyEncrypted = rsaKey.privateKeyEncrypted });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting private key");
                return StatusCode(500, new { message = "Lỗi server" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserPublicKey(Guid userId)
        {
            // UserId ở đây là UserId gốc (từ search/contact)
            try
            {
                var publicKey = await chatService.GetUserPublicKeyAsync(userId); // Service tự map sang Active ChatUser

                if (string.IsNullOrEmpty(publicKey))
                {
                    return NotFound(new { message = "User không có public key hoặc chưa kích hoạt chat" });
                }

                return Ok(new { publicKey });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting public key for user: {UserId}", userId);
                return StatusCode(500, new { message = "Error getting public key" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchUsers(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 3)
                return BadRequest(new { message = "Search term must be at least 3 characters" });

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            try
            {
                var results = await chatService.SearchChatUsersAsync(searchTerm, currentUser.Id);
                return Ok(results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error searching users");
                return StatusCode(500, new { message = "Error searching users" });
            }
        }
        

        public async Task<IActionResult> Index()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return RedirectToAction("Login", "Authen");

            var isChatActivated = await chatService.IsChatActivatedAsync(currentUser.Id);
            if (!isChatActivated) return RedirectToAction("Register");

            var rsaStatus = await chatService.CheckRsaKeyStatusAsync(currentUser.Id);
            if (!rsaStatus.IsValid)
            {
                TempData["RequireNewKey"] = true;
                TempData["KeyMessage"] = rsaStatus.Message;
                return RedirectToAction("SetupKeys");
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return RedirectToAction("Login", "Authen");

            if (await chatService.IsChatActivatedAsync(currentUser.Id))
                return RedirectToAction("VerifyPinCode");

            return View(new ChatRegistrationViewModel { Username = currentUser.Username });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(ChatRegistrationViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return RedirectToAction("Login", "Authen");

            try
            {
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

                logger.LogInformation("User {UserId} registered for chat", currentUser.Id);
                return RedirectToAction("SetupKeys");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error registering chat");
                ModelState.AddModelError("", "Lỗi đăng ký.");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> SetupKeys()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return RedirectToAction("Login", "Authen");

            if (!await chatService.IsChatActivatedAsync(currentUser.Id))
                return RedirectToAction("Register");

            if (await chatService.HasValidRsaKeyAsync(currentUser.Id))
                return RedirectToAction("VerifyPinCode");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadKeys([FromBody] RsaKeysUploadModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            try
            {
                var result = await chatService.SaveUserRsaKeysAsync(
                    currentUser.Id,
                    model.PublicKey,
                    model.PrivateKeyEncrypted
                );

                if (!result.Success) return BadRequest(new { message = result.Message });

                return Ok(new { message = "Keys uploaded successfully", redirectUrl = Url.Action("Index") });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading keys");
                return StatusCode(500, new { message = "Lỗi lưu keys" });
            }
        }
        

        [HttpGet]
        public async Task<IActionResult> VerifyPinCode()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return RedirectToAction("Login", "Authen");

            if (!await chatService.IsChatActivatedAsync(currentUser.Id))
                return RedirectToAction("Register");

            return View();
        }

        [HttpPost]
        [EnableRateLimiting("ChatPinPolicy")]
        public async Task<IActionResult> VerifyPinCode([FromBody] string pinCodeHash)
        {
            return await VerifyPinInternal(pinCodeHash);
        }

        [HttpPost("VerifyPinCodeAuto")]
        [EnableRateLimiting("ChatPolicy")]
        public async Task<IActionResult> VerifyPinCodeAuto([FromBody] string pinCodeHash)
        {
            return await VerifyPinInternal(pinCodeHash);
        }

        private async Task<IActionResult> VerifyPinInternal(string pinCodeHash)
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(pinCodeHash))
                return Json(new { success = false, message = "Invalid PIN" });

            try
            {
                // Check Active ChatUser
                var chatUser = await db.ChatUsers
                    .FirstOrDefaultAsync(u => u.UserId == currentUser.Id && u.IsActive && u.PinCodeHash == pinCodeHash);

                if (chatUser == null)
                {
                    logger.LogWarning("PIN verification failed for {Username}", currentUser.Username);
                    return Json(new { success = false, message = "Mã PIN không đúng" });
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error verifying PIN");
                return Json(new { success = false, message = "Lỗi xác thực" });
            }
        }

        [HttpGet]
        public IActionResult ForgotPin()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetChatAccount()
        {
            var currentUser = await HttpContext.GetCurrentUserAsync(db);
            if (currentUser == null) return Unauthorized();

            try
            {
                var result = await chatService.ResetChatAccountAsync(currentUser.Id);

                if (result.Success)
                {
                    return Ok(new 
                    { 
                        success = true, 
                        message = "Đã reset tài khoản chat.",
                        redirectUrl = Url.Action("Register", "Chat")
                    });
                }

                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error resetting chat");
                return StatusCode(500, new{message="Lỗi server khi reset tài khoản"});
            }
        }
    }

    public class SendMessageModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid ConversationId { get; set; }
        public string CipherText { get; set; } = string.Empty;
        public byte MessageType { get; set; }
    }
    
    public class UpdateMessageFileModel
    {
        [System.ComponentModel.DataAnnotations.Required]
        public long MessageId { get; set; }
        public string EncryptedUrl { get; set; } = string.Empty;
    }
}