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
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<Follower> Followers { get; set; }
    public DbSet<Following> Following { get; set; }
    public DbSet<RecoveryCode> RecoveryCodes { get; set; } = null!;
    
    // Post feature - DbSets
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<PostComment> PostComments { get; set; } = null!;
    public DbSet<PostVote> PostVotes { get; set; } = null!;
    public DbSet<PostReport> PostReports { get; set; } = null!;
    public DbSet<PostAttachment> PostAttachments { get; set; } = null!;

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
        
        modelBuilder.Entity<Follower>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Follower>()
            .HasOne(f => f.FollowerUser)
            .WithMany()
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Following>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Following>()
            .HasOne(f => f.FollowingUser)
            .WithMany()
            .HasForeignKey(f => f.FollowingId)
            .OnDelete(DeleteBehavior.NoAction);
        
        // Post feature - Entity configurations
        // Post entity configuration
        modelBuilder.Entity<Post>(entity =>
        {
            // Index cho UserId để query nhanh
            entity.HasIndex(p => p.UserId);
            // Composite index cho IsVisible và IsDeleted để filter nhanh
            entity.HasIndex(p => new { p.IsVisible, p.IsDeleted });
            // Giới hạn độ dài VisibleToRoles
            entity.Property(p => p.VisibleToRoles).HasMaxLength(50);
        });

        // PostComment entity configuration
        modelBuilder.Entity<PostComment>(entity =>
        {
            // Index cho PostId để query comments của post nhanh
            entity.HasIndex(pc => pc.PostId);
            // Index cho ParentCommentId để query replies nhanh
            entity.HasIndex(pc => pc.ParentCommentId);
            // Cấu hình relationship cho nested comments
            entity.HasOne(pc => pc.ParentComment)
                .WithMany(pc => pc.Replies)
                .HasForeignKey(pc => pc.ParentCommentId)
                .OnDelete(DeleteBehavior.NoAction); // Không cascade để tránh xóa nhầm
        });

        // PostVote entity configuration
        modelBuilder.Entity<PostVote>(entity =>
        {
            // Composite key: PostId + UserId (mỗi user chỉ vote 1 lần cho 1 post)
            entity.HasKey(pv => new { pv.PostId, pv.UserId });
        });

        // PostReport entity configuration
        modelBuilder.Entity<PostReport>(entity =>
        {
            // Index cho PostId để query reports của post nhanh
            entity.HasIndex(pr => pr.PostId);
            // Index cho Status để filter reports theo trạng thái nhanh
            entity.HasIndex(pr => pr.Status);
            // Giới hạn độ dài Status
            entity.Property(pr => pr.Status).HasMaxLength(20);
        });
        
        //RecoveryCodes
        // modelBuilder.Entity<RecoveryCode>(entity =>
        // {
        //     entity.HasIndex(e => new { e.UserId, e.IsUsed })
        //         .HasDatabaseName("IX_RecoveryCodes_UserId_IsUsed")
        //         .IncludeProperties(e => e.HashedCode); // Covering index siêu nhanh
        //
        //     entity.Property(e => e.HashedCode)
        //         .IsRequired()
        //         .HasMaxLength(255);
        // });
    }
}