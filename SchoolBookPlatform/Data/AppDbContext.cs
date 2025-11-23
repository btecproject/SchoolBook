using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Các DbSet cũ
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

    // DbSet post
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<PostComment> PostComments { get; set; } = null!;
    public DbSet<PostAttachment> PostAttachments { get; set; } = null!;
    public DbSet<PostVote> PostVotes { get; set; } = null!;
    public DbSet<PostReport> PostReports { get; set; } = null!;

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

        
        // Post
        modelBuilder.Entity<Post>()
            .HasOne(p => p.User)
            .WithMany() // User có thể có nhiều post, nhưng không cần navigation property ở User vì chưa có
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Post>()
            .HasIndex(p => p.UserId)
            .HasDatabaseName("IX_Posts_UserId");

        // PostComment
        modelBuilder.Entity<PostComment>()
            .HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PostComment>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PostComment>()
            .HasOne(c => c.ParentComment)
            .WithMany()
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PostComment>()
            .HasIndex(c => c.PostId)
            .HasDatabaseName("IX_PostComments_PostId");

        modelBuilder.Entity<PostComment>()
            .HasIndex(c => c.ParentCommentId)
            .HasDatabaseName("IX_PostComments_ParentId");

        // PostAttachment
        modelBuilder.Entity<PostAttachment>()
            .HasOne(a => a.Post)
            .WithMany(p => p.Attachments)
            .HasForeignKey(a => a.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PostAttachment>()
            .HasIndex(a => a.PostId)
            .HasDatabaseName("IX_PostAttachments_PostId");

        // PostVote (composite key)
        modelBuilder.Entity<PostVote>()
            .HasKey(v => new { v.PostId, v.UserId });

        modelBuilder.Entity<PostVote>()
            .HasOne(v => v.Post)
            .WithMany(p => p.Votes)
            .HasForeignKey(v => v.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PostVote>()
            .HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // PostReport
        modelBuilder.Entity<PostReport>()
            .HasOne(r => r.Post)
            .WithMany(p => p.Reports)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PostReport>()
            .HasOne(r => r.ReportedByUser)
            .WithMany()
            .HasForeignKey(r => r.ReportedBy)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PostReport>()
            .HasOne(r => r.ReviewedByUser)
            .WithMany()
            .HasForeignKey(r => r.ReviewedBy)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PostReport>()
            .HasIndex(r => r.PostId)
            .HasDatabaseName("IX_PostReports_PostId");
    }
}