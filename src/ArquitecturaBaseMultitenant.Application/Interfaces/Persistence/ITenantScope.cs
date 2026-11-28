using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Entra temporalmente a un espacio después de autorizar, antes de abrir una transacción.</summary>
public interface ITenantScope
{
    IDisposable Enter(Guid tenantId);

    /// <summary>
    /// Entra temporalmente a un espacio con su clase explícita (<see cref="TenantKind.Business"/> o
    /// <see cref="TenantKind.Personal"/>) cuando el alcance técnico necesita crear datos públicos o compartidos.
    /// </summary>
    IDisposable Enter(Guid tenantId, TenantKind kind);
}
