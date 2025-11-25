using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.TwoFactorA;

public class TwoFactorSetupViewModel
{
    public string QrCodeBase64 { get; set; } = string.Empty;
    public string ManualEntryKey { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Vui lòng nhập mã xác thực")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã phải có 6 chữ số")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã phải là 6 chữ số")]
    public string VerificationCode { get; set; } = string.Empty;
}