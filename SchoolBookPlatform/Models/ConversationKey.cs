namespace SchoolBookPlatform.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ConversationKeys")]
public class ConversationKey
{
    public Guid ChatUserId { get; set; }
    
    public Guid ConversationId { get; set; }
    
    public int KeyVersion { get; set; } = 1;

    [Required]
    public string EncryptedKey { get; set; } = string.Empty; //key AES(ko phai pincode) đã mã hóa bằng RSA Public Key của UserId

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow.AddHours(7);
    
    [ForeignKey("ConversationId")]
    public virtual Conversation? Conversation { get; set; }
    
    [ForeignKey(nameof(ChatUserId))]
    public virtual ChatUser ChatUser { get; set; } = null!;
}
