using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.Authen;

public class LoginViewModel
{
    [Required] 
    [RegularExpression(@"^[a-zA-Z0-9\s\p{L}]+$", 
        ErrorMessage = "UserName chỉ được chứa chữ cái, số và khoảng trắng")]
    public string Username { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [Display(Name = "Send OTP to Email")]
    public bool RememberMe { get; set; } = true;

    public string OtpType { get; set; }
}