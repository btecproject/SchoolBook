using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services
{
    public class ChatService
    {
        private readonly AppDbContext _context;
        private readonly EncryptionService _encryptionService; 

        public ChatService(AppDbContext context, EncryptionService encryptionService)
        {
            _context = context;
            _encryptionService = encryptionService;
        }
        
        public async Task<ChatSegment> CreateSegment(int threadId, bool isProtected, string pin = null)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            string pinHash = null;
            if (isProtected)
            {
                if (string.IsNullOrEmpty(pin)) throw new ArgumentException("PIN required for protected segment");
                pinHash = ComputePinHash(pin, salt);
            }

            var segment = new ChatSegment
            {
                ThreadId = threadId,
                StartTime = DateTime.UtcNow,
                IsProtected = isProtected,
                PinHash = pinHash,
                Salt = salt,
                MessagesJson = JsonSerializer.Serialize(new List<ChatMessage>())
            };

            _context.ChatSegments.Add(segment);
            await _context.SaveChangesAsync();
            return segment;
        }

        public async Task AddMessageToSegment(int segmentId, ChatMessage message)
        {
            var segment = await _context.ChatSegments.FindAsync(segmentId);
            if (segment == null) throw new Exception("Segment not found");

            // Không cần decrypt nếu không protected
            var messages = segment.IsProtected 
                ? DeserializeMessages(segment.MessagesJson, true, null) 
                : DeserializeMessages(segment.MessagesJson, false, null);
                
            messages.Add(message);

            // Không cần encrypt nếu không protected
            segment.MessagesJson = segment.IsProtected
                ? SerializeMessages(messages, true, null)
                : SerializeMessages(messages, false, null);
                
            await _context.SaveChangesAsync();
        }

        public List<ChatMessage> GetMessagesFromSegment(int segmentId, string pin)
        {
            var segment = _context.ChatSegments.Find(segmentId);
            if (segment == null) 
                throw new Exception("Segment not found");

            if (segment.IsProtected)
            {
                if (string.IsNullOrEmpty(pin))
                {
                    throw new UnauthorizedAccessException("PIN required for protected segment");
                }
            
                if (!VerifyPin(pin, segment.PinHash, segment.Salt)) 
                {
                    throw new UnauthorizedAccessException("Invalid PIN");
                }
        
                // Decrypt messages cho protected segment
                try
                {
                    var key = DeriveKeyFromPin(pin, segment.Salt);
                    var decryptedJson = _encryptionService.Decrypt(segment.MessagesJson, key);
                    return JsonSerializer.Deserialize<List<ChatMessage>>(decryptedJson) ?? new List<ChatMessage>();
                }
                catch (Exception ex)
                {
                    throw new UnauthorizedAccessException("Failed to decrypt messages. Invalid PIN or corrupted data.");
                }
            }

            // Segment không protected
            try
            {
                return JsonSerializer.Deserialize<List<ChatMessage>>(segment.MessagesJson) ?? new List<ChatMessage>();
            }
            catch (Exception ex)
            {
                // Nếu JSON bị lỗi, trả về empty list thay vì crash
                return new List<ChatMessage>();
            }
        }

        private string SerializeMessages(List<ChatMessage> messages, bool isProtected, string pin)
        {
            var json = JsonSerializer.Serialize(messages);
            
            if (!isProtected || string.IsNullOrEmpty(pin)) 
                return json;

            var key = DeriveKeyFromPin(pin, RandomNumberGenerator.GetBytes(16));
            return _encryptionService.Encrypt(json, key);
        }

        private List<ChatMessage> DeserializeMessages(string json, bool isProtected, string pin)
        {
            if (string.IsNullOrEmpty(json)) 
                return new List<ChatMessage>();
                
            if (!isProtected || string.IsNullOrEmpty(pin))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
                }
                catch
                {
                    return new List<ChatMessage>();
                }
            }
            
            var key = DeriveKeyFromPin(pin, RandomNumberGenerator.GetBytes(16));
            json = _encryptionService.Decrypt(json, key);
            return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
        }

        private byte[] DeriveKeyFromPin(string pin, byte[] salt)
        {
            if (salt == null || salt.Length == 0)
                salt = RandomNumberGenerator.GetBytes(16);
                
            using var pbkdf2 = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(pin), 
                salt, 
                100000, 
                HashAlgorithmName.SHA256
            );
            return pbkdf2.GetBytes(32);
        }

        private string ComputePinHash(string pin, byte[] salt)
        {
            if (salt == null || salt.Length == 0)
                throw new ArgumentException("Salt cannot be null or empty");
                
            var key = DeriveKeyFromPin(pin, salt);
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(key));
        }

        private bool VerifyPin(string pin, string storedHash, byte[] salt)
        {
            if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(storedHash) || salt == null)
                return false;
                
            var computedHash = ComputePinHash(pin, salt);
            return computedHash == storedHash;
        }
        
        public ChatThread GetThreadById(int threadId, string userId)
        {
            // Load all threads into memory first, then filter
            var threads = _context.ChatThreads
                .Include(t => t.Segments)
                .ToList(); // Load to memory
            
            // Filter in memory using UserIds property
            var thread = threads.FirstOrDefault(t => 
                t.Id == threadId && 
                t.UserIds.Contains(userId)
            );
            
            // Tạo segment mặc định nếu chưa có
            if (thread != null && !thread.Segments.Any())
            {
                var segment = new ChatSegment
                {
                    ThreadId = thread.Id,
                    StartTime = DateTime.UtcNow,
                    IsProtected = false,
                    MessagesJson = "[]"
                };
                _context.ChatSegments.Add(segment);
                _context.SaveChanges();
                
                thread.Segments.Add(segment);
            }
            
            return thread;
        }
        
        public IEnumerable<ChatThread> GetThreadsForUser(string userId)
        {
            if (string.IsNullOrEmpty(userId)) 
                return new List<ChatThread>();
            
            // Load all threads into memory first
            var allThreads = _context.ChatThreads
                .Include(t => t.Segments)
                .ToList();
            
            // Filter in memory using UserIds property
            var userThreads = allThreads
                .Where(t => t.UserIds.Contains(userId))
                .OrderByDescending(t => t.Segments.Any() 
                    ? t.Segments.Max(s => s.StartTime) 
                    : DateTime.MinValue)
                .ToList();
            
            return userThreads;
        }
        
        public List<ChatThread> GetAllThreads()
        {
            return _context.ChatThreads
                .Include(t => t.Segments)
                .OrderByDescending(t => t.Segments.Any() 
                    ? t.Segments.Max(s => s.StartTime) 
                    : DateTime.MinValue)
                .ToList();
        }
        
        // Tạo thread mới
        public async Task<ChatThread> CreateThread(ChatThread thread)
        {
            _context.ChatThreads.Add(thread);
            await _context.SaveChangesAsync();
            return thread;
        }
        
        // Xóa tất cả chats (for testing)
        public async Task ClearAllChats()
        {
            _context.ChatSegments.RemoveRange(_context.ChatSegments);
            _context.ChatThreads.RemoveRange(_context.ChatThreads);
            await _context.SaveChangesAsync();
        }
        
        // Tìm kiếm users
        public List<UserSearchResult> SearchUsers(string query, string currentUserId)
        {
            if (string.IsNullOrEmpty(query)) 
                return new List<UserSearchResult>();
            
            query = query.ToLower();
            
            return _context.Users
                .Where(u => u.Username.ToLower().Contains(query) && u.Id.ToString() != currentUserId)
                .Take(10)
                .Select(u => new UserSearchResult
                {
                    Id = u.Id.ToString(),
                    Username = u.Username,
                    Email = u.Email
                })
                .ToList();
        }
        
        // Kiểm tra thread đã tồn tại giữa 2 users
        public ChatThread FindExistingThread(List<string> userIds)
        {
            var allThreads = _context.ChatThreads
                .Include(t => t.Segments)
                .ToList();
            
            // Tìm thread có cùng members
            return allThreads.FirstOrDefault(t => 
                t.UserIds.Count == userIds.Count && 
                t.UserIds.All(id => userIds.Contains(id))
            );
        }
    }
    
    public class UserSearchResult
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
    }
}