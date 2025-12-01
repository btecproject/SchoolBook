using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.DTOs;
using SchoolBookPlatform.Models;
using Conversation = SchoolBookPlatform.Models.Conversation;
using Message = SchoolBookPlatform.Models.Message;

namespace SchoolBookPlatform.Services
{
    public class ChatService(
        AppDbContext db,
        ILogger<ChatService> logger,
        CloudinaryService cloudinaryService)
    {
    // Lấy Conversation Key
        public async Task<string?> GetConversationKeyAsync(Guid userId, Guid conversationId, int version)
        {
            // Kiểm tra user có trong conversation không để bảo mật
            var isMember = await db.ConversationMembers
                .AnyAsync(cm => cm.ConversationId == conversationId && cm.UserId == userId);
            
            if (!isMember) return null;

            var key = await db.ConversationKeys
                .Where(ck => ck.UserId == userId && 
                             ck.ConversationId == conversationId && 
                             ck.KeyVersion == version)
                .Select(ck => ck.EncryptedKey)
                .FirstOrDefaultAsync();

            return key;
        }

        // Lưu Conversation Key
        public async Task<bool> SaveConversationKeyAsync(Guid userId, Guid conversationId, string encryptedKey, int version)
        {
            // Kiểm tra user có trong conversation không
            var isMember = await db.ConversationMembers
                .AnyAsync(cm => cm.ConversationId == conversationId && cm.UserId == userId);
            
            if (!isMember) return false;

            var existingKey = await db.ConversationKeys
                .FirstOrDefaultAsync(ck => ck.UserId == userId && 
                                           ck.ConversationId == conversationId && 
                                           ck.KeyVersion == version);

            if (existingKey != null)
            {
                // Update key nếu đã tồn tại
                existingKey.EncryptedKey = encryptedKey;
                existingKey.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Thêm mới
                var newKey = new ConversationKey
                {
                    UserId = userId,
                    ConversationId = conversationId,
                    KeyVersion = version,
                    EncryptedKey = encryptedKey,
                    UpdatedAt = DateTime.UtcNow
                };
                db.ConversationKeys.Add(newKey);
            }

            await db.SaveChangesAsync();
            return true;
        }    
    public async Task<List<ContactDto>> GetRecentContactsAsync(Guid currentUserId)
    {
        // 1. Lấy tất cả Conversation mà user tham gia
        var conversationIds = await db.ConversationMembers
            .Where(cm => cm.UserId == currentUserId)
            .Select(cm => cm.ConversationId)
            .ToListAsync();

        if (!conversationIds.Any()) return new List<ContactDto>();

        // 2. Lấy tin nhắn cuối cùng (Dùng EF Core Window Function hoặc GroupBy tối ưu)
        var lastMessages = await db.Messages
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.CreatedAt).FirstOrDefault())
            .ToListAsync();

        // 3. Lấy thông tin đối phương (Partner)
        var partners = await db.ConversationMembers
            .Where(cm => conversationIds.Contains(cm.ConversationId) && cm.UserId != currentUserId)
            .Select(cm => new { cm.ConversationId, cm.UserId })
            .ToListAsync();

        // 4. Lấy số lượng tin chưa đọc
        var unreadCounts = await db.MessageNotifications
            .Where(mn => mn.RecipientId == currentUserId)
            .ToDictionaryAsync(mn => mn.SenderId, mn => mn.UnreadCount);

        // 5. Lấy User Profile (Avatar + Name)
        var partnerIds = partners.Select(p => p.UserId).Distinct().ToList();
        
        var users = await db.Users
            .Where(u => partnerIds.Contains(u.Id))
            .Select(u => new 
            { 
                u.Id, 
                u.Username,
                DisplayName = db.ChatUsers.FirstOrDefault(cu => cu.UserId == u.Id).DisplayName ?? u.Username,
                AvatarUrl = db.UserProfiles.FirstOrDefault(up => up.UserId == u.Id).AvatarUrl 
            })
            .ToDictionaryAsync(u => u.Id);

        var result = new List<ContactDto>();

