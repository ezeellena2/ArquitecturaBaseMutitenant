using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IMemberReader
{
    Task<MemberRow?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberRow>> ListCurrentTenantAsync(CancellationToken cancellationToken);
}
