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
    
    public DbSet<UserRsaKey> UserRsaKeys { get; set; }
    public DbSet<ChatUser> ChatUsers { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ConversationMember> ConversationMembers { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<MessageAttachment> MessageAttachments { get; set; }
    public DbSet<MessageNotification> MessageNotifications { get; set; }
    public DbSet<ConversationKey>  ConversationKeys { get; set; }
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
        
        // CHAT
        // ConversationMember composite PK
        modelBuilder.Entity<ConversationMember>()
            .HasKey(cm => new { cm.ConversationId, cm.ChatUserId });

        // MessageNotification composite PK
        modelBuilder.Entity<MessageNotification>()
            .HasKey(mn => new { mn.RecipientId, mn.SenderId });

        // Unique active RSA key per user
        modelBuilder.Entity<UserRsaKey>()
            .HasIndex(k => k.ChatUserId)
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        // Các index khác theo schema
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ConversationId, m.CreatedAt });

        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.SenderId, m.CreatedAt });

        modelBuilder.Entity<MessageNotification>()
            .HasIndex(mn => new { mn.RecipientId, mn.UnreadCount });

        modelBuilder.Entity<ChatUser>()
            .HasIndex(cu => cu.DisplayName);

        modelBuilder.Entity<ChatUser>()
            .HasIndex(cu => cu.Username);

        modelBuilder.Entity<UserRsaKey>()
            .HasIndex(k => k.ExpiresAt);

        modelBuilder.Entity<UserRsaKey>()
            .HasIndex(k => k.IsActive);
        
        modelBuilder.Entity<ConversationKey>()
            .HasKey(k => new { k.ChatUserId, k.ConversationId, k.KeyVersion });
        base.OnModelCreating(modelBuilder);
    }
}