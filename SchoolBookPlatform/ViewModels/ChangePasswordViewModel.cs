using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels;

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Please enter your new Password")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; }
    
    [Required(ErrorMessage = "Please re-enter your new Password")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; }
}