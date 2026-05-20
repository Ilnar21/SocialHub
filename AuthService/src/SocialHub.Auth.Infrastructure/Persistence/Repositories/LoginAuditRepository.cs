using SocialHub.Auth.Application.Abstractions;
using SocialHub.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SocialHub.Auth.Infrastructure.Persistence.Repositories;

public sealed class LoginAuditRepository(AuthDbContext dbContext) : ILoginAuditRepository
{
    public async Task AddAsync(LoginAuditEntry entry, CancellationToken cancellationToken) =>
        await dbContext.LoginAudit.AddAsync(entry, cancellationToken);

    public async Task<IReadOnlyCollection<LoginAuditEntry>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.LoginAudit
            .AsNoTracking()
            .OrderByDescending(entry => entry.CreatedAt)
            .ToArrayAsync(cancellationToken);
}
