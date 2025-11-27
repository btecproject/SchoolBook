using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.Post;

/// <summary>
/// ViewModel cho form Moderator xóa bài đăng
/// </summary>
public class ModeratorDeleteViewModel
{
    /// <summary>
    /// ID của bài đăng cần xóa
    /// </summary>
    public Guid PostId { get; set; }

    /// <summary>
    /// Lý do xóa bài đăng (bắt buộc, tối đa 500 ký tự)
    /// </summary>
    [Required(ErrorMessage = "Lý do xóa không được để trống")]
    [MaxLength(500, ErrorMessage = "Lý do không được quá 500 ký tự")]
    [Display(Name = "Lý do xóa")]
    public string Reason { get; set; } = string.Empty;
}




