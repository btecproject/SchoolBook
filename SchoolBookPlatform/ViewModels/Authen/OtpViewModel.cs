using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.Authen;

public class OtpViewModel
{
    [Required(ErrorMessage = "Enter your OTP")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP has 6 digits")]
    public string Code { get; set; }

    public string Type { get; set; } //sms;email
}