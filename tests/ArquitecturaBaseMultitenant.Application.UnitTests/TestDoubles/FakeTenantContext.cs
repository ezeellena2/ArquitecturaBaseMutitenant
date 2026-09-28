using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.TestDoubles;

/// <summary>Contexto del acceso activo configurable por cada test de un servicio.</summary>
internal sealed class FakeTenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }

    public TenantKind? TenantKind { get; set; }

    public Guid RequiredTenantId => TenantId ??
        throw new InvalidOperationException("The active access has no tenant.");
}
