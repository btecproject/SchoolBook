using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.Setting;

public class SettingChangePasswordViewModel
{
    [DataType(DataType.Password)]
    [Required(ErrorMessage = "Please enter your Password")]

    public string RecentPassword { get; set; }
    
    [Required(ErrorMessage = "Please enter your new Password")]
    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$", 
        ErrorMessage = "Mật khẩu phải chứa ít nhất một chữ cái viết hoa, một chữ cái viết thường, một số và một ký tự đặc biệt.")]
    public string NewPassword { get; set; }

    [Required(ErrorMessage = "Please re-enter your new Password")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmNewPassword { get; set; }
}