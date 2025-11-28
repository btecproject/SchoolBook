using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.Chat;

public class ChatRegistrationViewModel
{
    //Username readonly, hiển thị trong form
    public string Username { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên hiển thị phải từ 2-100 ký tự")]
    [Display(Name = "Tên hiển thị")]
    public string DisplayName { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Vui lòng nhập mã PIN hash")]
    [Display(Name = "PIN Code Hash")]
    public string PinCodeHash { get; set; } = string.Empty;
    
    // Không gửi lên server, chỉ để validation ở browser
    public string? PinCode { get; set; }

    [Compare("PinCode", ErrorMessage = "Xác nhận PIN không khớp")]
    public string? PinCodeConfirm { get; set; }
}