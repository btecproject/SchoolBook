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

        public ChatController(ChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpGet("messages")]
        public IActionResult GetMessages(int segmentId, string pin)
        {
            try
            {
                var messages = _chatService.GetMessagesFromSegment(segmentId, pin);
                return Ok(messages);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Invalid PIN");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
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
                return BadRequest(ex.Message);
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