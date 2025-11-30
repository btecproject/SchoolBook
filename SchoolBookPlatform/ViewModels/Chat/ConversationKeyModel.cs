using System.ComponentModel.DataAnnotations;
namespace SchoolBookPlatform.ViewModels.Chat
{
    // Dùng cho API SaveConversationKey
    public class ConversationKeyModel
    {
        [Required]
        public Guid ConversationId { get; set; }

        [Required]
        public string EncryptedKey { get; set; } = string.Empty;

        public int KeyVersion { get; set; } = 1;
    }
}