namespace SchoolBookPlatform.Models;

public class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public ICollection<UserRole>? UserRoles { get; set; }
}