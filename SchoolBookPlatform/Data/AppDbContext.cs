using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<UserToken> UserTokens { get; set; } = null!;
    public DbSet<OtpCode> OtpCodes { get; set; } = null!;
    public DbSet<FaceProfile> FaceProfiles { get; set; } = null!;
    public DbSet<TrustedDevice> TrustedDevices { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // UserRole
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        // TrustedDevice
        modelBuilder.Entity<TrustedDevice>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.IPAddress, e.DeviceInfo })
                .IsUnique();
        });

        // UserToken
        modelBuilder.Entity<UserToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.ExpiredAt).IsRequired();
            entity.HasIndex(t => t.UserId);
        });

        // OtpCode
        modelBuilder.Entity<OtpCode>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.ExpiresAt).IsRequired();
        });

        // FaceProfile
        modelBuilder.Entity<FaceProfile>(entity =>
        {
            entity.HasKey(f => f.UserId);
            entity.HasOne(f => f.User)
                .WithOne(u => u.FaceProfile)
                .HasForeignKey<FaceProfile>(f => f.UserId);
        });

        modelBuilder.Entity<UserToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.LoginAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(t => t.ExpiredAt).IsRequired();
            entity.HasIndex(t => t.UserId);
        });
    }
}