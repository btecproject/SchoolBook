namespace SchoolBookPlatform.DTOs;

public class UserConversationKeyDto
{
    public Guid UserId { get; set; }
    public string EncryptedKey { get; set; } = string.Empty; //AES Key mã hóa bởi RSA của User này
}