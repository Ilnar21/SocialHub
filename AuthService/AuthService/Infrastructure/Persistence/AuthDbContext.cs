using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<LoginAuditEntry> LoginAudit => Set<LoginAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();

            entity.Property(user => user.Username).HasMaxLength(64).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(user => user.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(user => user.BlockReason).HasMaxLength(512);

            entity.OwnsOne(user => user.Profile, profile =>
            {
                profile.Property(value => value.DisplayName).HasColumnName("display_name").HasMaxLength(128).IsRequired();
                profile.Property(value => value.Bio).HasColumnName("bio").HasMaxLength(1000);
                profile.Property(value => value.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(512);
            });
        });

        modelBuilder.Entity<AuthSession>(entity =>
        {
            entity.ToTable("auth_sessions");
            entity.HasKey(session => session.Token);
            entity.Property(session => session.Token).HasMaxLength(2048);
            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoginAuditEntry>(entity =>
        {
            entity.ToTable("login_audit");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.UsernameOrEmail).HasMaxLength(256).IsRequired();
            entity.Property(entry => entry.Reason).HasMaxLength(256).IsRequired();
            entity.HasOne(entry => entry.User)
                .WithMany()
                .HasForeignKey(entry => entry.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        base.OnModelCreating(modelBuilder);
    }
}
