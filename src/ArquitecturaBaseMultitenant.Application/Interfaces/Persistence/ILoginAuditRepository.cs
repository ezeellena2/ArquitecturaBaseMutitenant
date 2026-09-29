using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface ILoginAuditRepository
{
    void Add(LoginAudit audit);

    Task<DateTime?> FindLastSuccessAtUtcAsync(Guid userId, CancellationToken cancellationToken);
}
