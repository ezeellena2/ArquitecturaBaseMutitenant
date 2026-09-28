using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Organización o espacio personal del acceso activo; nunca proviene del host ni de parámetros HTTP.</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    TenantKind? TenantKind { get; }

    Guid RequiredTenantId { get; }
}
