using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.Models;

public class Role
{
    public Guid Id { get; set; }
    [RegularExpression(@"^[a-zA-Z0-9\s\p{L}]+$", 
        ErrorMessage = "Tên được chứa chữ cái, số và khoảng trắng")]
    public string Name { get; set; }
    public string Description { get; set; }
    public ICollection<UserRole>? UserRoles { get; set; }
}