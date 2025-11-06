using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SchoolBookPlatform.Models
{
    public class ChatThread
    {
        public int Id { get; set; }
        public string ThreadName { get; set; }
        
        // Lưu dưới dạng JSON string trong database
        public string UserIdsJson { get; set; }
        
        // Property không map vào database
        [NotMapped]
        public List<string> UserIds 
        { 
            get => string.IsNullOrEmpty(UserIdsJson) 
                ? new List<string>() 
                : JsonSerializer.Deserialize<List<string>>(UserIdsJson) ?? new List<string>();
            set => UserIdsJson = JsonSerializer.Serialize(value ?? new List<string>());
        }
        
        public List<ChatSegment> Segments { get; set; } = new List<ChatSegment>();
        
        [NotMapped]
        public string LastMessageContent
        {
            get
            {
                var latestSegment = Segments?
                    .OrderByDescending(s => s.StartTime)
                    .FirstOrDefault();
                    
                if (latestSegment == null || string.IsNullOrEmpty(latestSegment.MessagesJson)) 
                    return "No messages yet";
                
                try
                {
                    var messages = JsonSerializer.Deserialize<List<ChatMessage>>(latestSegment.MessagesJson);
                    return messages?
                        .OrderByDescending(m => m.Timestamp)
                        .FirstOrDefault()?
                        .Content ?? "No messages yet";
                }
                catch
                {
                    return "No messages yet";
                }
            }
        }
    }
}