using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Controllers
{
    [Route("api/chat")]
    [Authorize]
    [ApiController]
    public class EncryptionApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<EncryptionApiController> _logger;

        public EncryptionApiController(AppDbContext context, ILogger<EncryptionApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===================================================
        // UPLOAD PUBLIC KEY
        // ===================================================
        [HttpPost("upload-public-key")]
        public async Task<IActionResult> UploadPublicKey([FromBody] UploadPublicKeyRequest request)
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                _logger.LogInformation($"Uploading public key for user {username}");

                // FIXED: Get user with correct type
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username);
                    
                if (user == null)
                {
                    _logger.LogWarning($"User not found: {username}");
                    return NotFound(new { error = "User not found" });
                }

                var userId = user.Id;
                
                _logger.LogInformation($"User ID: {userId}, Type: {userId.GetType().Name}");

                // Check if key already exists
                var existingKey = await _context.Set<UserEncryptionKey>()
                    .FirstOrDefaultAsync(k => k.UserId == userId);

                if (existingKey != null)
                {
                    // Update existing key
                    existingKey.PublicKey = request.PublicKey;
                    existingKey.LastUsedAt = DateTime.UtcNow;
                    
                    _logger.LogInformation($" Updated existing public key for user {userId}");
                }
                else
                {
                    // Create new key record
                    var encryptionKey = new UserEncryptionKey
                    {
                        UserId = userId,
                        PublicKey = request.PublicKey,
                        EncryptedPrivateKey = "", // Client keeps private key
                        PrivateKeySalt = new byte[0],
                        CreatedAt = DateTime.UtcNow,
                        LastUsedAt = DateTime.UtcNow
                    };

                    _context.Set<UserEncryptionKey>().Add(encryptionKey);
                    
                    _logger.LogInformation($" Created new public key for user {userId}");
                }

                await _context.SaveChangesAsync();

                return Ok(new { 
                    success = true, 
                    message = "Public key uploaded successfully" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error uploading public key: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                
                return StatusCode(500, new { 
                    error = "Failed to upload public key", 
                    message = ex.Message 
                });
            }
        }

        // ===================================================
        // GET PUBLIC KEYS FOR THREAD MEMBERS
        // ===================================================
        [HttpGet("thread/{threadId}/public-keys")]
        public async Task<IActionResult> GetThreadPublicKeys(int threadId)
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                _logger.LogInformation($" Getting public keys for thread {threadId}");

                // Get thread
                var thread = await _context.ChatThreads
                    .FirstOrDefaultAsync(t => t.Id == threadId);
                    
                if (thread == null)
                {
                    return NotFound(new { error = "Thread not found" });
                }

                // Get user IDs from thread (UserIds is List<string>)
                var userIds = thread.UserIds;
                
                _logger.LogInformation($"Thread has {userIds.Count} users: {string.Join(", ", userIds)}");

                // FIXED: Get all users in thread first
                var usersInThread = await _context.Users
                    .Where(u => userIds.Contains(u.Username))
                    .Select(u => u.Id)
                    .ToListAsync();
                
                _logger.LogInformation($"Found {usersInThread.Count} user IDs in database");

                // Get public keys for these users
                var publicKeys = await _context.Set<UserEncryptionKey>()
                    .Where(k => usersInThread.Contains(k.UserId))
                    .ToListAsync();

                var result = publicKeys.Select(k => new 
                {
                    userId = k.UserId.ToString(),
                    publicKey = k.PublicKey,
                    lastUsed = k.LastUsedAt
                }).ToList();

                _logger.LogInformation($" Retrieved {result.Count} public keys for thread {threadId}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error getting public keys: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                
                return StatusCode(500, new { 
                    error = "Failed to get public keys", 
                    message = ex.Message 
                });
            }
        }

        // ===================================================
        // GET MY PUBLIC KEY
        // ===================================================
        [HttpGet("my-public-key")]
        public async Task<IActionResult> GetMyPublicKey()
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username);
                    
                if (user == null)
                {
                    return NotFound(new { error = "User not found" });
                }

                var encryptionKey = await _context.Set<UserEncryptionKey>()
                    .FirstOrDefaultAsync(k => k.UserId == user.Id);

                if (encryptionKey == null)
                {
                    return NotFound(new { 
                        error = "No public key found for this user",
                        hasKey = false
                    });
                }

                return Ok(new 
                { 
                    publicKey = encryptionKey.PublicKey,
                    createdAt = encryptionKey.CreatedAt,
                    lastUsedAt = encryptionKey.LastUsedAt,
                    hasKey = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error getting public key: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                
                return StatusCode(500, new { 
                    error = "Failed to get public key", 
                    message = ex.Message 
                });
            }
        }

        // ===================================================
        // DELETE MY ENCRYPTION KEYS (For reset/troubleshooting)
        // ===================================================
        [HttpDelete("my-encryption-keys")]
        public async Task<IActionResult> DeleteMyKeys()
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized(new { error = "User not authenticated" });
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username);
                    
                if (user == null)
                {
                    return NotFound(new { error = "User not found" });
                }

                var encryptionKey = await _context.Set<UserEncryptionKey>()
                    .FirstOrDefaultAsync(k => k.UserId == user.Id);

                if (encryptionKey != null)
                {
                    _context.Set<UserEncryptionKey>().Remove(encryptionKey);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation($" Deleted encryption keys for user {user.Id}");
                }

                return Ok(new { 
                    success = true, 
                    message = "Encryption keys deleted successfully" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($" Error deleting keys: {ex.Message}");
                return StatusCode(500, new { 
                    error = "Failed to delete keys", 
                    message = ex.Message 
                });
            }
        }
    }

    // ===================================================
    // REQUEST MODELS
    // ===================================================
    public class UploadPublicKeyRequest
    {
        public string PublicKey { get; set; }
    }
}