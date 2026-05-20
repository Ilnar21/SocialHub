using SocialHub.Auth.Domain.Entities;

namespace SocialHub.Auth.Application.Abstractions;

public interface ILoginAuditRepository
{
    Task AddAsync(LoginAuditEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LoginAuditEntry>> ListAsync(CancellationToken cancellationToken);
}
