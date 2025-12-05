namespace SchoolBookPlatform.DTOs;

public class InitializeConversationKeyRequest
{
    public Guid ConversationId { get; set; }
        
    // Danh sách key cho từng thành viên trong cuộc hội thoại
    public List<UserConversationKeyDto> Keys { get; set; } = new();
}