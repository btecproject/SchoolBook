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

        public async Task SendMessage(int threadId, int segmentId, string content)
        {
            try
            {
                var userId = Context.User?.Identity?.Name;
                
                _logger.LogInformation($"SendMessage START - Thread:{threadId}, Segment:{segmentId}, User:{userId}");
                
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
                    throw new HubException("Invalid segment ID");
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

                _logger.LogInformation($"Calling AddMessageToSegment...");
                
                try
                {
                    await _chatService.AddMessageToSegment(segmentId, message);
                    _logger.LogInformation($"Message saved to segment {segmentId}");
                }
                catch (Exception saveEx)
                {
                    _logger.LogError($"AddMessageToSegment failed: {saveEx.GetType().Name}: {saveEx.Message}");
                    _logger.LogError($"Stack trace: {saveEx.StackTrace}");
                    
                    if (saveEx.InnerException != null)
                    {
                        _logger.LogError($"Inner exception: {saveEx.InnerException.Message}");
                    }
                    
                    throw new HubException($"Failed to save message: {saveEx.Message}");
                }

                _logger.LogInformation($"Broadcasting message...");
                
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "ReceiveMessage", 
                    userId, 
                    content, 
                    message.Timestamp.ToString("o")
                );

                _logger.LogInformation($"Message sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendMessage ERROR: {ex.GetType().Name}: {ex.Message}");
                _logger.LogError($"Stack: {ex.StackTrace}");
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
                
                _logger.LogInformation($"User {userId} joined thread {threadId}");

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
                
                _logger.LogInformation($"Created segment {segment.Id} with MessagesJson: {segment.MessagesJson}");
                
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "SegmentStarted", 
                    segment.Id,
                    "New segment started"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating segment: {ex.Message}");
                throw new HubException($"Failed to start segment: {ex.Message}");
            }
        }

        public async Task StartProtectedSegment(int threadId, string pin)
        {
            try
            {
                _logger.LogInformation($"Starting protected segment for thread {threadId}");
                
                var segment = await _chatService.CreateSegment(threadId, true, pin);
                
                _logger.LogInformation($"Created protected segment {segment.Id}");
                
                await Clients.Group($"thread-{threadId}").SendAsync(
                    "SegmentStarted",
                    segment.Id, 
                    "Protected segment started"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating protected segment: {ex.Message}");
                throw new HubException($"Failed to start protected segment: {ex.Message}");
            }
        }
        
        public async Task SendMessageWithAttachment(int threadId, int segmentId, string content, 
            int attachmentId, string attachmentType, string attachmentName, long attachmentSize)
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

                try
                {
                    await _chatService.AddMessageToSegment(segmentId, message);
                    _logger.LogInformation($"Message with attachment saved");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to save message: {ex.Message}");
                    throw new HubException($"Failed to save message: {ex.Message}");
                }

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

                _logger.LogInformation($"Message with attachment broadcast");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                throw new HubException($"Failed to send message: {ex.Message}");
            }
        }

        public async Task MarkAsRead(int threadId, int segmentId)
        {
            var userId = Context.User?.Identity?.Name;
            
            await Clients.OthersInGroup($"thread-{threadId}").SendAsync(
                "MessagesRead", 
                userId, 
                segmentId
            );
        }
    }
}