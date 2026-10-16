using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>
/// Estados de acceso en la caché compartida (ADR 0052), con TTL breve. Un cambio confirmado avanza la generación de su
/// etiqueta en la misma transacción: nadie invalida a mano.
/// </summary>
public interface IAccessStatusCache
{
    Task<UserStatus?> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken);

    Task<MemberStatus?> GetMemberStatusAsync(Guid userId, Guid tenantId, TenantKind kind,
        CancellationToken cancellationToken);
}
