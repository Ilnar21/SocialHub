using AuthService.Domain.Entities;

namespace AuthService.Application.Abstractions;

public interface ILoginAuditRepository
{
    Task AddAsync(LoginAuditEntry entry, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<LoginAuditEntry>> ListAsync(CancellationToken cancellationToken);
}
