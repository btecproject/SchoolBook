using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.Authen;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}