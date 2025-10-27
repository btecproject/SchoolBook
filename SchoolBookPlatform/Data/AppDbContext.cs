using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Data;

public class AppDbContext:DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options):base(options){}
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<UserToken> UserTokens { get; set; }
    public DbSet<OtpCode> OtpCodes { get; set; }
    public DbSet<FaceProfile> FaceProfiles { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 🔹 Khóa chính kép cho UserRole
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        // 🔹 Thiết lập mối quan hệ N-N
        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        // Các entity khác
        modelBuilder.Entity<UserToken>().HasKey(t => t.Id);
        modelBuilder.Entity<OtpCode>().HasKey(o => o.Id);
        modelBuilder.Entity<FaceProfile>().HasKey(f => f.UserId);
    }
}