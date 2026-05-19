using AuthService.Application.Abstractions;

namespace AuthService.Infrastructure.Persistence;

public sealed class EfUnitOfWork(AuthDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
