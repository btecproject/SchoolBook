using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Controllers
{
    // ==================== MVC VIEWS CONTROLLER ====================
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(ChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpGet]
        [Route("/Chat")]
        public IActionResult Index()
        {
            var userId = User.Identity?.Name;
            var threads = _chatService.GetThreadsForUser(userId)?.ToList() ?? new List<ChatThread>();
            return View("~/Views/Chat/Index.cshtml", threads);
        }
        
        [HttpGet]
        [Route("/Chat/Thread/{threadId:int}")]
        public IActionResult Thread(int threadId)
        {
            var userId = User.Identity?.Name;
            var thread = _chatService.GetThreadById(threadId, userId);
            
            if (thread == null)
            {
                _logger.LogWarning($"Thread {threadId} not found for user {userId}");
                return NotFound();
            }
            
            return View("~/Views/Chat/Thread.cshtml", thread);
        }
    }

    // ==================== API CONTROLLER ====================
    [Route("api/chat")]
    [Authorize]
    [ApiController]
    public class ChatApiController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly ILogger<ChatApiController> _logger;
        private readonly AppDbContext _context;

        public ChatApiController(
            ChatService chatService, 
            ILogger<ChatApiController> logger,
            AppDbContext context)
        {
            _chatService = chatService;
            _logger = logger;
            _context = context;
        }

        // ==================== GET MESSAGES ====================
        [HttpGet("messages")]
        public IActionResult GetMessages(int segmentId, string pin = null)
        {
            try
            {
                _logger.LogInformation($"📥 GetMessages API called");
                _logger.LogInformation($"   SegmentId: {segmentId}");
                _logger.LogInformation($"   PIN: {(pin != null ? "PROVIDED" : "NULL")}");
                _logger.LogInformation($"   User: {User.Identity?.Name}");

                // STEP 1: Validate segmentId
                if (segmentId <= 0)
                {
                    _logger.LogError($"❌ Invalid segmentId: {segmentId}");
                    return BadRequest(new { 
                        error = "Invalid segment ID", 
                        segmentId = segmentId,
                        message = "Segment ID must be greater than 0" 
                    });
                }

                // STEP 2: Kiểm tra segment tồn tại trong database
                _logger.LogInformation($"🔍 Checking if segment {segmentId} exists in database...");
                
                var segment = _context.ChatSegments
                    .Where(s => s.Id == segmentId)
                    .Select(s => new { 
                        s.Id, 
                        s.ThreadId, 
                        s.IsProtected, 
                        s.MessagesJson,
                        MessageLength = s.MessagesJson.Length
                    })
                    .FirstOrDefault();
                
                if (segment == null)
                {
                    _logger.LogError($"❌ Segment {segmentId} NOT FOUND in database");
                    return NotFound(new { 
                        error = "Segment not found", 
                        segmentId = segmentId,
                        message = $"Segment with ID {segmentId} does not exist"
                    });
                }
                
                _logger.LogInformation($"✅ Segment found: ID={segment.Id}, ThreadId={segment.ThreadId}, IsProtected={segment.IsProtected}");
                _logger.LogInformation($"   MessagesJson: {(segment.MessagesJson == null ? "NULL" : $"'{segment.MessagesJson}'")}");
                _logger.LogInformation($"   Length: {segment.MessageLength}");

                // STEP 3: Validate MessagesJson
                if (string.IsNullOrWhiteSpace(segment.MessagesJson))
                {
                    _logger.LogWarning($"⚠️ MessagesJson is NULL or empty! Fixing...");
                    
                    var segmentToFix = _context.ChatSegments.Find(segmentId);
                    if (segmentToFix != null)
                    {
                        segmentToFix.MessagesJson = "[]";
                        _context.SaveChanges();
                        _logger.LogInformation($"✅ Fixed MessagesJson to '[]'");
                    }
                }

                // STEP 4: Load messages through service
                _logger.LogInformation($"📥 Calling ChatService.GetMessagesFromSegment...");
                
                var messages = _chatService.GetMessagesFromSegment(segmentId, pin);

                if (messages == null)
                {
                    _logger.LogWarning($"⚠️ Service returned NULL messages");
                    messages = new List<ChatMessage>();
                }

                _logger.LogInformation($"✅ Successfully loaded {messages.Count} messages");

                return Ok(messages);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"⚠️ Argument error: {ex.Message}");
                return BadRequest(new { 
                    error = "Invalid argument", 
                    message = ex.Message,
                    type = "ArgumentException"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning($"⚠️ Unauthorized access: {ex.Message}");
                return Unauthorized(new { 
                    error = "Invalid PIN", 
                    message = ex.Message,
                    type = "UnauthorizedAccessException"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ FATAL ERROR in GetMessages");
                _logger.LogError($"   Type: {ex.GetType().Name}");
                _logger.LogError($"   Message: {ex.Message}");
                _logger.LogError($"   Stack: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    _logger.LogError($"   Inner Exception: {ex.InnerException.Message}");
                    _logger.LogError($"   Inner Stack: {ex.InnerException.StackTrace}");
                }
                
                return StatusCode(500, new { 
                    error = "Internal server error", 
                    message = ex.Message,
                    type = ex.GetType().Name,
                    innerException = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // ==================== SEARCH USERS ====================
        [HttpGet("search-users")]
        public IActionResult SearchUsers([FromQuery] string query)
        {
            try
            {
                _logger.LogInformation($"🔍 Searching users with query: '{query}'");
                
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Ok(new List<UserSearchResult>());
                }
                
                var currentUserId = User.Identity?.Name;
                var users = _chatService.SearchUsers(query, currentUserId);
                
                _logger.LogInformation($"✅ Found {users?.Count ?? 0} users");
                
                return Ok(users ?? new List<UserSearchResult>());
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Search users error: {ex.Message}");
                return BadRequest(new { 
                    error = "Failed to search users", 
                    message = ex.Message 
                });
            }
        }

        // ==================== CREATE THREAD ====================
        [HttpPost("create-thread")]
        public async Task<IActionResult> CreateThread([FromBody] CreateThreadRequest request)
        {
            try
            {
                var currentUserId = User.Identity?.Name;
                
                _logger.LogInformation($"📝 Creating thread...");
                _logger.LogInformation($"   Current user: {currentUserId}");
                _logger.LogInformation($"   Requested users: {(request.UserIds != null ? string.Join(", ", request.UserIds) : "NULL")}");
                _logger.LogInformation($"   Thread name: {request.ThreadName ?? "NULL"}");
                
                // STEP 1: Validate request
                if (string.IsNullOrEmpty(currentUserId))
                {
                    _logger.LogError($"❌ Current user is not authenticated");
                    return Unauthorized(new { 
                        success = false, 
                        message = "User not authenticated" 
                    });
                }
                
                if (request == null || request.UserIds == null || !request.UserIds.Any())
                {
                    _logger.LogError($"❌ No users selected");
                    return BadRequest(new { 
                        success = false, 
                        message = "At least one user must be selected" 
                    });
                }
                
                // STEP 2: Build user list
                var userIds = new List<string> { currentUserId };
                userIds.AddRange(request.UserIds);
                userIds = userIds.Distinct().ToList();

                _logger.LogInformation($"   Final user list: {string.Join(", ", userIds)}");

                // STEP 3: Check if thread already exists
                _logger.LogInformation($"🔍 Checking for existing thread...");
                
                var existingThread = _chatService.FindExistingThread(userIds);
                
                if (existingThread != null)
                {
                    _logger.LogInformation($"✅ Thread already exists: ID={existingThread.Id}");
                    return Ok(new 
                    { 
                        success = true, 
                        threadId = existingThread.Id, 
                        isNew = false,
                        message = "Thread already exists" 
                    });
                }

                // STEP 4: Create new thread
                _logger.LogInformation($"📝 Creating new thread...");
                
                var thread = new ChatThread
                {
                    ThreadName = request.ThreadName ?? $"Chat with {string.Join(", ", request.UserIds)}",
                    UserIds = userIds
                };

                var createdThread = await _chatService.CreateThread(thread);
                
                _logger.LogInformation($"✅ Thread created: ID={createdThread.Id}, Name={createdThread.ThreadName}");
                
                // STEP 5: Create initial segment
                _logger.LogInformation($"📝 Creating initial segment for thread {createdThread.Id}...");
                
                try
                {
                    var segment = await _chatService.CreateSegment(createdThread.Id, false);
                    
                    _logger.LogInformation($"✅ Initial segment created:");
                    _logger.LogInformation($"   Segment ID: {segment.Id}");
                    _logger.LogInformation($"   Thread ID: {segment.ThreadId}");
                    _logger.LogInformation($"   MessagesJson: {segment.MessagesJson}");
                    _logger.LogInformation($"   IsProtected: {segment.IsProtected}");
                    
                    // STEP 6: Verify segment was saved
                    if (segment.Id <= 0)
                    {
                        throw new Exception("Segment ID is invalid (0 or negative)");
                    }
                    
                    // Double-check segment exists in database
                    var segmentExists = _context.ChatSegments.Any(s => s.Id == segment.Id);
                    if (!segmentExists)
                    {
                        throw new Exception($"Segment {segment.Id} was not saved to database");
                    }
                    
                    _logger.LogInformation($"✅ Segment verified in database");
                }
                catch (Exception segmentEx)
                {
                    _logger.LogError($"❌ Failed to create initial segment");
                    _logger.LogError($"   Error: {segmentEx.Message}");
                    _logger.LogError($"   Stack: {segmentEx.StackTrace}");
                    
                    // Rollback thread if segment creation failed
                    _logger.LogWarning($"⚠️ Rolling back thread {createdThread.Id}...");
                    await _chatService.DeleteThread(createdThread.Id);
                    
                    return BadRequest(new { 
                        success = false, 
                        message = $"Failed to create chat segment: {segmentEx.Message}",
                        error = segmentEx.GetType().Name
                    });
                }

                _logger.LogInformation($"✅ Thread creation completed successfully");

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
                _logger.LogError($"❌ FATAL ERROR creating thread");
                _logger.LogError($"   Type: {ex.GetType().Name}");
                _logger.LogError($"   Message: {ex.Message}");
                _logger.LogError($"   Stack: {ex.StackTrace}");
                
                return BadRequest(new { 
                    success = false, 
                    message = ex.Message,
                    error = ex.GetType().Name
                });
            }
        }
    }

    // ==================== REQUEST MODEL ====================
    public class CreateThreadRequest
    {
        public string ThreadName { get; set; }
        public List<string> UserIds { get; set; }
    }
}