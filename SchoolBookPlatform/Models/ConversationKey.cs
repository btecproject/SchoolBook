namespace SchoolBookPlatform.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ConversationKeys")]
public class ConversationKey
{
    public Guid UserId { get; set; }
    
    public Guid ConversationId { get; set; }
    
    public int KeyVersion { get; set; } = 1;

    [Required]
    public string EncryptedKey { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    
    [ForeignKey("UserId")]
    public virtual User? User { get; set; }

    [ForeignKey("ConversationId")]
    public virtual Conversation? Conversation { get; set; }
}