        foreach (var msg in lastMessages)
        {
            if (msg == null) continue;

            var partner = partners.FirstOrDefault(p => p.ConversationId == msg.ConversationId);
            if (partner == null) continue; // Trường hợp chat 1 mình hoặc lỗi dữ liệu

            if (users.TryGetValue(partner.UserId, out var userInfo))
            {
                string prefix = msg.SenderId == currentUserId ? "Bạn: " : "";
                string preview = msg.MessageType == 0 
                    ? (msg.PinExchange != null ? "Tin nhắn bảo mật" : "Tin nhắn văn bản")
                    : "Đã gửi tệp đính kèm";

                // Nếu tin nhắn văn bản, cố gắng cắt ngắn nếu quá dài (Optional)
                // Lưu ý: CipherText ở đây là mã hóa, nên cắt chuỗi mã hóa không có ý nghĩa lắm về mặt hiển thị
                // Nhưng cứ để nguyên để client giải mã hoặc hiển thị text placeholder.

                result.Add(new ContactDto
                {
                    UserId = partner.UserId,
                    Username = userInfo.Username,
                    DisplayName = userInfo.DisplayName,
                    AvatarUrl = userInfo.AvatarUrl ?? "",
                    UnreadCount = unreadCounts.ContainsKey(partner.UserId) ? unreadCounts[partner.UserId] : 0,
                    LastSentAt = msg.CreatedAt,
                    LastMessagePreview = prefix + preview
                });
            }
        }

