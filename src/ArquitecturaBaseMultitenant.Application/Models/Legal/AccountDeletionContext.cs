using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

public sealed record AccountDeletionContext(Guid UserId, DateTime NowUtc, Guid? TenantId = null, TenantKind? TenantKind = null);
public sealed record AccountDeletionTenant(Guid TenantId, TenantKind Kind);
