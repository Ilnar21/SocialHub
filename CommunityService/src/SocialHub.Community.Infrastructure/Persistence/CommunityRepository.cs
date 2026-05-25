using Microsoft.EntityFrameworkCore;
using SocialHub.Community.Application.Abstractions;
using SocialHub.Community.Domain.Entities;
using SocialHub.Community.Domain.Enums;

namespace SocialHub.Community.Infrastructure.Persistence;

public sealed class CommunityRepository : ICommunityRepository
{
    private readonly CommunityDbContext _dbContext;

    public CommunityRepository(CommunityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<Domain.Entities.Community>> GetCommunitiesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Communities
            .AsNoTracking()
            .Include(c => c.Members)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Domain.Entities.Community>> GetCommunitiesByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.Communities
            .AsNoTracking()
            .Include(c => c.Members)
            .Where(c => c.Members.Any(m => m.UserId == userId))
            .ToListAsync(cancellationToken);
    }

    public Task<Domain.Entities.Community?> GetCommunityAsync(Guid communityId, CancellationToken cancellationToken)
    {
        return _dbContext.Communities
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == communityId, cancellationToken);
    }

    public Task<Domain.Entities.Community?> GetCommunityByUsernameAsync(string normalizedUsername, CancellationToken cancellationToken)
    {
        return _dbContext.Communities
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.NormalizedUsername == normalizedUsername, cancellationToken);
    }

    public Task<bool> CommunityNameExistsAsync(string normalizedName, CancellationToken cancellationToken)
    {
        return _dbContext.Communities.AnyAsync(c => c.NormalizedName == normalizedName, cancellationToken);
    }

    public Task<bool> CommunityUsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken)
    {
        return _dbContext.Communities.AnyAsync(c => c.NormalizedUsername == normalizedUsername, cancellationToken);
    }

    public Task<CommunityMember?> GetMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.CommunityMembers
            .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.UserId == userId, cancellationToken);
    }

    public Task<bool> IsMemberAsync(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.CommunityMembers
            .AsNoTracking()
            .AnyAsync(m => m.CommunityId == communityId && m.UserId == userId, cancellationToken);
    }

    public Task<List<CommunityMember>> GetMembersAsync(Guid communityId, CancellationToken cancellationToken)
    {
        return _dbContext.CommunityMembers
            .AsNoTracking()
            .Where(m => m.CommunityId == communityId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Guid>> GetCommunityIdsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.CommunityMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.CommunityId)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _dbContext.CommunityMembers.CountAsync(m => m.UserId == userId, cancellationToken);
    }

    public Task<SuggestedPost?> GetSuggestedPostAsync(Guid communityId, Guid suggestedPostId, CancellationToken cancellationToken)
    {
        return _dbContext.SuggestedPosts
            .FirstOrDefaultAsync(p => p.CommunityId == communityId && p.Id == suggestedPostId, cancellationToken);
    }

    public Task<List<SuggestedPost>> GetSuggestedPostsAsync(Guid communityId, SuggestedPostStatus? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.SuggestedPosts
            .AsNoTracking()
            .Where(p => p.CommunityId == communityId);

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status);
        }

        return query.ToListAsync(cancellationToken);
    }

    public async Task AddCommunityAsync(Domain.Entities.Community community, CancellationToken cancellationToken)
    {
        await _dbContext.Communities.AddAsync(community, cancellationToken);
    }

    public async Task AddMemberAsync(CommunityMember member, CancellationToken cancellationToken)
    {
        await _dbContext.CommunityMembers.AddAsync(member, cancellationToken);
    }

    public async Task AddSuggestedPostAsync(SuggestedPost suggestedPost, CancellationToken cancellationToken)
    {
        await _dbContext.SuggestedPosts.AddAsync(suggestedPost, cancellationToken);
    }

    public async Task AddAuditLogAsync(CommunityAuditLog auditLog, CancellationToken cancellationToken)
    {
        await _dbContext.CommunityAuditLogs.AddAsync(auditLog, cancellationToken);
    }

    public void RemoveMember(CommunityMember member)
    {
        _dbContext.CommunityMembers.Remove(member);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
