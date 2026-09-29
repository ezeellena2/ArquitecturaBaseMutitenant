using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;

/// <summary>Estados de acceso con TTL breve e invalidación explícita al cambiar de estado.</summary>
public interface IAccessStatusCache
{
    Task<UserStatus?> GetUserStatusAsync(Guid userId, CancellationToken cancellationToken);

    Task<MemberStatus?> GetMemberStatusAsync(Guid userId, Guid tenantId, TenantKind kind,
        CancellationToken cancellationToken);

    ValueTask InvalidateUserAsync(Guid userId, CancellationToken cancellationToken);

    ValueTask InvalidateMemberAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}
