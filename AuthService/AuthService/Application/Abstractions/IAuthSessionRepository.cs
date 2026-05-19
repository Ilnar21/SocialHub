using AuthService.Domain.Entities;

namespace AuthService.Application.Abstractions;

public interface IAuthSessionRepository
{
    Task AddAsync(AuthSession session, CancellationToken cancellationToken);

    Task<UserAccount?> FindUserByTokenAsync(string token, CancellationToken cancellationToken);
}
