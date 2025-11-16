using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Controllers
{
    [Route("api/chat")]
    [Authorize]
    [ApiController]
    public class ChatController : Controller
    {
        private readonly ChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpGet("messages")]
        public IActionResult GetMessages(int segmentId, string pin = null)
        {
            try
            {
                _logger.LogInformation($"GetMessages called: segmentId={segmentId}, pin={pin ?? "null"}");
        
                var messages = _chatService.GetMessagesFromSegment(segmentId, pin);
        
                _logger.LogInformation($"Messages returned: {messages?.Count ?? 0}");
        
                // CRITICAL: Luôn trả về JSON array, không bao giờ trả về string
                return Ok(messages ?? new List<ChatMessage>());
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"Unauthorized access: {ex.Message}");
                // Trả về JSON object thay vì string
                return Unauthorized(new { error = "Invalid PIN", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetMessages: {ex.Message}\n{ex.StackTrace}");
                return BadRequest(new { error = "Failed to load messages", message = ex.Message });
            }
        }

        // Tìm kiếm users
        [HttpGet("search-users")]
        public IActionResult SearchUsers([FromQuery] string query)
        {
            try
            {
                var currentUserId = User.Identity?.Name;
                var users = _chatService.SearchUsers(query, currentUserId);
                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Tạo thread mới hoặc lấy thread đã có
        [HttpPost("create-thread")]
        public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequest request)
        {
            try
            {
                var currentUserId = User.Identity?.Name;
                
                // Thêm current user vào list
                var userIds = new List<string> { currentUserId };
                userIds.AddRange(request.UserIds);
                userIds = userIds.Distinct().ToList();

                // Kiểm tra thread đã tồn tại
                var existingThread = _chatService.FindExistingThread(userIds);
                if (existingThread != null)
                {
                    return Ok(new 
                    { 
                        success = true, 
                        threadId = existingThread.Id, 
                        isNew = false,
                        message = "Thread already exists" 
                    });
                }

                // Tạo thread mới
                var thread = new ChatThread
                {
                    ThreadName = request.ThreadName ?? $"Chat with {string.Join(", ", request.UserIds)}",
                    UserIds = userIds
                };

                var createdThread = await _chatService.CreateThread(thread);
                
                // Tạo segment đầu tiên
                await _chatService.CreateSegment(createdThread.Id, false);

                return Ok(new 
                { 
                    success = true, 
                    threadId = createdThread.Id,
                    isNew = true,
                    message = "Thread created successfully" 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // Render view Index
        [HttpGet]
        [Route("/Chat")]
        public IActionResult Index()
        {
            var userId = User.Identity?.Name;
            var threads = _chatService.GetThreadsForUser(userId)?.ToList() ?? new List<ChatThread>();
            return View("~/Views/Chat/Index.cshtml", threads);
        }
        
        [HttpGet("Thread/{threadId:int}")]
        public IActionResult Thread(int threadId)
        {
            var userId = User.Identity?.Name;
            var thread = _chatService.GetThreadById(threadId, userId);
            if (thread == null) return NotFound();
            return View("~/Views/Chat/Thread.cshtml", thread);
        }
    }

    public class CreateThreadRequest
    {
        public string ThreadName { get; set; }
        public List<string> UserIds { get; set; }
    }
}