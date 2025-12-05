using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.DTOs;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly ChatService _chatService;
        private readonly ILogger<ChatHub> _logger;

        // Dictionary để track user connections
        // Key: UserId (Gốc), Value: List of ConnectionIds
        private static readonly Dictionary<Guid, HashSet<string>> _userConnections = new();
        private static readonly object _lock = new();

        public ChatHub(AppDbContext db, ChatService chatService, ILogger<ChatHub> logger)
        {
            _db = db;
            _chatService = chatService;
            _logger = logger;
        }

        // Khi client kết nối
        public override async Task OnConnectedAsync()
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user != null)
            {
                lock (_lock)
                {
                    if (!_userConnections.ContainsKey(user.Id))
                    {
                        _userConnections[user.Id] = new HashSet<string>();
                    }

                    _userConnections[user.Id].Add(Context.ConnectionId);
                }

                _logger.LogInformation("User {UserId} connected with ConnectionId {ConnectionId}",
                    user.Id, Context.ConnectionId);

                // Notify user's contacts that they are online
                await NotifyContactsUserStatus(user.Id, true);
            }

            await base.OnConnectedAsync();
        }

        // Khi client ngắt kết nối
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user != null)
            {
                lock (_lock)
                {
                    if (_userConnections.ContainsKey(user.Id))
                    {
                        _userConnections[user.Id].Remove(Context.ConnectionId);

                        if (_userConnections[user.Id].Count == 0)
                        {
                            _userConnections.Remove(user.Id);
                        }
                    }
                }

                _logger.LogInformation("User {UserId} disconnected with ConnectionId {ConnectionId}",
                    user.Id, Context.ConnectionId);

                if (!IsUserOnline(user.Id))
                {
                    await NotifyContactsUserStatus(user.Id, false);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Join vào conversation room
        public async Task JoinConversation(string conversationId)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user == null) return;

            // 1. Lấy Active ChatUser
            var chatUser = await _db.ChatUsers.FirstOrDefaultAsync(cu => cu.UserId == user.Id && cu.IsActive);
            if (chatUser == null)
            {
                await Clients.Caller.SendAsync("Error", "Chat account not active or registered");
                return;
            }

            if (!Guid.TryParse(conversationId, out var convId))
            {
                await Clients.Caller.SendAsync("Error", "Invalid conversation ID");
                return;
            }

            // 2. Kiểm tra user có phải member của conversation không (Dùng ChatUserId)
            var isMember = await _db.ConversationMembers
                .AnyAsync(cm => cm.ConversationId == convId && cm.ChatUserId == chatUser.Id);

            if (!isMember)
            {
                await Clients.Caller.SendAsync("Error", "You are not a member of this conversation");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);

            _logger.LogInformation("User {UserId} (ChatUser: {ChatUserId}) joined conversation {ConvId}", user.Id, chatUser.Id, convId);

            // Notify others
            await Clients.OthersInGroup(conversationId).SendAsync("UserJoinedConversation", new
            {
                userId = user.Id, // Vẫn gửi UserId gốc để client map avatar/status
                chatUserId = chatUser.Id,
                username = user.Username
            });
        }

        public async Task LeaveConversation(string conversationId)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user == null) return;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);

            _logger.LogInformation("User {UserId} left conversation {ConvId}", user.Id, conversationId);

            await Clients.OthersInGroup(conversationId).SendAsync("UserLeftConversation", new
            {
                userId = user.Id,
                username = user.Username
            });
        }

        // Gửi tin nhắn realtime
        public async Task SendMessage(SendMessageRequest request)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user == null)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            // 1. Lấy Active ChatUser
            var chatUser = await _db.ChatUsers.FirstOrDefaultAsync(cu => cu.UserId == user.Id && cu.IsActive);
            if (chatUser == null)
            {
                await Clients.Caller.SendAsync("Error", "Chat account not active");
                return;
            }

            try
            {
                if (!Guid.TryParse(request.ConversationId, out var convId))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid conversation ID");
                    return;
                }

                // Send message through service (Service đã xử lý logic ChatUser)
                var result = await _chatService.SendMessageAsync(
                    convId,
                    user.Id, // Service sẽ tự resolve ChatUserId từ UserId này
                    request.CipherText,
                    request.MessageType
                );

                if (!result.Success)
                {
                    await Clients.Caller.SendAsync("Error", result.Message);
                    return;
                }

                // Lấy attachment nếu có
                MessageAttachmentDto? attachment = null;
                if (request.MessageType != 0)
                {
                    attachment = await _db.MessageAttachments
                        .Where(a => a.MessageId == result.Data)
                        .Select(a => new MessageAttachmentDto
                        {
                            Url = a.CloudinaryUrl,
                            FileName = a.FileName,
                            ResourceType = a.ResourceType,
                            Format = a.Format
                        })
                        .FirstOrDefaultAsync();
                }

                // Broadcast
                await Clients.Group(request.ConversationId).SendAsync("ReceiveMessage", new
                {
                    messageId = result.Data,
                    conversationId = convId,
                    senderId = user.Id,
                    senderUsername = user.Username,
                    cipherText = request.CipherText,
                    messageType = request.MessageType,
                    createdAt = DateTime.UtcNow.AddHours(7),
                });

                // Thông báo cập nhật danh bạ
                // Cần tìm UserId gốc của các thành viên khác để gửi notify
                var otherMemberUserIds = await _db.ConversationMembers
                    .Where(cm => cm.ConversationId == convId && cm.ChatUserId != chatUser.Id)
                    .Include(cm => cm.ChatUser)
                    .Select(cm => cm.ChatUser.UserId) // Lấy UserId gốc
                    .ToListAsync();

                await NotifyUsersUpdateContact(otherMemberUserIds);

                _logger.LogInformation("Message {MsgId} sent by user {UserId} (ChatUser {ChatUserId})",
                    result.Data, user.Id, chatUser.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message in SignalR");
                await Clients.Caller.SendAsync("Error", "Failed to send message");
            }
        }

        public async Task SendFileMessage(SendFileMessageRequest request)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user == null)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            var chatUser = await _db.ChatUsers.FirstOrDefaultAsync(cu => cu.UserId == user.Id && cu.IsActive);
            if (chatUser == null)
            {
                await Clients.Caller.SendAsync("Error", "Chat account not active");
                return;
            }

            try
            {
                if (!Guid.TryParse(request.ConversationId, out var convId))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid conversation ID");
                    return;
                }

                // Kiểm tra member bằng ChatUserId
                var isMember = await _db.ConversationMembers
                    .AnyAsync(cm => cm.ConversationId == convId && cm.ChatUserId == chatUser.Id);

                if (!isMember)
                {
                    await Clients.Caller.SendAsync("Error", "You are not a member of this conversation");
                    return;
                }

                // Verify message exists và thuộc về ChatUser này (SenderId trong DB là ChatUserId)
                var messageExists = await _db.Messages
                    .AnyAsync(m => m.Id == request.MessageId && m.SenderId == chatUser.Id);

                if (!messageExists)
                {
                    await Clients.Caller.SendAsync("Error", "Message not found or unauthorized");
                    return;
                }

                // Broadcast
                await Clients.Group(request.ConversationId).SendAsync("ReceiveMessage", new
                {
                    messageId = request.MessageId,
                    conversationId = convId,
                    senderId = user.Id,
                    senderUsername = user.Username,
                    cipherText = request.CipherText,
                    messageType = request.MessageType,
                    createdAt = DateTime.UtcNow.AddHours(7),
                    pinExchange = (string?)null,
                    attachment = request.Attachment
                });

                // Notify
                var otherMemberUserIds = await _db.ConversationMembers
                    .Where(cm => cm.ConversationId == convId && cm.ChatUserId != chatUser.Id)
                    .Include(cm => cm.ChatUser)
                    .Select(cm => cm.ChatUser.UserId)
                    .ToListAsync();

                await NotifyUsersUpdateContact(otherMemberUserIds);

                _logger.LogInformation("File message {MsgId} sent by {UserId}", request.MessageId, user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending file message {MsgId}", request.MessageId);
                await Clients.Caller.SendAsync("Error", "Failed to send file message");
            }
        }

        // Helper: Gửi thông báo cập nhật danh bạ cho danh sách user (theo UserId gốc)
        private async Task NotifyUsersUpdateContact(List<Guid> userIds)
        {
            foreach (var userId in userIds)
            {
                // Gửi qua User ID provider
                await Clients.User(userId.ToString()).SendAsync("UpdateContactList");

                // Gửi trực tiếp vào ConnectionId (nếu đang online)
                List<string>? connections = null;
                lock (_lock)
                {
                    if (_userConnections.TryGetValue(userId, out var set))
                    {
                        connections = set.ToList();
                    }
                }

                if (connections != null && connections.Count > 0)
                {
                    await Clients.Clients(connections).SendAsync("UpdateContactList");
                }
            }
        }

        // User đang typing
        public async Task UserTyping(string conversationId)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user == null) return;

            await Clients.OthersInGroup(conversationId).SendAsync("UserTyping", new
            {
                userId = user.Id,
                username = user.Username,
                conversationId
            });
        }

        public async Task UserStoppedTyping(string conversationId)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user == null) return;

            await Clients.OthersInGroup(conversationId).SendAsync("UserStoppedTyping", new
            {
                userId = user.Id,
                username = user.Username,
                conversationId
            });
        }

        // Mark messages as read
        public async Task MarkMessagesAsRead(string conversationId, long lastMessageId)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user == null) return;

            if (!Guid.TryParse(conversationId, out var convId)) return;

            await Clients.OthersInGroup(conversationId).SendAsync("MessagesRead", new
            {
                userId = user.Id,
                conversationId = convId,
                lastMessageId
            });
        }

        // --- Helpers quản lý connection ---
        private static bool IsUserOnline(Guid userId)
        {
            lock (_lock)
            {
                return _userConnections.ContainsKey(userId) && _userConnections[userId].Count > 0;
            }
        }

        private async Task NotifyContactsUserStatus(Guid userId, bool isOnline)
        {
            try
            {
                // Cần tìm các conversation mà user tham gia, nhưng bảng ConversationMembers dùng ChatUserId.
                // Ta phải join ngược từ UserId -> ChatUsers -> ConversationMembers
                
                // 1. Lấy tất cả ChatUserIds của user này (cũ và mới, để thông báo hết)
                var chatUserIds = await _db.ChatUsers
                    .Where(cu => cu.UserId == userId)
                    .Select(cu => cu.Id)
                    .ToListAsync();

                if (!chatUserIds.Any()) return;

                // 2. Lấy danh sách ConversationId
                var conversationIds = await _db.ConversationMembers
                    .Where(cm => chatUserIds.Contains(cm.ChatUserId))
                    .Select(cm => cm.ConversationId.ToString())
                    .Distinct()
                    .ToListAsync();

                foreach (var convId in conversationIds)
                {
                    await Clients.OthersInGroup(convId).SendAsync("UserStatusChanged", new
                    {
                        userId,
                        isOnline,
                        timestamp = DateTime.UtcNow.AddHours(7)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying contacts about user status");
            }
        }

        public async Task GetOnlineStatus(List<string> userIds)
        {
            var onlineUsers = new List<string>();
            foreach (var userIdStr in userIds)
            {
                if (Guid.TryParse(userIdStr, out var userId) && IsUserOnline(userId))
                {
                    onlineUsers.Add(userIdStr);
                }
            }
            await Clients.Caller.SendAsync("OnlineStatusResponse", onlineUsers);
        }

        public class SendFileMessageRequest
        {
            public string ConversationId { get; set; } = string.Empty;
            public long MessageId { get; set; }
            public string CipherText { get; set; } = string.Empty;
            public byte MessageType { get; set; }
            public FileAttachmentData? Attachment { get; set; }
        }

        public class FileAttachmentData
        {
            public string Url { get; set; } = string.Empty;
            public string? FileName { get; set; }
            public string? ResourceType { get; set; }
            public string? Format { get; set; }
        }

        public class SendMessageRequest
        {
            public string ConversationId { get; set; } = string.Empty;
            public string CipherText { get; set; } = string.Empty;
            public byte MessageType { get; set; }
        }
    }
}