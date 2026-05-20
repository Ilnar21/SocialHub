using SocialHub.Auth.Domain.Entities;

namespace SocialHub.Auth.Application.Abstractions;

public interface IUserRepository
{
    Task<IReadOnlyCollection<UserAccount>> ListAsync(CancellationToken cancellationToken);

    Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<UserAccount?> FindByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken cancellationToken);

    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    Task AddAsync(UserAccount user, CancellationToken cancellationToken);
}
