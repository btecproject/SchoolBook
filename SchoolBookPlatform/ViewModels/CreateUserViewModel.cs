using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels;

public class CreateUserViewModel
{
    [Required(ErrorMessage = "Username là bắt buộc")]
    [MaxLength(50, ErrorMessage = "Username tối đa 50 ký tự")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [MinLength(8, ErrorMessage = "Mật khẩu tối thiểu 8 ký tự")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$", 
        ErrorMessage = "Mật khẩu phải chứa ít nhất một chữ cái viết hoa, một chữ cái viết thường, một số và một ký tự đặc biệt.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = null!;

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
    public bool MustChangePassword { get; set; } = true;

    [Display(Name = "Tài khoản hoạt động")]
    public bool IsActive { get; set; } = true;

    // Helper để hiển thị danh sách roles
    public List<RoleOption> AvailableRoles { get; set; } = new();
}

public class RoleOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
}

