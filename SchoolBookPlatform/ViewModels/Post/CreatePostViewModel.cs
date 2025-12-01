using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SchoolBookPlatform.ViewModels.Post;

/// <summary>
/// ViewModel cho form tạo bài đăng mới
/// </summary>
public class CreatePostViewModel
{
    /// <summary>
    /// Tiêu đề bài đăng (bắt buộc, tối đa 300 ký tự)
    /// </summary>
    [Required(ErrorMessage = "Tiêu đề không được để trống")]
    [MaxLength(300, ErrorMessage = "Tiêu đề không được quá 300 ký tự")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung bài đăng (bắt buộc)
    /// </summary>
    [Required(ErrorMessage = "Nội dung không được để trống")]
    [Display(Name = "Nội dung")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Nhóm người dùng được phép xem bài đăng
    /// Giá trị: 'Student', 'Teacher', 'Admin', 'All' (mặc định: 'All')
    /// </summary>
    [Display(Name = "Hiển thị cho")]
    public string VisibleToRoles { get; set; } = "All";

    /// <summary>
    /// Danh sách file đính kèm (ảnh/video) - không bắt buộc
    /// </summary>
    [Display(Name = "Thêm ảnh/video")]
    public List<IFormFile>? Files { get; set; }
}