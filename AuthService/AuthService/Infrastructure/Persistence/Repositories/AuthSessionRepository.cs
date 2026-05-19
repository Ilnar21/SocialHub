using AuthService.Application.Abstractions;
using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence.Repositories;

public sealed class AuthSessionRepository(AuthDbContext dbContext) : IAuthSessionRepository
{
    public async Task AddAsync(AuthSession session, CancellationToken cancellationToken) =>
        await dbContext.AuthSessions.AddAsync(session, cancellationToken);

    public async Task<UserAccount?> FindUserByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var session = await dbContext.AuthSessions
            .Include(value => value.User)
            .FirstOrDefaultAsync(value => value.Token == token, cancellationToken);

        if (session is null)
        {
            return null;
        }

        if (session.ExpiresAt >= DateTimeOffset.UtcNow)
        {
            return session.User;
        }

        dbContext.AuthSessions.Remove(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return null;
    }
}
