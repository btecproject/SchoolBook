using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels.Admin;

public class EditUserViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Username là bắt buộc")]
    [MaxLength(50, ErrorMessage = "Username tối đa 50 ký tự")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = null!;

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ít nhất một vai trò")]
    [Display(Name = "Vai trò")]
    public List<Guid> RoleIds { get; set; } = new();

    [Display(Name = "Bắt buộc đổi mật khẩu")]
    public bool MustChangePassword { get; set; }

    [Display(Name = "Tài khoản hoạt động")]
    public bool IsActive { get; set; }

    [Display(Name = "Đã đăng ký Face")]
    public bool FaceRegistered { get; set; }

    // Helper để hiển thị danh sách roles
    public List<RoleOption> AvailableRoles { get; set; } = new();

    // Thông tin hiện tại của user (để kiểm tra quyền)
    public List<string> CurrentRoles { get; set; } = new();
}

