using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;

internal sealed class TestTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid? TenantId => tenantId;
    public TenantKind? TenantKind => null;
    public Guid RequiredTenantId => tenantId ?? throw new InvalidOperationException("No active tenant.");
}
