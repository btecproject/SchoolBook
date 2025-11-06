using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Hubs
{
    [Authorize]  // Yêu cầu JWT token
    public class ChatHub : Hub
    {
        private readonly ChatService _chatService;

        public ChatHub(ChatService chatService)
        {
            _chatService = chatService;
        }

        public async Task SendMessage(int threadId, int segmentId, string content)
        {
            var userId = Context.UserIdentifier;  // Từ JWT claim (cấu hình IUserIdProvider nếu cần)
            var message = new ChatMessage { UserId = userId, Content = content, Timestamp = DateTime.UtcNow };

            await _chatService.AddMessageToSegment(segmentId, message);

            // Broadcast đến group (thread)
            await Clients.Group($"thread-{threadId}").SendAsync("ReceiveMessage", userId, content);
        }

        // Join thread
        public async Task JoinThread(int threadId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"thread-{threadId}");
        }

        // Tạo protected segment (client gửi PIN)
        public async Task StartProtectedSegment(int threadId, string pin)
        {
            await _chatService.CreateSegment(threadId, true, pin);
            await Clients.Group($"thread-{threadId}").SendAsync("SegmentStarted", "Protected segment started");
        }
        public async Task StartNewSegment(int threadId)
        {
            await _chatService.CreateSegment(threadId, false); // Không protected
            await Clients.Group($"thread-{threadId}").SendAsync("SegmentStarted", "New segment started");
        }
        
        
    }
}