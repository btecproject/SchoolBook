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
        // Key: UserId, Value: List of ConnectionIds
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

                        // Nếu user không còn connection nào thì xóa khỏi dictionary
                        if (_userConnections[user.Id].Count == 0)
                        {
                            _userConnections.Remove(user.Id);
                        }
                    }
                }

                _logger.LogInformation("User {UserId} disconnected with ConnectionId {ConnectionId}",
                    user.Id, Context.ConnectionId);

                // Check if user is completely offline
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

            if (!Guid.TryParse(conversationId, out var convId))
            {
                await Clients.Caller.SendAsync("Error", "Invalid conversation ID");
                return;
            }

            // Kiểm tra user có phải member của conversation không
            var isMember = await _db.ConversationMembers
                .AnyAsync(cm => cm.ConversationId == convId && cm.UserId == user.Id);

            if (!isMember)
            {
                await Clients.Caller.SendAsync("Error", "You are not a member of this conversation");
                return;
            }

            // Join vào group (room) của conversation
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);

            _logger.LogInformation("User {UserId} joined conversation {ConvId}", user.Id, convId);

            // Notify others in the conversation
            await Clients.OthersInGroup(conversationId).SendAsync("UserJoinedConversation", new
            {
                userId = user.Id,
                username = user.Username
            });
        }

        // Leave conversation room
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

            try
            {
                // Validate conversation ID
                if (!Guid.TryParse(request.ConversationId, out var convId))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid conversation ID");
                    return;
                }

                // Send message through service
                var result = await _chatService.SendMessageAsync(
                    convId,
                    user.Id,
                    request.CipherText,
                    request.MessageType
                );

                if (!result.Success)
                {
                    await Clients.Caller.SendAsync("Error", result.Message);
                    return;
                }

                // Lấy attachment nếu có (cho file messages)
                MessageAttachmentDto? attachment = null;
                if (request.MessageType != 0) // Không phải text
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

                // Broadcast to all members in conversation (including sender)
                await Clients.Group(request.ConversationId).SendAsync("ReceiveMessage", new
                {
                    messageId = result.Data,
                    conversationId = convId,
                    senderId = user.Id,
                    senderUsername = user.Username,
                    cipherText = request.CipherText,
                    messageType = request.MessageType,
                    createdAt = DateTime.UtcNow,
                    pinExchange = request.PinExchange
                });

                //Fix: bắt buộc thành viên chat khác phải updateContactList (trường hợp A nhắn B, B chưa ở đoạn chat bao giờ)
                var otherMembers = await _db.ConversationMembers
                    .Where(cm => cm.ConversationId == convId && cm.UserId != user.Id)
                    .Select(cm => cm.UserId.ToString())
                    .ToListAsync();

                foreach (var memberId in otherMembers)
                {
                    await Clients.User(memberId).SendAsync("UpdateContactList");
                }

                _logger.LogInformation("Message {MsgId} sent by user {UserId} in conversation {ConvId}",
                    result.Data, user.Id, convId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message in SignalR");
                await Clients.Caller.SendAsync("Error", "Failed to send message");
            }
        }

        // Gửi PIN Exchange
        public async Task SendPinExchange(PinExchangeRequest request)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user == null)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            try
            {
                if (!Guid.TryParse(request.ConversationId, out var convId))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid conversation ID");
                    return;
                }

                if (!Guid.TryParse(request.RecipientId, out var recipientId))
                {
                    await Clients.Caller.SendAsync("Error", "Invalid recipient ID");
                    return;
                }

                // Send PIN exchange through service
                var result = await _chatService.SendPinExchangeMessageAsync(
                    convId,
                    user.Id,
                    recipientId,
                    request.EncryptedPin
                );

                if (!result.Success)
                {
                    await Clients.Caller.SendAsync("Error", result.Message);
                    return;
                }

                // Broadcast PIN exchange to conversation members
                await Clients.Group(request.ConversationId).SendAsync("ReceivePinExchange", new
                {
                    conversationId = convId,
                    senderId = user.Id,
                    senderUsername = user.Username,
                    encryptedPin = request.EncryptedPin,
                    createdAt = DateTime.UtcNow
                });
                //Fix: bắt buộc thành viên chat khác phải updateContactList (trường hợp A nhắn B, B chưa ở đoạn chat bao giờ)
                await Clients.User(request.RecipientId).SendAsync("UpdateContactList");
                _logger.LogInformation("PIN exchange sent by user {UserId} in conversation {ConvId}",
                    user.Id, convId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending PIN exchange in SignalR");
                await Clients.Caller.SendAsync("Error", "Failed to send PIN exchange");
            }
        }

        // User đang typing
        public async Task UserTyping(string conversationId)
        {
            var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
            if (user == null) return;

            // Notify others in the conversation
            await Clients.OthersInGroup(conversationId).SendAsync("UserTyping", new
            {
                userId = user.Id,
                username = user.Username,
                conversationId
            });
        }

        // User stopped typing
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

            if (!Guid.TryParse(conversationId, out var convId))
            {
                return;
            }

            // Notify others that messages have been read
            await Clients.OthersInGroup(conversationId).SendAsync("MessagesRead", new
            {
                userId = user.Id,
                conversationId = convId,
                lastMessageId
            });

            _logger.LogDebug("User {UserId} marked messages as read in conversation {ConvId}",
                user.Id, convId);
        }

        // Helper: Check if user is online
        private static bool IsUserOnline(Guid userId)
        {
            lock (_lock)
            {
                return _userConnections.ContainsKey(userId) && _userConnections[userId].Count > 0;
            }
        }

        // Helper: Get all connection IDs for a user
        private static List<string> GetUserConnectionIds(Guid userId)
        {
            lock (_lock)
            {
                return _userConnections.ContainsKey(userId)
                    ? _userConnections[userId].ToList()
                    : new List<string>();
            }
        }

        // Helper: Notify contacts about user status
        private async Task NotifyContactsUserStatus(Guid userId, bool isOnline)
        {
            try
            {
                // Get all conversations where user is a member
                var conversationIds = await _db.ConversationMembers
                    .Where(cm => cm.UserId == userId)
                    .Select(cm => cm.ConversationId.ToString())
                    .ToListAsync();

                // Notify all those conversations
                foreach (var convId in conversationIds)
                {
                    await Clients.OthersInGroup(convId).SendAsync("UserStatusChanged", new
                    {
                        userId,
                        isOnline,
                        timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying contacts about user status");
            }
        }

        // Get online status of users
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


 public async Task SendFileMessage(SendFileMessageRequest request)
{
    var user = await Context.GetHttpContext()!.GetCurrentUserAsync(_db);
    if (user == null)
    {
        await Clients.Caller.SendAsync("Error", "User not authenticated");
        return;
    }

    try
    {
        // Validate conversation ID
        if (!Guid.TryParse(request.ConversationId, out var convId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid conversation ID");
            return;
        }

        // Kiểm tra user có phải member của conversation không
        var isMember = await _db.ConversationMembers
            .AnyAsync(cm => cm.ConversationId == convId && cm.UserId == user.Id);

        if (!isMember)
        {
            await Clients.Caller.SendAsync("Error", "You are not a member of this conversation");
            return;
        }

        // Verify message exists và thuộc về user này
        var messageExists = await _db.Messages
            .AnyAsync(m => m.Id == request.MessageId && m.SenderId == user.Id);

        if (!messageExists)
        {
            await Clients.Caller.SendAsync("Error", "Message not found or unauthorized");
            return;
        }

        // Broadcast file message với đầy đủ attachment data
        await Clients.Group(request.ConversationId).SendAsync("ReceiveMessage", new
        {
            messageId = request.MessageId,
            conversationId = convId,
            senderId = user.Id,
            senderUsername = user.Username,
            cipherText = request.CipherText,
            messageType = request.MessageType,
            createdAt = DateTime.UtcNow,
            pinExchange = (string?)null,
            attachment = request.Attachment // ✅ GỬI KÈM ATTACHMENT DATA
        });
        
        // Thông báo cho các members khác cập nhật contact list
        var otherMembers = await _db.ConversationMembers
            .Where(cm => cm.ConversationId == convId && cm.UserId != user.Id)
            .Select(cm => cm.UserId.ToString())
            .ToListAsync();

        foreach (var memberId in otherMembers)
        {
            await Clients.User(memberId).SendAsync("UpdateContactList");
        }
        
        _logger.LogInformation("File message {MsgId} (type: {Type}) sent by user {UserId} in conversation {ConvId}", 
            request.MessageId, request.MessageType, user.Id, convId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending file message {MsgId} in SignalR", request.MessageId);
        await Clients.Caller.SendAsync("Error", "Failed to send file message");
    }
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

        // Request models for SignalR
        public class SendMessageRequest
        {
            public string ConversationId { get; set; } = string.Empty;
            public string CipherText { get; set; } = string.Empty;
            public byte MessageType { get; set; }
            public string? PinExchange { get; set; }
        }

        public class PinExchangeRequest
        {
            public string ConversationId { get; set; } = string.Empty;
            public string RecipientId { get; set; } = string.Empty;
            public string EncryptedPin { get; set; } = string.Empty;
        }
    }
}