        return result.OrderByDescending(x => x.LastSentAt).ToList();
    }
        //2. Đánh dấu đã đọc
        public async Task MarkMessagesAsReadAsync(Guid recipientId, Guid senderId)
        {
            var noti = await db.MessageNotifications.FindAsync(recipientId, senderId);
            if (noti != null && noti.UnreadCount > 0)
            {
                noti.UnreadCount = 0;
                await db.SaveChangesAsync();
            }
        }
        
        // Kiểm tra user đã kích hoạt chat chưa
        public async Task<bool> IsChatActivatedAsync(Guid userId)
        {
            return await db.ChatUsers.AnyAsync(cu => cu.UserId == userId);
        }

        // Kiểm tra trạng thái RSA key
        public async Task<RsaKeyStatus> CheckRsaKeyStatusAsync(Guid userId)
        {
            var rsaKey = await db.UserRsaKeys
                .Where(k => k.UserId == userId && k.IsActive)
                .FirstOrDefaultAsync();

            if (rsaKey == null)
            {
                return new RsaKeyStatus
                {
                    IsValid = false,
                    Message = "Bạn cần tạo cặp khóa RSA để sử dụng chat."
                };
            }

            if (rsaKey.ExpiresAt < DateTime.UtcNow)
            {
                // Key hết hạn -> đánh dấu không active
                rsaKey.IsActive = false;
                await db.SaveChangesAsync();

                return new RsaKeyStatus
                {
                    IsValid = false,
                    Message = "Khóa RSA đã hết hạn. Vui lòng tạo cặp khóa mới.",
                    ExpiresAt = rsaKey.ExpiresAt
                };
            }

            return new RsaKeyStatus
            {
                IsValid = true,
                Message = "Khóa RSA hợp lệ.",
                ExpiresAt = rsaKey.ExpiresAt
            };
        }

        // Kiểm tra có RSA key hợp lệ không
        public async Task<bool> HasValidRsaKeyAsync(Guid userId)
        {
            var status = await CheckRsaKeyStatusAsync(userId);
            return status.IsValid;
        }

        // Đăng ký ChatUser
        public async Task<ServiceResult> RegisterChatUserAsync(
            Guid userId,
            string username,
            string displayName,
            string pinCodeHash)
        {
            try
            {
                // Kiểm tra đã đăng ký chưa
                var existing = await db.ChatUsers.FindAsync(userId);
                if (existing != null)
                {
                    return new ServiceResult
                    {
                        Success = false,
                        Message = "Bạn đã đăng ký chat rồi."
                    };
                }

                // Tạo ChatUser mới
                var chatUser = new ChatUser
                {
                    UserId = userId,
                    Username = username,
                    DisplayName = displayName,
                    PinCodeHash = pinCodeHash,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.ChatUsers.Add(chatUser);
                await db.SaveChangesAsync();

                logger.LogInformation("ChatUser created successfully for userId: {UserId}", userId);

                return new ServiceResult
                {
                    Success = true,
                    Message = "Đăng ký chat thành công!"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error registering ChatUser for userId: {UserId}", userId);
                return new ServiceResult
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi khi đăng ký."
                };
            }
        }

        // Lưu RSA keys
        public async Task<ServiceResult> SaveUserRsaKeysAsync(
            Guid userId,
            string publicKey,
            string privateKeyEncrypted)
        {
            try
            {
                // Kiểm tra đã kích hoạt chat chưa
                var isChatActivated = await IsChatActivatedAsync(userId);
                if (!isChatActivated)
                {
                    return new ServiceResult
                    {
                        Success = false,
                        Message = "Bạn cần đăng ký chat trước."
                    };
                }

                // Vô hiệu hóa các key cũ
                var oldKeys = await db.UserRsaKeys
                    .Where(k => k.UserId == userId && k.IsActive)
                    .ToListAsync();

                foreach (var key in oldKeys)
                {
                    key.IsActive = false;
                }
                //Xóa Key bị !isActivê
                var disabledKeys = await db.UserRsaKeys.Where(k => !k.IsActive).ToListAsync();
                db.UserRsaKeys.RemoveRange(disabledKeys);
                // Tạo key mới
                var newKey = new UserRsaKey
                {
                    UserId = userId,
                    PublicKey = publicKey,
                    PrivateKeyEncrypted = privateKeyEncrypted,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddYears(100), //tạm để 100 năm 
                    IsActive = true
                };

                db.UserRsaKeys.Add(newKey);
                await db.SaveChangesAsync();

                logger.LogInformation("RSA keys saved successfully for userId: {UserId}", userId);

                return new ServiceResult
                {
                    Success = true,
                    Message = "Lưu khóa RSA thành công!"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving RSA keys for userId: {UserId}", userId);
                return new ServiceResult
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi khi lưu keys."
                };
            }
        }

        // Lấy public key của user
        public async Task<string?> GetUserPublicKeyAsync(Guid userId)
        {
            var key = await db.UserRsaKeys
                .Where(k => k.UserId == userId && k.IsActive)
                .Select(k => k.PublicKey)
                .FirstOrDefaultAsync();

            return key;
        }

        // Tìm kiếm ChatUser theo DisplayName hoặc Username
        public async Task<List<ChatUserSearchResult>> SearchChatUsersAsync(string searchTerm, Guid currentUserId)
        {
            var results = await db.ChatUsers
                .Where(cu => cu.UserId != currentUserId &&
                             (cu.DisplayName.Contains(searchTerm) || cu.Username.Contains(searchTerm)))
                .Select(cu => new ChatUserSearchResult
                {
                    UserId = cu.UserId,
                    Username = cu.Username,
                    AvatarUrl = db.UserProfiles.Where(up=>up.UserId == cu.UserId)
                        .Select(up => up.AvatarUrl).FirstOrDefault(),
                    DisplayName = cu.DisplayName
                })
                .Take(20)
                .ToListAsync();

            return results;
        }

        // Get or Create Conversation (1-1)
        public async Task<ConversationResult> GetOrCreateConversationAsync(Guid userId1, Guid userId2)
        {
            try
            {
                // Tìm conversation 1-1 đã tồn tại
                var existingConv = await db.Conversations
                    .Where(c => c.Type == 0) // Type 0 = 1-1
                    .Where(c => db.ConversationMembers
                                    .Where(cm => cm.ConversationId == c.Id)
                                    .Select(cm => cm.UserId)
                                    .Contains(userId1) &&
                                db.ConversationMembers
                                    .Where(cm => cm.ConversationId == c.Id)
                                    .Select(cm => cm.UserId)
                                    .Contains(userId2))
                    .FirstOrDefaultAsync();

                if (existingConv != null)
                {
                    // ✅ FIX: Kiểm tra có PIN exchange CỦA TỪNG USER chưa
                    var hasPinExchangeFromUser1 = await db.Messages
                        .Where(m => m.ConversationId == existingConv.Id && m.SenderId == userId1)
                        .AnyAsync(m => m.PinExchange != null);

                    return new ConversationResult
                    {
                        ConversationId = existingConv.Id,
                        IsNew = false,
                        HasPinExchange = hasPinExchangeFromUser1  // ✅ Chỉ check PIN của user hiện tại
                    };
                }

                // Tạo conversation mới
                var newConv = new Conversation
                {
                    Id = Guid.NewGuid(),
                    Type = 0, // 1-1
                    CreatedAt = DateTime.UtcNow,
                    CreatorId = userId1,
                };

                db.Conversations.Add(newConv);

                // Thêm members
                db.ConversationMembers.Add(new ConversationMember
                {
                    ConversationId = newConv.Id,
                    UserId = userId1,
                    JoinedAt = DateTime.UtcNow,
                    Role = 0
                });

                db.ConversationMembers.Add(new ConversationMember
                {
                    ConversationId = newConv.Id,
                    UserId = userId2,
                    JoinedAt = DateTime.UtcNow,
                    Role = 0
                });

                await db.SaveChangesAsync();

                logger.LogInformation("Created new conversation {ConvId} between {User1} and {User2}",
                    newConv.Id, userId1, userId2);

                return new ConversationResult
                {
                    ConversationId = newConv.Id,
                    IsNew = true,
                    HasPinExchange = false
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating conversation between {User1} and {User2}", userId1, userId2);
                throw;
            }
        }

        // Send PIN Exchange Message
        public async Task<ServiceResult> SendPinExchangeMessageAsync(
            Guid conversationId,
            Guid senderId,
            Guid recipientId,
            string encryptedPin)
        {
            try
            {
                // Kiểm tra conversation tồn tại và user có quyền
                var isMember = await db.ConversationMembers
                    .AnyAsync(cm => cm.ConversationId == conversationId && cm.UserId == senderId);

                if (!isMember)
                {
                    return new ServiceResult
                    {
                        Success = false,
                        Message = "Bạn không có quyền gửi tin trong conversation này"
                    };
                }

                var message = new Message
                {
                    ConversationId = conversationId,
                    SenderId = senderId,
                    MessageType = 0, // Text
                    CipherText = "[PIN Exchange]", // Placeholder
                    PinExchange = encryptedPin,
                    CreatedAt = DateTime.UtcNow
                };

                db.Messages.Add(message);
                await db.SaveChangesAsync();

                logger.LogInformation("PIN exchange sent by user {UserId} in conversation {ConvId}", 
                    senderId, conversationId);

                return new ServiceResult
                {
                    Success = true,
                    Message = "PIN exchange sent successfully"
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending PIN exchange");
                return new ServiceResult
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi khi gửi PIN exchange"
                };
            }
        }

        // Get messages with attachments
        public async Task<List<MessageDto>> GetMessagesAsync(
            Guid conversationId,
            Guid userId,
            int count = 20)
        {
            try
            {
                var isMember = await db.ConversationMembers
                    .AnyAsync(cm => cm.ConversationId == conversationId && cm.UserId == userId);

                if (!isMember)
                {
                    throw new UnauthorizedAccessException("Bạn không có quyền xem tin nhắn này");
                }

                var messages = await db.Messages
                    .Where(m => m.ConversationId == conversationId)
                    .OrderByDescending(m => m.Id)
                    .Take(count)
                    .OrderBy(m => m.Id)
                    .Select(m => new MessageDto
                    {
                        MessageId = m.Id,
                        SenderId = m.SenderId,
                        CipherText = m.CipherText,
                        PinExchange = m.PinExchange,
                        MessageType = m.MessageType,
                        CreatedAt = m.CreatedAt,
                        IsMine = m.SenderId == userId,
                        Attachment = db.MessageAttachments
                            .Where(a => a.MessageId == m.Id)
                            .Select(a => new MessageAttachmentDto
                            {
                                Url = a.CloudinaryUrl,
                                FileName = a.FileName,
                                ResourceType = a.ResourceType,
                                Format = a.Format
                            })
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return messages;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting messages for conversation {ConvId}", conversationId);
                throw;
            }
        }

        // Send Message
        public async Task<ServiceResult<long>> SendMessageAsync(
            Guid conversationId,
            Guid senderId,
            string cipherText,
            byte messageType)
        {
            try
            {
                var isMember = await db.ConversationMembers
                    .AnyAsync(cm => cm.ConversationId == conversationId && cm.UserId == senderId);

                if (!isMember)
                {
                    return new ServiceResult<long>
                    {
                        Success = false,
                        Message = "Bạn không có quyền gửi tin trong conversation này"
                    };
                }

                var message = new Message
                {
                    ConversationId = conversationId,
                    SenderId = senderId,
                    MessageType = messageType,
                    CipherText = cipherText,
                    CreatedAt = DateTime.UtcNow
                };

                db.Messages.Add(message);
                await db.SaveChangesAsync();

                logger.LogInformation("Message {MsgId} sent in conversation {ConvId}",
                    message.Id, conversationId);

                return new ServiceResult<long>
                {
                    Success = true,
                    Message = "Message sent successfully",
                    Data = message.Id
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending message");
                return new ServiceResult<long>
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi khi gửi tin nhắn"
                };
            }
        }

        // Create message (for file upload)
        public async Task<ServiceResult<long>> CreateMessageAsync(
            Guid conversationId,
            Guid senderId,
            string cipherText,
            byte messageType)
        {
            try
            {
                var isMember = await db.ConversationMembers
                    .AnyAsync(cm => cm.ConversationId == conversationId && cm.UserId == senderId);

                if (!isMember)
                {
                    return new ServiceResult<long>
                    {
                        Success = false,
                        Message = "Bạn không có quyền gửi tin trong conversation này"
                    };
                }

                var message = new Message
                {
                    ConversationId = conversationId,
                    SenderId = senderId,
                    MessageType = messageType,
                    CipherText = cipherText,
                    CreatedAt = DateTime.UtcNow
                };

                db.Messages.Add(message);
                await db.SaveChangesAsync();

                return new ServiceResult<long>
                {
                    Success = true,
                    Data = message.Id
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating message");
                return new ServiceResult<long>
                {
                    Success = false,
                    Message = "Lỗi khi tạo message"
                };
            }
        }

        // Delete message
        public async Task<bool> DeleteMessageAsync(long messageId)
        {
            try
            {
                var message = await db.Messages.FindAsync(messageId);
                if (message == null) return false;

                db.Messages.Remove(message);
                await db.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting message {MsgId}", messageId);
                return false;
            }
        }

        // Upload file và update message
        public async Task<CloudinaryUploadResult> UploadFileAsync(
            IFormFile file,
            Guid userId,
            Guid conversationId,
            long messageId)
        {
            try
            {
                // Upload to Cloudinary
                var uploadResult = await cloudinaryService.UploadChatFileAsync(
                    file, userId, conversationId, messageId);

                if (!uploadResult.Success)
                {
                    return uploadResult;
                }

                // Update message with encrypted URL
                var message = await db.Messages.FindAsync(messageId);
                if (message == null)
                {
                    return new CloudinaryUploadResult
                    {
                        Success = false,
                        Message = "Message không tồn tại"
                    };
                }

                message.CipherText = uploadResult.Url; // URL will be encrypted on client

                // Save attachment metadata
                var attachment = new MessageAttachment
                {
                    MessageId = messageId,
                    CloudinaryUrl = uploadResult.Url,
                    ResourceType = uploadResult.ResourceType ?? "raw",
                    Format = uploadResult.Format ?? "",
                    FileName = uploadResult.FileName ?? "",
                    UploadedAt = DateTime.UtcNow
                };

                db.MessageAttachments.Add(attachment);
                await db.SaveChangesAsync();

                logger.LogInformation("File uploaded and message {MsgId} updated", messageId);

                return uploadResult;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading file for message {MsgId}", messageId);
                return new CloudinaryUploadResult
                {
                    Success = false,
                    Message = "Lỗi khi upload file"
                };
            }
        }

        // Delete message with attachment
        public async Task<bool> DeleteMessageWithAttachmentAsync(long messageId, Guid userId)
        {
            try
            {
                var message = await db.Messages
                    .Include(m => m.Conversation)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null) return false;

                // Check permission
                var isMember = await db.ConversationMembers
                    .AnyAsync(cm => cm.ConversationId == message.ConversationId && cm.UserId == userId);

                if (!isMember || message.SenderId != userId)
                {
                    return false;
                }

                // Get attachment
                var attachment = await db.MessageAttachments
                    .FirstOrDefaultAsync(a => a.MessageId == messageId);

                if (attachment != null)
                {
                    // Extract public ID from URL
                    var publicId = ExtractPublicIdFromUrl(attachment.CloudinaryUrl);

                    // Delete from Cloudinary
                    await cloudinaryService.DeleteChatFileAsync(publicId, attachment.ResourceType);

                    // Delete attachment record
                    db.MessageAttachments.Remove(attachment);
                }

                // Delete message
                db.Messages.Remove(message);
                await db.SaveChangesAsync();

                logger.LogInformation("Message {MsgId} and attachment deleted", messageId);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting message {MsgId}", messageId);
                return false;
            }
        }

        // Helper: Extract public ID from Cloudinary URL
        private string ExtractPublicIdFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;

                // Remove /v{version}/ and extension
                var parts = path.Split('/');
                var relevantParts = parts.Skip(3).ToArray();
                var publicId = string.Join("/", relevantParts);

                // Remove extension
                var lastDot = publicId.LastIndexOf('.');
                if (lastDot > 0)
                {
                    publicId = publicId.Substring(0, lastDot);
                }

                return publicId;
            }
            catch
            {
                return url; // Fallback
            }
        }
    }
}