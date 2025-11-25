using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.ForgotPassword;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
}