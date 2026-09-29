using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

/// <summary>Proyección global de una organización, sin sus datos privados.</summary>
public sealed record TenantRow(Guid Id, TenantKind Kind, TenantStatus Status, string Name);
