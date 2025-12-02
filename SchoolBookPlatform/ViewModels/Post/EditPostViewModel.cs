using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SchoolBookPlatform.ViewModels.Post;

/// <summary>
/// ViewModel cho form sửa bài đăng
/// </summary>
public class EditPostViewModel
{
    /// <summary>
    /// ID của bài đăng cần sửa
    /// </summary>
    [Required]
    public Guid Id { get; set; }

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
    /// Danh sách file đính kèm hiện có của bài đăng
    /// </summary>
    public List<AttachmentViewModel> ExistingAttachments { get; set; } = new();

    /// <summary>
    /// Danh sách file mới được upload (không bắt buộc)
    /// </summary>
    [Display(Name = "Thêm ảnh/video")]
    public List<IFormFile>? Files { get; set; }

    /// <summary>
    /// Danh sách ID của các file cần xóa
    /// </summary>
    public List<Guid>? AttachmentIdsToDelete { get; set; }
}

/// <summary>
/// ViewModel cho thông tin file đính kèm
/// </summary>
public class AttachmentViewModel
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    
    /// <summary>
    /// Kiểm tra xem file có phải là ảnh không (dựa vào extension)
    /// </summary>
    public bool IsImage => FileName.ToLower().EndsWith(".jpg") || 
                          FileName.ToLower().EndsWith(".jpeg") || 
                          FileName.ToLower().EndsWith(".png") || 
                          FileName.ToLower().EndsWith(".gif") || 
                          FileName.ToLower().EndsWith(".webp");
    
    public bool IsVideo => IsVideoFile(FileName);
    
    private bool IsVideoFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
            
        var extension = Path.GetExtension(fileName).ToLower();
        var videoExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm" };
        return videoExtensions.Contains(extension);
    }
}

