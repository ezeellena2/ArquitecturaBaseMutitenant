using ArquitecturaBaseMultitenant.Application.Models.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Proyecta membresías privadas del tenant activo sin salir de su alcance RLS.</summary>
public interface IMemberReader
{
    Task<MemberRow?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MemberRow>> ListCurrentTenantAsync(CancellationToken cancellationToken);
}
