using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.Setting;

public class ChangePinViewModel
{
    [Required]
    public string OldPinHash { get; set; } = string.Empty;

    [Required]
    public string NewPinHash { get; set; } = string.Empty;

    [Required]
    public string NewEncryptedPrivateKey { get; set; } = string.Empty;
}