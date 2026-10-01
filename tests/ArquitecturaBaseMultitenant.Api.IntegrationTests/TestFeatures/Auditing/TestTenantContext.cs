using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;

/// <summary>
/// Proporciona el espacio controlado por cada test de auditoría. Permite comprobar sellado y rechazo cuando
/// falta un contexto requerido.
/// </summary>
internal sealed class TestTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid? TenantId => tenantId;
    public TenantKind? TenantKind => null;
    public Guid RequiredTenantId => tenantId ?? throw new InvalidOperationException("No active tenant.");
}
