using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.TwoFactorA;

public class TwoFactorVerifyViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mã xác thực")]
    [StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$")]
    public string Code { get; set; } = string.Empty;
}