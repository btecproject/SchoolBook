using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.Chat;

public class RsaKeysUploadModel
{
    //Public key ở dạng PEM format
    [Required(ErrorMessage = "Public key is required")]
    public string PublicKey { get; set; } = string.Empty;

    //Private key đã được mã hóa AES bằng PIN của user (ở client)
    //Dạng: CryptoJS.AES.encrypt(privateKey, pin).toString()
    //Server lưu giữ nhưng không thể giải mã (không có PIN)
    [Required(ErrorMessage = "Encrypted private key is required")]
    public string PrivateKeyEncrypted { get; set; } = string.Empty;
}