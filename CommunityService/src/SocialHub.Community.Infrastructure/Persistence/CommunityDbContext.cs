using Microsoft.EntityFrameworkCore;
using SocialHub.Community.Domain.Constants;
using SocialHub.Community.Domain.Entities;

namespace SocialHub.Community.Infrastructure.Persistence;

public sealed class CommunityDbContext : DbContext
{
    public CommunityDbContext(DbContextOptions<CommunityDbContext> options)
        : base(options)
    {
    }

    public DbSet<Domain.Entities.Community> Communities => Set<Domain.Entities.Community>();
    public DbSet<CommunityMember> CommunityMembers => Set<CommunityMember>();
    public DbSet<CommunityJoinRequest> CommunityJoinRequests => Set<CommunityJoinRequest>();
    public DbSet<SuggestedPost> SuggestedPosts => Set<SuggestedPost>();
    public DbSet<CommunityAuditLog> CommunityAuditLogs => Set<CommunityAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Entities.Community>(builder =>
        {
            builder.ToTable("communities");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(CommunityLimits.NameMaxLength).IsRequired();
            builder.Property(x => x.NormalizedName).HasMaxLength(CommunityLimits.NameMaxLength).IsRequired();
            builder.Property(x => x.Username).HasMaxLength(CommunityLimits.UsernameMaxLength).IsRequired();
            builder.Property(x => x.NormalizedUsername).HasMaxLength(CommunityLimits.UsernameMaxLength).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(CommunityLimits.DescriptionMaxLength).IsRequired();
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.HasIndex(x => x.NormalizedName).IsUnique();
            builder.HasIndex(x => x.NormalizedUsername).IsUnique();
            builder.Metadata.FindNavigation(nameof(Domain.Entities.Community.Members))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(Domain.Entities.Community.SuggestedPosts))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(Domain.Entities.Community.JoinRequests))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CommunityMember>(builder =>
        {
            builder.ToTable("community_members");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.HasIndex(x => new { x.CommunityId, x.UserId }).IsUnique();
            builder.HasIndex(x => x.UserId);
            builder.HasOne(x => x.Community)
                .WithMany(nameof(Domain.Entities.Community.Members))
                .HasForeignKey(x => x.CommunityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommunityJoinRequest>(builder =>
        {
            builder.ToTable("community_join_requests");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(x => x.ReviewComment).HasMaxLength(500);
            builder.HasIndex(x => new { x.CommunityId, x.Status });
            builder.HasIndex(x => new { x.CommunityId, x.UserId, x.Status });
            builder.HasOne(x => x.Community)
                .WithMany(nameof(Domain.Entities.Community.JoinRequests))
                .HasForeignKey(x => x.CommunityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SuggestedPost>(builder =>
        {
            builder.ToTable("suggested_posts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).HasMaxLength(CommunityLimits.SuggestedPostTitleMaxLength).IsRequired();
            builder.Property(x => x.Text).HasMaxLength(CommunityLimits.SuggestedPostTextMaxLength).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(x => x.ReviewComment).HasMaxLength(500);
            builder.Property(x => x.PublicationWarning).HasMaxLength(500);
            builder.HasIndex(x => new { x.CommunityId, x.Status });
            builder.HasOne(x => x.Community)
                .WithMany(nameof(Domain.Entities.Community.SuggestedPosts))
                .HasForeignKey(x => x.CommunityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommunityAuditLog>(builder =>
        {
            builder.ToTable("community_audit_logs");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Action).HasMaxLength(80).IsRequired();
            builder.Property(x => x.Details).HasMaxLength(1_000).IsRequired();
            builder.HasIndex(x => new { x.CommunityId, x.CreatedAtUtc });
        });
    }
}
