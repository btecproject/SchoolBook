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
        private async Task<Guid?> GetActiveChatUserIdAsync(Guid userId)
        {
            //Tìm ChatUser thuộc về UserId này và đang Active
            var chatUserId = await db.ChatUsers
                .Where(cu => cu.UserId == userId && cu.IsActive)
                .Select(cu => cu.Id)
                .FirstOrDefaultAsync();

            return chatUserId == Guid.Empty ? null : chatUserId;
        }

        //Lấy ChatUserId từ danh sách UserIds (groupChat(làm sau))
        private async Task<List<Guid>> GetActiveChatUserIdsAsync(List<Guid> userIds)
        {
            return await db.ChatUsers
                .Where(cu => userIds.Contains(cu.UserId) && cu.IsActive)
                .Select(cu => cu.Id)
                .ToListAsync();
        }

        public async Task<ServiceResult> InitializeConversationKeysAsync(Guid currentUserId,
            InitializeConversationKeyRequest request)
        {
            var myChatUserId = await GetActiveChatUserIdAsync(currentUserId);
            if (myChatUserId == null) return new ServiceResult { Success = false, Message = "Chưa kích hoạt." };

            try
            {
                var isMember = await db.ConversationMembers
                    .AnyAsync(cm => cm.ConversationId == request.ConversationId && cm.ChatUserId == myChatUserId.Value);

                if (!isMember) return new ServiceResult { Success = false, Message = "Không phải thành viên." };

                var newKeys = new List<ConversationKey>();
                var now = DateTime.UtcNow.AddHours(7);

                foreach (var item in request.Keys)
                {
                    // Lưu ý: item.UserId từ Frontend gửi lên là UserID gốc. 
                    // Ta cần map sang ChatUserId Active tương ứng.
                    var targetChatUserId = await GetActiveChatUserIdAsync(item.UserId);

                    if (targetChatUserId == null) continue;

                    var existingKey = await db.ConversationKeys
                        .FirstOrDefaultAsync(ck => ck.ConversationId == request.ConversationId
                                                   && ck.ChatUserId == targetChatUserId.Value);

                    if (existingKey == null)
                    {
                        newKeys.Add(new ConversationKey
                        {
                            ConversationId = request.ConversationId,
                            ChatUserId = targetChatUserId.Value,
                            KeyVersion = 1,
                            EncryptedKey = item.EncryptedKey,
                            UpdatedAt = now
                        });
                    }
                }

                if (newKeys.Any())
                {
                    await db.ConversationKeys.AddRangeAsync(newKeys);
                    await db.SaveChangesAsync();
                }

                return new ServiceResult { Success = true, Message = "OK" };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error init keys");
                return new ServiceResult { Success = false, Message = "Lỗi hệ thống." };
            }
        }

        public async Task<string?> GetConversationKeyAsync(Guid userId, Guid conversationId, int version)
        {
            var chatUserId = await GetActiveChatUserIdAsync(userId);
            if (chatUserId == null) return null;
            
            var isMember = await db.ConversationMembers
                .AnyAsync(cm => cm.ConversationId == conversationId && cm.ChatUserId == chatUserId.Value);
    
            if (!isMember) return null;

            var key = await db.ConversationKeys
                .Where(ck => ck.ChatUserId == chatUserId.Value &&
                             ck.ConversationId == conversationId && 
                             ck.KeyVersion == version)
                .Select(ck => ck.EncryptedKey)
                .FirstOrDefaultAsync();

            return key;
        }


        // Lưu Conversation Key
        // Lưu Conversation Key (Sửa để dùng ChatUserId)
        public async Task<bool> SaveConversationKeyAsync(Guid userId, Guid conversationId, string encryptedKey, int version)
        {
            // 1. Lấy Chat User ID đang active
            var chatUserId = await GetActiveChatUserIdAsync(userId);
            if (chatUserId == null) return false; // Chưa kích hoạt chat

            // 2. Kiểm tra user có trong conversation không
            // Lưu ý: Check theo ChatUserId
            var isMember = await db.ConversationMembers
                .AnyAsync(cm => cm.ConversationId == conversationId && cm.ChatUserId == chatUserId.Value);

            if (!isMember) return false;

            // 3. Tìm key đã tồn tại
            var existingKey = await db.ConversationKeys
                .FirstOrDefaultAsync(ck => ck.ChatUserId == chatUserId.Value && // <-- Dùng ChatUserId
                                           ck.ConversationId == conversationId && 
                                           ck.KeyVersion == version);

            if (existingKey != null)
            {
                // Update key nếu đã tồn tại
                existingKey.EncryptedKey = encryptedKey;
                existingKey.UpdatedAt = DateTime.UtcNow.AddHours(7);
            }
            else
            {
                // Thêm mới
                var newKey = new ConversationKey
                {
                    ChatUserId = chatUserId.Value, // <-- Dùng ChatUserId
                    ConversationId = conversationId,
                    KeyVersion = version,
                    EncryptedKey = encryptedKey,
                    UpdatedAt = DateTime.UtcNow.AddHours(7)
                };
                db.ConversationKeys.Add(newKey);
            }

            await db.SaveChangesAsync();
            return true;
        }


        public async Task<List<ContactDto>> GetRecentContactsAsync(Guid currentUserId)
        {
            var myChatUserId = await GetActiveChatUserIdAsync(currentUserId);
            if (myChatUserId == null) return new List<ContactDto>();

            var conversationIds = await db.ConversationMembers
                .Where(cm => cm.ChatUserId == myChatUserId.Value)
                .Select(cm => cm.ConversationId)
                .ToListAsync();

            if (!conversationIds.Any()) return new List<ContactDto>();

            var lastMessages = await db.Messages
                .Where(m => conversationIds.Contains(m.ConversationId))
                .GroupBy(m => m.ConversationId)
                .Select(g => g.OrderByDescending(m => m.CreatedAt).FirstOrDefault())
                .ToListAsync();

            //Partner có thể là ChatUser cũ (đã inactive) hoặc mới -> Vẫn lấy để hiển thị lịch sử
            var partners = await db.ConversationMembers
                .Include(cm => cm.ChatUser) // Include bảng ChatUser để lấy DisplayName
                .Where(cm => conversationIds.Contains(cm.ConversationId) && cm.ChatUserId != myChatUserId.Value)
                .Select(cm => new
                {
                    cm.ConversationId,
                    ChatUserId = cm.ChatUserId,
                    UserId = cm.ChatUser.UserId, // Lấy UserId gốc để link Avatar
                    DisplayName = cm.ChatUser.DisplayName,
                    Username = cm.ChatUser.Username
                })
                .ToListAsync();

            // 4. Lấy Avatar từ User Profile (dựa trên UserId gốc)
            var partnerUserIds = partners.Select(p => p.UserId).Distinct().ToList();
            var avatars = await db.UserProfiles
                .Where(up => partnerUserIds.Contains(up.UserId))
                .ToDictionaryAsync(up => up.UserId, up => up.AvatarUrl);

            var result = new List<ContactDto>();

            foreach (var msg in lastMessages)
            {
                if (msg == null) continue;
                var partner = partners.FirstOrDefault(p => p.ConversationId == msg.ConversationId);
                if (partner == null) continue;

                var avatarUrl = avatars.ContainsKey(partner.UserId) ? avatars[partner.UserId] : "";

                string prefix = msg.SenderId == myChatUserId.Value ? "Bạn: " : "";
                string preview = msg.MessageType == 0 ? "Tin nhắn văn bản" : "File đính kèm";

                result.Add(new ContactDto
                {
                    ConversationId = msg.ConversationId,
                    UserId = partner.UserId,
                    Username = partner.Username,
                    DisplayName = partner.DisplayName,
                    AvatarUrl = avatarUrl ?? "",
                    LastSentAt = msg.CreatedAt,
                    LastMessagePreview = prefix + preview,
                    UnreadCount = 0
                });
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


        public async Task<bool> IsChatActivatedAsync(Guid userId)
        {
            return await db.ChatUsers.AnyAsync(cu => cu.UserId == userId && cu.IsActive == true);
        }

        public async Task<RsaKeyStatus> CheckRsaKeyStatusAsync(Guid userId)
        {
            var chatUserId = await GetActiveChatUserIdAsync(userId);
            if (chatUserId == null) return new RsaKeyStatus { IsValid = false, Message = "Chưa kích hoạt." };

            var rsaKey = await db.UserRsaKeys
                .Where(k => k.ChatUserId == chatUserId.Value && k.IsActive)
                .FirstOrDefaultAsync();

            if (rsaKey == null)
            {
                return new RsaKeyStatus { IsValid = false, Message = "Chưa có khóa RSA." };
            }

            return new RsaKeyStatus { IsValid = true, Message = "Hợp lệ.", ExpiresAt = rsaKey.ExpiresAt };
        }

        // Kiểm tra có RSA key hợp lệ không
        public async Task<bool> HasValidRsaKeyAsync(Guid userId)
        {
            var status = await CheckRsaKeyStatusAsync(userId);
            return status.IsValid;
        }

        // Đăng ký ChatUser
        public async Task<ServiceResult> RegisterChatUserAsync(Guid userId, string username, string displayName,
            string pinCodeHash)
        {
            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                //Vô hiệu hóa tất cả ChatUser cũ của user này (nếu có)
                var oldChatUsers = await db.ChatUsers
                    .Where(cu => cu.UserId == userId && cu.IsActive)
                    .ToListAsync();

                foreach (var old in oldChatUsers)
                {
                    old.IsActive = false;
                }

                var newChatUser = new ChatUser
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Username = username,
                    DisplayName = displayName,
                    PinCodeHash = pinCodeHash,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddHours(7),
                    UpdatedAt = DateTime.UtcNow.AddHours(7)
                };

                db.ChatUsers.Add(newChatUser);
                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                return new ServiceResult { Success = true, Message = "Đăng ký chat thành công!" };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "Error registering chat user");
                return new ServiceResult { Success = false, Message = "Lỗi đăng ký." };
            }
        }

        // Lưu RSA keys
        public async Task<ServiceResult> SaveUserRsaKeysAsync(Guid userId, string publicKey,
            string privateKeyEncrypted)
        {
            var chatUserId = await GetActiveChatUserIdAsync(userId);
            if (chatUserId == null)
                return new ServiceResult { Success = false, Message = "Chưa kích hoạt chat." };

            var oldKeys = await db.UserRsaKeys.Where(k => k.ChatUserId == chatUserId.Value && k.IsActive)
                .ToListAsync();
            foreach (var k in oldKeys) k.IsActive = false;

            var newKey = new UserRsaKey
            {
                ChatUserId = chatUserId.Value,
                PublicKey = publicKey,
                PrivateKeyEncrypted = privateKeyEncrypted,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddHours(7),
                ExpiresAt = DateTime.UtcNow.AddYears(100)
            };

            db.UserRsaKeys.Add(newKey);
            await db.SaveChangesAsync();

            return new ServiceResult { Success = true };
        }

        // Lấy public key của user
        public async Task<string?> GetUserPublicKeyAsync(Guid userId)
        {
            var chatUserId = await GetActiveChatUserIdAsync(userId);
            if (chatUserId == null) return null;

            return await db.UserRsaKeys
                .Where(k => k.ChatUserId == chatUserId.Value && k.IsActive)
                .Select(k => k.PublicKey)
                .FirstOrDefaultAsync();
        }

        // Tìm kiếm ChatUser theo DisplayName hoặc Username
        public async Task<List<ChatUserSearchResult>> SearchChatUsersAsync(string searchTerm, Guid currentUserId)
        {
            // Chỉ tìm các ChatUser đang Active và không phải của mình
            var results = await db.ChatUsers
                .Where(cu => cu.UserId != currentUserId && cu.IsActive == true &&
                             (cu.DisplayName.Contains(searchTerm) || cu.Username.Contains(searchTerm)))
                .Select(cu => new ChatUserSearchResult
                {
                    UserId = cu.UserId, // Trả về UserId gốc để Frontend dùng init chat
                    Username = cu.Username,
                    DisplayName = cu.DisplayName,
                    AvatarUrl = db.UserProfiles.Where(up => up.UserId == cu.UserId).Select(up => up.AvatarUrl)
                        .FirstOrDefault()
                })
                .Take(20)
                .ToListAsync();

            return results;
        }


        // Get or Create Conversation (1-1)
        // public async Task<ConversationResult> GetOrCreateConversationAsync(Guid userId1, Guid userId2)
        // {
        //     var chatUser1 = await GetActiveChatUserIdAsync(userId1);
        //     var chatUser2 = await GetActiveChatUserIdAsync(userId2);
        //
        //     // Nếu đối phương đã reset mà chưa tạo mới, chatUser2 sẽ là null -> Không thể chat
        //     if (chatUser1 == null || chatUser2 == null)
        //         throw new Exception("Người dùng chưa kích hoạt chat hoặc đã reset tài khoản.");
        //
        //     try
        //     {
        //         var existingConv = await db.Conversations
        //             .Where(c => c.Type == 0) // Type 0 = 1-1
        //             .Where(c => db.ConversationMembers
        //                             .Where(cm => cm.ConversationId == c.Id)
        //                             .Select(cm => cm.ChatUserId)
        //                             .Contains(chatUser1.Value) &&
        //                         db.ConversationMembers
        //                             .Where(cm => cm.ConversationId == c.Id)
        //                             .Select(cm => cm.ChatUserId)
        //                             .Contains(chatUser2.Value))
        //             .FirstOrDefaultAsync();
        //
        //         if (existingConv != null)
        //         {
        //             var hasValidKey = await db.ConversationKeys
        //                 .AnyAsync(ck => ck.ConversationId == existingConv.Id && ck.ChatUserId == chatUser1.Value);
        //
        //             return new ConversationResult
        //             {
        //                 ConversationId = existingConv.Id,
        //                 IsNew = false,
        //                 IsKeyInitialized = hasValidKey
        //             };
        //         }
        //
        //         var newConv = new Conversation
        //         {
        //             Id = Guid.NewGuid(),
        //             Type = 0,
        //             CreatedAt = DateTime.UtcNow.AddHours(7),
        //             CreatorId = userId1,
        //         };
        //
        //         db.Conversations.Add(newConv);
        //
        //         db.ConversationMembers.Add(new ConversationMember
        //         {
        //             ConversationId = newConv.Id,
        //             ChatUserId = chatUser1.Value,
        //             JoinedAt = DateTime.UtcNow.AddHours(7),
        //             Role = 0
        //         });
        //
        //         db.ConversationMembers.Add(new ConversationMember
        //         {
        //             ConversationId = newConv.Id,
        //             ChatUserId = chatUser2.Value,
        //             JoinedAt = DateTime.UtcNow.AddHours(7),
        //             Role = 0
        //         });
        //
        //         await db.SaveChangesAsync();
        //
        //         return new ConversationResult
        //         {
        //             ConversationId = newConv.Id,
        //             IsNew = true,
        //             IsKeyInitialized = false
        //         };
        //     }
        //     catch (Exception ex)
        //     {
        //         logger.LogError(ex, "Error creating conversation");
        //         throw;
        //     }
        // }

        // Get messages with attachments
        
        public async Task<ConversationResult> GetOrCreateConversationAsync(Guid currentUserId, Guid recipientUserId)
    {
        var senderChatUser = await db.ChatUsers
            .FirstOrDefaultAsync(u => u.UserId == currentUserId && u.IsActive);

        if (senderChatUser == null)
            throw new Exception("Tài khoản chat của bạn chưa được kích hoạt.");

        //Kiểm tra xem ng nhận có tài khoản nào đang Active không
        var recipientActiveUser = await db.ChatUsers
            .FirstOrDefaultAsync(u => u.UserId == recipientUserId && u.IsActive);
        
        //Có tài khoản Active
        //Tìm conversation cũ, nếu không có thì TẠO MỚI.
        if (recipientActiveUser != null)
        {
            // Tìm cuộc trò chuyện giữa A và B_Active
            var liveConversation = await db.Conversations
                .Where(c => c.Type == 0)
                .Where(c => db.ConversationMembers.Any(cm => cm.ConversationId == c.Id && cm.ChatUserId == senderChatUser.Id) &&
                            db.ConversationMembers.Any(cm => cm.ConversationId == c.Id && cm.ChatUserId == recipientActiveUser.Id))
                .FirstOrDefaultAsync();

            if (liveConversation != null)
            {
                var hasKey = await db.ConversationKeys
                    .AnyAsync(ck => ck.ConversationId == liveConversation.Id && ck.ChatUserId == senderChatUser.Id);

                return new ConversationResult
                {
                    ConversationId = liveConversation.Id,
                    IsNew = false,
                    IsKeyInitialized = hasKey
                };
            }

            //Nếu chưa có -> tạo 
            return await CreateNewConversationInternal(senderChatUser.Id, recipientActiveUser.Id, currentUserId);
        }
        
        //Đã reset/Deactive
        //tìm conversation cũ ko tạo mới
        else
        {
            // Lấy danh sách tất cả ID cũ của B (để quét lịch sử)
            var allRecipientIds = await db.ChatUsers
                .Where(u => u.UserId == recipientUserId)
                .Select(u => u.Id)
                .ToListAsync();

            if (!allRecipientIds.Any())
                throw new Exception("Người dùng không tồn tại hoặc chưa kích hoạt chat.");

            // Tìm conversation cũ nhất hoặc mới nhất trong lịch sử
            var historyConversation = await db.Conversations
                .Where(c => c.Type == 0)
                .Where(c => db.ConversationMembers.Any(cm => cm.ConversationId == c.Id && cm.ChatUserId == senderChatUser.Id) &&
                            db.ConversationMembers.Any(cm => cm.ConversationId == c.Id && allRecipientIds.Contains(cm.ChatUserId)))
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();

            if (historyConversation != null)
            {
                var hasKey = await db.ConversationKeys
                    .AnyAsync(ck => ck.ConversationId == historyConversation.Id && ck.ChatUserId == senderChatUser.Id);
                return new ConversationResult
                {
                    ConversationId = historyConversation.Id,
                    IsNew = false,
                    IsKeyInitialized = hasKey 
                };
            }
            
            throw new Exception("Người dùng này không còn hoạt động và bạn chưa có lịch sử trò chuyện.");
        }
    }
        private async Task<ConversationResult> CreateNewConversationInternal(Guid senderChatId, Guid recipientChatId, Guid creatorUserId)
        {
            var newConv = new Conversation
            {
                Id = Guid.NewGuid(),
                Type = 0,
                CreatedAt = DateTime.UtcNow.AddHours(7),
                CreatorId = creatorUserId,
            };

            db.Conversations.Add(newConv);

            db.ConversationMembers.Add(new ConversationMember
            {
                ConversationId = newConv.Id,
                ChatUserId = senderChatId,
                JoinedAt = DateTime.UtcNow.AddHours(7),
                Role = 0
            });

            db.ConversationMembers.Add(new ConversationMember
            {
                ConversationId = newConv.Id,
                ChatUserId = recipientChatId,
                JoinedAt = DateTime.UtcNow.AddHours(7),
                Role = 0
            });

            await db.SaveChangesAsync();

            return new ConversationResult
            {
                ConversationId = newConv.Id,
                IsNew = true,
                IsKeyInitialized = false
            };
        }
        public async Task<List<MessageDto>> GetMessagesAsync(Guid conversationId, Guid userId, int count = 20,
            long? beforeId = null)
        {
            var chatUserId = await GetActiveChatUserIdAsync(userId);
            if (chatUserId == null) throw new UnauthorizedAccessException("Tài khoản chưa kích hoạt.");

            var isMember = await db.ConversationMembers
                .AnyAsync(cm => cm.ConversationId == conversationId && cm.ChatUserId == chatUserId.Value);

            if (!isMember) throw new UnauthorizedAccessException("Không có quyền xem.");

            var query = db.Messages.Where(m => m.ConversationId == conversationId);
            if (beforeId.HasValue)
            {
                query = query.Where(m => m.Id < beforeId.Value);
            }

            // IsMine: so sánh SenderId (ChatUser) với chatUserId hiện tại
            var messages = await query
                .OrderByDescending(m => m.Id)
                .Take(count)
                .OrderBy(m => m.Id)
                .Select(m => new MessageDto
                {
                    MessageId = m.Id,
                    ConversationId = m.ConversationId,
                    SenderId = m.SenderId, //ChatUserId của người gửi
                    CipherText = m.CipherText,
                    MessageType = m.MessageType,
                    CreatedAt = m.CreatedAt,
                    IsMine = m.SenderId == chatUserId.Value,
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

        // Send Message
        public async Task<ServiceResult<long>> SendMessageAsync(Guid conversationId, Guid senderId, string cipherText,
            byte messageType)
        {
            var senderChatUserId = await GetActiveChatUserIdAsync(senderId);
            if (senderChatUserId == null)
                return new ServiceResult<long> { Success = false, Message = "Lỗi xác thực người gửi." };

            try
            {
                // Check quyền gửi (theo ChatUser)
                var isMember = await db.ConversationMembers
                    .AnyAsync(cm => cm.ConversationId == conversationId && cm.ChatUserId == senderChatUserId.Value);

                if (!isMember) return new ServiceResult<long> { Success = false, Message = "Không có quyền gửi tin." };

                var message = new Message
                {
                    ConversationId = conversationId,
                    SenderId = senderChatUserId.Value,
                    MessageType = messageType,
                    CipherText = cipherText,
                    CreatedAt = DateTime.UtcNow.AddHours(7)
                };

                db.Messages.Add(message);
                await db.SaveChangesAsync();

                return new ServiceResult<long> { Success = true, Data = message.Id };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending message");
                return new ServiceResult<long> { Success = false, Message = "Lỗi gửi tin." };
            }
        }

        // Create message (for file upload)
        public async Task<ServiceResult<long>> CreateMessageAsync(
            Guid conversationId,
            Guid senderId,
            string cipherText,
            byte messageType)
        {
            return await SendMessageAsync(conversationId, senderId, cipherText, messageType);
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
                    // Nếu upload thất bại, XÓA message tạm
                    await DeleteMessageAsync(messageId);
                    return uploadResult;
                }

                // Chỉ lưu attachment metadata
                var attachment = new MessageAttachment
                {
                    MessageId = messageId,
                    CloudinaryUrl = uploadResult.PublicId,
                    ResourceType = uploadResult.ResourceType ?? "raw",
                    Format = uploadResult.Format ?? "",
                    FileName = uploadResult.FileName ?? "",
                    UploadedAt = DateTime.UtcNow.AddHours(7)
                };

                db.MessageAttachments.Add(attachment);
                await db.SaveChangesAsync();

                logger.LogInformation("File uploaded and attachment saved for message {MsgId}", messageId);

                return uploadResult;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading file for message {MsgId}", messageId);

                // Cleanup: Xóa message tạm nếu có lỗi
                await DeleteMessageAsync(messageId);

                return new CloudinaryUploadResult
                {
                    Success = false,
                    Message = "Lỗi khi upload file"
                };
            }
        }

        public async Task<ServiceResult> ChangePinAsync(Guid userId, string oldPinHash, string newPinHash,
            string newEncryptedPrivateKey)
        {
            // Tìm ChatUser đang active
            var chatUser = await db.ChatUsers.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive == true);
            if (chatUser == null)
            {
                return new ServiceResult { Success = false, Message = "Người dùng chưa đăng ký chat." };
            }

            if (chatUser.PinCodeHash != oldPinHash)
            {
                return new ServiceResult { Success = false, Message = "Mã PIN cũ không chính xác." };
            }

            // Tìm RSA Key gắn với ChatUser này
            var rsaKey = await db.UserRsaKeys.FirstOrDefaultAsync(k => k.ChatUserId == chatUser.Id && k.IsActive);
            if (rsaKey == null)
            {
                return new ServiceResult { Success = false, Message = "Không tìm thấy khóa RSA." };
            }

            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                chatUser.PinCodeHash = newPinHash;
                chatUser.UpdatedAt = DateTime.UtcNow.AddHours(7);
                rsaKey.PrivateKeyEncrypted = newEncryptedPrivateKey;

                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return new ServiceResult { Success = true, Message = "Đổi mã PIN thành công." };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ServiceResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<ServiceResult> ResetChatAccountAsync(Guid userId)
        {
            var chatUser = await db.ChatUsers.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive == true);
            
            if (chatUser != null)
            {
                chatUser.IsActive = false; 
                var userRsaKey = await db.UserRsaKeys.FirstOrDefaultAsync(k => k.ChatUserId == chatUser.Id);
                if (userRsaKey != null && userRsaKey.IsActive)
                {
                    userRsaKey.IsActive = false;
                }
                // Không xóa dữ liệu vật lý -> Tránh lỗi FK ở phía người nhận tin nhắn cũ
                await db.SaveChangesAsync();
            }
            logger.LogInformation("User {UserId} has reset their chat account (Soft Delete).", userId);
            return new ServiceResult { Success = true, Message = "Reset tài khoản chat thành công." };
        }

        // Delete message with attachment
        public async Task<bool> DeleteMessageWithAttachmentAsync(long messageId, Guid userId)
        {
            var chatUser = await db.ChatUsers.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive == true);
            if (chatUser == null) return false;
            var chatUserId = chatUser.UserId;

            try
            {
                var message = await db.Messages
                    .Include(m => m.Conversation)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null) return false;

                var isMember = await db.ConversationMembers
                    .AnyAsync(cm => cm.ConversationId == message.ConversationId && cm.ChatUserId == chatUserId);

                if (!isMember || message.SenderId != userId)
                {
                    return false;
                }

                // Get attachment
                var attachment = await db.MessageAttachments
                    .FirstOrDefaultAsync(a => a.MessageId == messageId);

                if (attachment != null)
                {
                    string publicIdToDelete = attachment.CloudinaryUrl;
                    //check data cũ
                    if (publicIdToDelete.StartsWith("http"))
                    {
                        publicIdToDelete = ExtractPublicIdFromUrl(publicIdToDelete);
                    }

                    await cloudinaryService.DeleteChatFileAsync(publicIdToDelete, attachment.ResourceType);

                    db.MessageAttachments.Remove(attachment);
                }

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