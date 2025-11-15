using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChatService _chatService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ChatService chatService, ILogger<ChatHub> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.Identity?.Name;
            _logger.LogInformation($"User {userId} connected with connection ID: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User?.Identity?.Name;
            _logger.LogInformation($"User {userId} disconnected");
            await base.OnDisconnectedAsync(exception);
        }

        // Gửi tin nhắn
        public async Task SendMessage(int threadId, int segmentId, string content)
        {
            try
            {
                var userId = Context.User?.Identity?.Name;
                
                _logger.LogInformation($"SendMessage called - ThreadId: {threadId}, SegmentId: {segmentId}, UserId: {userId}, Content: {content}");
                
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("User not authenticated");
                    throw new HubException("User not authenticated");
                }
                
                if (threadId <= 0)
                {
                    _logger.LogError($"Invalid threadId: {threadId}");
                    throw new HubException("Invalid thread ID");
                }
                
                if (segmentId <= 0)
                {
                    _logger.LogError($"Invalid segmentId: {segmentId}");
                    throw new HubException("Invalid segment ID. Please create a segment first.");
                }
                
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogError("Message content is empty");
                    throw new HubException("Message content cannot be empty");
                }

                var message = new ChatMessage 
                { 
                    UserId = userId, 
                    Content = content, 
                    Timestamp = DateTime.UtcNow 
                };

                // Lưu tin nhắn vào database
                await _chatService.AddMessageToSegment(segmentId, message);

                _logger.LogInformation($"Message saved to database successfully");

                // Broadcast đến tất cả members trong thread
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "ReceiveMessage", 
                    userId, 
                    content, 
                    message.Timestamp.ToString("o") // ISO 8601 format
                );

                _logger.LogInformation($"Message broadcast successfully from {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in SendMessage: {ex.Message}\nStackTrace: {ex.StackTrace}");
                throw new HubException($"Failed to send message: {ex.Message}");
            }
        }

        // Join thread (room)
        public async Task JoinThread(int threadId)
        {
            try
            {
                var userId = Context.User?.Identity?.Name;
                
                // Kiểm tra user có quyền join thread không
                var thread = _chatService.GetThreadById(threadId, userId);
                if (thread == null)
                {
                    throw new HubException("Thread not found or access denied");
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, $"thread-{threadId}");
                
                _logger.LogInformation($"User {userId} joined thread {threadId}");

                // Thông báo cho các thành viên khác
                await Clients.OthersInGroup($"thread-{threadId}").SendAsync(
                    "UserJoined", 
                    userId
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error joining thread: {ex.Message}");
                throw new HubException($"Failed to join thread: {ex.Message}");
            }
        }

        // Leave thread
        public async Task LeaveThread(int threadId)
        {
            var userId = Context.User?.Identity?.Name;
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"thread-{threadId}");
            
            await Clients.OthersInGroup($"thread-{threadId}").SendAsync(
                "UserLeft", 
                userId
            );
            
            _logger.LogInformation($"User {userId} left thread {threadId}");
        }

        // Typing indicator
        public async Task SendTyping(int threadId, bool isTyping)
        {
            var userId = Context.User?.Identity?.Name;
            
            await Clients.OthersInGroup($"thread-{threadId}").SendAsync(
                "UserTyping", 
                userId, 
                isTyping
            );
        }

        // Tạo segment mới
        public async Task StartNewSegment(int threadId)
        {
            try
            {
                var segment = await _chatService.CreateSegment(threadId, false);
                
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "SegmentStarted", 
                    segment.Id,
                    "New segment started"
                );
            }
            catch (Exception ex)
            {
                throw new HubException($"Failed to start segment: {ex.Message}");
            }
        }

        // Tạo protected segment
        public async Task StartProtectedSegment(int threadId, string pin)
        {
            try
            {
                var segment = await _chatService.CreateSegment(threadId, true, pin);
                
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "SegmentStarted",
                    segment.Id, 
                    "Protected segment started"
                );
            }
            catch (Exception ex)
            {
                throw new HubException($"Failed to start protected segment: {ex.Message}");
            }
        }
        
        // Gửi tin nhắn với attachment
        public async Task SendMessageWithAttachment(int threadId, int segmentId, string content, int attachmentId, string attachmentType, string attachmentName, long attachmentSize)
        {
            try
            {
                var userId = Context.User?.Identity?.Name;
        
                _logger.LogInformation($"SendMessageWithAttachment - AttachmentId: {attachmentId}");
        
                if (string.IsNullOrEmpty(userId))
                    throw new HubException("User not authenticated");
        
                if (threadId <= 0 || segmentId <= 0)
                    throw new HubException("Invalid thread or segment ID");

                var message = new ChatMessage 
                { 
                    UserId = userId, 
                    Content = content ?? "",
                    Timestamp = DateTime.UtcNow,
                    AttachmentId = attachmentId,
                    AttachmentType = attachmentType,
                    AttachmentName = attachmentName,
                    AttachmentSize = attachmentSize
                };

                await _chatService.AddMessageToSegment(segmentId, message);

                _logger.LogInformation($"Message with attachment saved");

                // Broadcast
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "ReceiveMessage", 
                    userId, 
                    content ?? "",
                    message.Timestamp.ToString("o"),
                    attachmentId,
                    attachmentType,
                    attachmentName,
                    attachmentSize
                );

                _logger.LogInformation($"Message broadcast successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                throw new HubException($"Failed to send message: {ex.Message}");
            }
        }

        // Mark messages as read
        public async Task MarkAsRead(int threadId, int segmentId)
        {
            var userId = Context.User?.Identity?.Name;
            
            // Broadcast đến các thành viên khác
            await Clients.OthersInGroup($"thread-{threadId}").SendAsync(
                "MessagesRead", 
                userId, 
                segmentId
            );
        }
    }
}