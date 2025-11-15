using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public DbSet<ChatThread> ChatThreads { get; set; }
    public DbSet<ChatSegment> ChatSegments { get; set; }
    public DbSet<ChatAttachment> ChatAttachments { get; set; }
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
            entity.Property(t => t.LoginAt).HasDefaultValueSql("GETUTCDATE()");
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

        // ChatThread - Cấu hình UserIdsJson
        modelBuilder.Entity<ChatThread>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.ThreadName).IsRequired().HasMaxLength(200);
            entity.Property(t => t.UserIdsJson).IsRequired().HasColumnName("UserIds");
            
            entity.HasMany(t => t.Segments)
                .WithOne()
                .HasForeignKey(s => s.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatSegment
        modelBuilder.Entity<ChatSegment>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.MessagesJson).IsRequired();
            entity.Property(s => s.StartTime).IsRequired();
        });
        
        //ChatAttachment
        modelBuilder.Entity<ChatAttachment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FileName).IsRequired().HasMaxLength(255);
            entity.Property(a => a.FileType).IsRequired().HasMaxLength(50);
            entity.Property(a => a.MimeType).IsRequired().HasMaxLength(100);
            entity.Property(a => a.FileData).IsRequired();
            entity.Property(a => a.UploadedAt).HasDefaultValueSql("GETUTCDATE()");
            
            entity.HasIndex(a => a.SegmentId);
        });
    }
}