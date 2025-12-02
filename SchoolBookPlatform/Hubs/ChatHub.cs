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

        public async Task SendEncryptedMessage(
            int threadId, 
            int segmentId, 
            string content,
            string encryptionIV,
            string encryptedKey,
            bool isEncrypted,
            int? attachmentId = null,
            string attachmentType = null,
            string attachmentName = null,
            long? attachmentSize = null)
        {
            try
            {
                var userId = Context.User?.Identity?.Name;
                
                _logger.LogInformation($" SendEncryptedMessage - Thread:{threadId}, Segment:{segmentId}, Encrypted:{isEncrypted}");
                
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("User not authenticated");
                    throw new HubException("User not authenticated");
                }
                
                if (threadId <= 0 || segmentId <= 0)
                {
                    _logger.LogError($"Invalid IDs - Thread:{threadId}, Segment:{segmentId}");
                    throw new HubException("Invalid thread or segment ID");
                }

                // Create message object with encryption metadata
                var message = new ChatMessage 
                { 
                    UserId = userId, 
                    Content = content, 
                    Timestamp = DateTime.UtcNow,
                    IsEncrypted = isEncrypted,
                    EncryptionIV = encryptionIV,
                    EncryptedKey = encryptedKey,
                    AttachmentId = attachmentId,
                    AttachmentType = attachmentType,
                    AttachmentName = attachmentName,
                    AttachmentSize = attachmentSize
                };

                _logger.LogInformation($" Saving encrypted message to segment {segmentId}");
                
                try
                {
                    await _chatService.AddMessageToSegment(segmentId, message);
                    _logger.LogInformation($" Encrypted message saved");
                }
                catch (Exception saveEx)
                {
                    _logger.LogError($"Failed to save: {saveEx.Message}");
                    throw new HubException($"Failed to save message: {saveEx.Message}");
                }

                _logger.LogInformation($" Broadcasting encrypted message to thread {threadId}");
                
                // Broadcast to all clients in the thread
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "ReceiveEncryptedMessage", 
                    userId, 
                    content,
                    message.Timestamp.ToString("o"),
                    isEncrypted,
                    encryptionIV,
                    encryptedKey,
                    attachmentId,
                    attachmentType,
                    attachmentName,
                    attachmentSize
                );

                _logger.LogInformation($" Encrypted message broadcast successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($" SendEncryptedMessage ERROR: {ex.Message}");
                throw new HubException($"Failed to send message: {ex.Message}");
            }
        }
        
        public async Task JoinThread(int threadId)
        {
            try
            {
                var userId = Context.User?.Identity?.Name;
                
                var thread = _chatService.GetThreadById(threadId, userId);
                if (thread == null)
                {
                    throw new HubException("Thread not found or access denied");
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, $"thread-{threadId}");
                
                _logger.LogInformation($" User {userId} joined thread {threadId}");

                await Clients.OthersInGroup($"thread-{threadId}").SendAsync(
                    "UserJoined", 
                    userId
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error joining thread: {ex.Message}");
                throw new HubException($"Failed to join thread: {ex.Message}");
            }
        }

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

        public async Task SendTyping(int threadId, bool isTyping)
        {
            var userId = Context.User?.Identity?.Name;
            
            await Clients.OthersInGroup($"thread-{threadId}").SendAsync(
                "UserTyping", 
                userId, 
                isTyping
            );
        }

        public async Task StartNewSegment(int threadId)
        {
            try
            {
                _logger.LogInformation($"Starting new segment for thread {threadId}");
                
                var segment = await _chatService.CreateSegment(threadId, false);
                
                _logger.LogInformation($" Created segment {segment.Id}");
                
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "SegmentStarted", 
                    segment.Id,
                    "New segment started"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error creating segment: {ex.Message}");
                throw new HubException($"Failed to start segment: {ex.Message}");
            }
        }
        

        public async Task StartProtectedSegment(int threadId, string pin)
        {
            try
            {
                _logger.LogInformation($"Starting protected segment for thread {threadId}");
                
                var segment = await _chatService.CreateSegment(threadId, true, pin);
                
                _logger.LogInformation($" Created protected segment {segment.Id}");
                
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "SegmentStarted",
                    segment.Id, 
                    "Protected segment started"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error creating protected segment: {ex.Message}");
                throw new HubException($"Failed to start protected segment: {ex.Message}");
            }
        }
    }
}