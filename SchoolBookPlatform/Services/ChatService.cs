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
            Console.WriteLine($" CreateSegment START");
            Console.WriteLine($"   ThreadId: {threadId}");
            Console.WriteLine($"   IsProtected: {isProtected}");
            Console.WriteLine($"   PIN provided: {(pin != null ? "YES" : "NO")}");
            
            byte[]? salt = null;
            string? pinHash = null;
            
            if (isProtected)
            {
                if (string.IsNullOrEmpty(pin)) 
                    throw new ArgumentException("PIN required for protected segment");
                
                // Validate PIN format
                if (!System.Text.RegularExpressions.Regex.IsMatch(pin, @"^\d{4,6}$"))
                    throw new ArgumentException("PIN must be 4-6 digits");
                
                // Generate salt
                salt = RandomNumberGenerator.GetBytes(16);
                Console.WriteLine($"   Generated Salt: {salt.Length} bytes");
                
                // Compute hash
                pinHash = ComputePinHash(pin, salt);
                Console.WriteLine($"   Computed PinHash: {pinHash.Substring(0, 20)}...");
                
                // Verify immediately
                var verifyResult = VerifyPin(pin, pinHash, salt);
                Console.WriteLine($"   Immediate Verify: {(verifyResult ? " SUCCESS" : " FAILED")}");
                
                if (!verifyResult)
                {
                    throw new Exception("CRITICAL: PIN verification failed immediately after creation!");
                }
            }

            var segment = new ChatSegment
            {
                ThreadId = threadId,
                StartTime = DateTime.UtcNow,
                IsProtected = isProtected,
                PinHash = pinHash,
                Salt = salt,
                MessagesJson = "[]"
            };

            Console.WriteLine($" Segment object created");

            _context.ChatSegments.Add(segment);
            
            Console.WriteLine($" Calling SaveChangesAsync...");
            
            try
            {
                await _context.SaveChangesAsync();
                Console.WriteLine($" SaveChangesAsync completed - Segment ID: {segment.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" SaveChangesAsync FAILED!");
                Console.WriteLine($"   Error: {ex.Message}");
                throw;
            }
            
            // Reload from database to verify
            _context.Entry(segment).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            
            var reloadedSegment = await _context.ChatSegments
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == segment.Id);
                
            if (reloadedSegment == null)
            {
                throw new Exception($"CRITICAL: Segment {segment.Id} not found after save!");
            }
            
            Console.WriteLine($" Reloaded from DB:");
            Console.WriteLine($"   ID: {reloadedSegment.Id}");
            Console.WriteLine($"   IsProtected: {reloadedSegment.IsProtected}");
            Console.WriteLine($"   MessagesJson: '{reloadedSegment.MessagesJson}'");
            
            if (reloadedSegment.IsProtected)
            {
                Console.WriteLine($"   PinHash: {(reloadedSegment.PinHash != null ? "SET" : "NULL")}");
                Console.WriteLine($"   Salt: {(reloadedSegment.Salt != null ? reloadedSegment.Salt.Length + " bytes" : "NULL")}");
                
                // Verify PIN works with reloaded data
                if (pin != null)
                {
                    var verifyReloaded = VerifyPin(pin, reloadedSegment.PinHash, reloadedSegment.Salt);
                    Console.WriteLine($"   Verify with reloaded data: {(verifyReloaded ? " SUCCESS" : " FAILED")}");
                    
                    if (!verifyReloaded)
                    {
                        throw new Exception("CRITICAL: PIN verification failed with reloaded data!");
                    }
                }
            }
            
            // Fix MessagesJson if NULL
            if (string.IsNullOrWhiteSpace(reloadedSegment.MessagesJson))
            {
                Console.WriteLine($" MessagesJson is NULL/empty, fixing...");
                
                var sql = "UPDATE ChatSegments SET MessagesJson = '[]' WHERE Id = {0}";
                await _context.Database.ExecuteSqlRawAsync(sql, segment.Id);
                
                reloadedSegment = await _context.ChatSegments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == segment.Id);
            }
            
            Console.WriteLine($" CreateSegment COMPLETED");
            
            return reloadedSegment;
        }

        public List<ChatMessage> GetMessagesFromSegment(int segmentId, string pin)
        {
            try
            {
                Console.WriteLine($" GetMessagesFromSegment - SegmentId: {segmentId}");
                
                if (segmentId <= 0)
                {
                    Console.WriteLine($" Invalid segmentId: {segmentId}");
                    throw new ArgumentException("Invalid segment ID");
                }
                
                // Clear cache
                _context.ChangeTracker.Clear();
                
                var segment = _context.ChatSegments
                    .Where(s => s.Id == segmentId)
                    .AsNoTracking()
                    .FirstOrDefault();
                    
                if (segment == null)
                {
                    Console.WriteLine($" Segment {segmentId} not found in database");
                    throw new Exception($"Segment {segmentId} not found");
                }

                Console.WriteLine($"🔍 Segment found: IsProtected={segment.IsProtected}, MessagesJson='{segment.MessagesJson ?? "NULL"}', Length={segment.MessagesJson?.Length ?? 0}");

                // Check PIN nếu protected
                if (segment.IsProtected)
                {
                    if (string.IsNullOrEmpty(pin))
                    {
                        Console.WriteLine(" PIN required but not provided");
                        throw new UnauthorizedAccessException("PIN required for protected segment");
                    }
                        
                    if (!VerifyPin(pin, segment.PinHash, segment.Salt))
                    {
                        Console.WriteLine(" Invalid PIN");
                        throw new UnauthorizedAccessException("Invalid PIN");
                    }
                    
                    Console.WriteLine(" PIN verified");
                }

                var messagesJson = segment.MessagesJson;

                // CRITICAL: Fix nếu NULL hoặc invalid
                if (string.IsNullOrWhiteSpace(messagesJson) || messagesJson == "null")
                {
                    Console.WriteLine($" MessagesJson is invalid: '{messagesJson}' - Fixing in database...");
                    
                    // Update trong database
                    var segmentToUpdate = _context.ChatSegments.Find(segmentId);
                    if (segmentToUpdate != null)
                    {
                        segmentToUpdate.MessagesJson = "[]";
                        _context.SaveChanges();
                        Console.WriteLine($" Fixed MessagesJson in database");
                    }
                    
                    messagesJson = "[]";
                }

                // Deserialize
                try
                {
                    Console.WriteLine($" Deserializing: '{messagesJson}'");
                    
                    var messages = JsonSerializer.Deserialize<List<ChatMessage>>(messagesJson) 
                                   ?? new List<ChatMessage>();
                    
                    Console.WriteLine($" Deserialized {messages.Count} messages successfully");
                    return messages;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" JSON Deserialize error: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"Problematic JSON: '{messagesJson}'");
                    
                    // Return empty list thay vì throw
                    return new List<ChatMessage>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" GetMessagesFromSegment fatal error: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                throw; // Re-throw để controller xử lý
            }
        }

        public async Task AddMessageToSegment(int segmentId, ChatMessage message)
        {
            try
            {
                Console.WriteLine($" AddMessageToSegment - SegmentId: {segmentId}");
                
                // CRITICAL: Detach tất cả entities để tránh cache
                _context.ChangeTracker.Clear();
                
                // Query lại segment
                var segment = await _context.ChatSegments
                    .Where(s => s.Id == segmentId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
                    
                if (segment == null)
                {
                    Console.WriteLine($" Segment {segmentId} not found");
                    throw new Exception($"Segment {segmentId} not found");
                }

                var messagesJson = segment.MessagesJson ?? "[]";
                Console.WriteLine($" Current MessagesJson: '{messagesJson}'");
                
                // Fix nếu invalid
                if (string.IsNullOrWhiteSpace(messagesJson) || messagesJson == "null")
                {
                    Console.WriteLine($" Fixing invalid MessagesJson...");
                    messagesJson = "[]";
                    
                    var segmentToFix = await _context.ChatSegments.FindAsync(segmentId);
                    if (segmentToFix != null)
                    {
                        segmentToFix.MessagesJson = "[]";
                        await _context.SaveChangesAsync();
                        _context.ChangeTracker.Clear();
                    }
                }

                // Deserialize
                List<ChatMessage> messages;
                
                try
                {
                    messages = JsonSerializer.Deserialize<List<ChatMessage>>(messagesJson) 
                               ?? new List<ChatMessage>();
                    Console.WriteLine($" Current messages count: {messages.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" Deserialize error: {ex.Message}");
                    messages = new List<ChatMessage>();
                }
                
                // Add message
                messages.Add(message);
                Console.WriteLine($" Added message, new count: {messages.Count}");
                
                // Serialize
                var newJson = JsonSerializer.Serialize(messages);
                Console.WriteLine($" Serialized to: {newJson.Length} chars");
                
                // Update
                var segmentForSave = await _context.ChatSegments.FindAsync(segmentId);
                if (segmentForSave != null)
                {
                    segmentForSave.MessagesJson = newJson;
                    await _context.SaveChangesAsync();
                    Console.WriteLine($" Saved to database");
                }
                else
                {
                    Console.WriteLine($" Could not find segment {segmentId} for saving");
                    throw new Exception($"Segment {segmentId} not found for update");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" AddMessageToSegment error: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                throw;
            }
        }

        private byte[] DeriveKeyFromPin(string pin, byte[] salt)
        {
            // CRITICAL: KHÔNG tạo salt mới nếu salt NULL
            // Phải dùng salt được truyền vào
            if (salt == null || salt.Length == 0)
                throw new ArgumentException("Salt is required for PIN derivation");
                
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
            try
            {
                if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(storedHash) || salt == null || salt.Length == 0)
                {
                    Console.WriteLine($" VerifyPin: Missing required data");
                    Console.WriteLine($"   PIN: {(string.IsNullOrEmpty(pin) ? "NULL/EMPTY" : "PROVIDED")}");
                    Console.WriteLine($"   StoredHash: {(string.IsNullOrEmpty(storedHash) ? "NULL/EMPTY" : "PROVIDED")}");
                    Console.WriteLine($"   Salt: {(salt == null ? "NULL" : salt.Length + " bytes")}");
                    return false;
                }
                
                Console.WriteLine($" VerifyPin: Computing hash with provided salt ({salt.Length} bytes)");
                
                var computedHash = ComputePinHash(pin, salt);
                
                Console.WriteLine($"   Stored Hash:   {storedHash}");
                Console.WriteLine($"   Computed Hash: {computedHash}");
                Console.WriteLine($"   Match: {computedHash == storedHash}");
                
                return computedHash == storedHash;
            }
            catch (Exception ex)
            {
                Console.WriteLine($" VerifyPin error: {ex.Message}");
                return false;
            }
        }
        
        public ChatThread GetThreadById(int threadId, string userId)
        {
            var threads = _context.ChatThreads
                .Include(t => t.Segments)
                .ToList();
            
            var thread = threads.FirstOrDefault(t => 
                t.Id == threadId && 
                t.UserIds.Contains(userId)
            );
            
            if (thread != null && !thread.Segments.Any())
            {
                Console.WriteLine($" Thread {threadId} has no segments, creating initial segment...");
                
                var segment = new ChatSegment
                {
                    ThreadId = thread.Id,
                    StartTime = DateTime.UtcNow,
                    IsProtected = false,
                    MessagesJson = "[]"
                };
                _context.ChatSegments.Add(segment);
                _context.SaveChanges();
                
                // Reload to get ID
                _context.Entry(segment).Reload();
                
                Console.WriteLine($" Created initial segment {segment.Id}");
                thread.Segments.Add(segment);
            }
            
            return thread;
        }
        
        public IEnumerable<ChatThread> GetThreadsForUser(string userId)
        {
            if (string.IsNullOrEmpty(userId)) 
                return new List<ChatThread>();
            
            var allThreads = _context.ChatThreads
                .Include(t => t.Segments)
                .ToList();
            
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
        
        public async Task<ChatThread> CreateThread(ChatThread thread)
        {
            _context.ChatThreads.Add(thread);
            await _context.SaveChangesAsync();
            return thread;
        }
        
        public async Task DeleteThread(int threadId)
        {
            var thread = await _context.ChatThreads.FindAsync(threadId);
            if (thread != null)
            {
                _context.ChatThreads.Remove(thread);
                await _context.SaveChangesAsync();
            }
        }
        
        public async Task ClearAllChats()
        {
            _context.ChatSegments.RemoveRange(_context.ChatSegments);
            _context.ChatThreads.RemoveRange(_context.ChatThreads);
            await _context.SaveChangesAsync();
        }
        
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
        
        public ChatThread FindExistingThread(List<string> userIds)
        {
            var allThreads = _context.ChatThreads
                .Include(t => t.Segments)
                .ToList();
            
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