using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Fija el tenant de un acceso ya autenticado, antes de consultar datos privados.</summary>
public interface ITenantAccessInitializer
{
    void SetFromAccess(Guid tenantId, TenantKind kind);
}
