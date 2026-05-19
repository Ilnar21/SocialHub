using AuthService.Application.Abstractions;
using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AuthDbContext dbContext) : IUserRepository
{
    public async Task<IReadOnlyCollection<UserAccount>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Username)
            .ToArrayAsync(cancellationToken);

    public Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<UserAccount?> FindByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(
            user => user.Username.ToLower() == usernameOrEmail.ToLower()
                    || user.Email.ToLower() == usernameOrEmail.ToLower(),
            cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Username.ToLower() == username.ToLower(), cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Email.ToLower() == email.ToLower(), cancellationToken);

    public async Task AddAsync(UserAccount user, CancellationToken cancellationToken) =>
        await dbContext.Users.AddAsync(user, cancellationToken);
}
