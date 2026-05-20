using SocialHub.Auth.Domain.Entities;

namespace SocialHub.Auth.Application.Abstractions;

public interface IAuthSessionRepository
{
    Task AddAsync(AuthSession session, CancellationToken cancellationToken);

    Task<UserAccount?> FindUserByTokenAsync(string token, CancellationToken cancellationToken);
}
