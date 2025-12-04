namespace SchoolBookPlatform.DTOs;

public class ConversationResult
{
    public Guid ConversationId { get; set; }
    public bool IsNew { get; set; }
    public bool IsKeyInitialized { get; set; }
}