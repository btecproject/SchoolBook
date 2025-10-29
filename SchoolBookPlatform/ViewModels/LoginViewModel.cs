using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        
        [Display(Name = "Send OTP to Email")]
        public bool RememberMe { get; set; } = true;
        
        public string OtpType { get; set; }
    }
}