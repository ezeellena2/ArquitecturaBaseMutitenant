using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>Entrega a cada participante la cuenta y, cuando corresponde, el tenant que debe limpiar durante la baja.</summary>
public sealed record AccountDeletionContext(Guid UserId, DateTime NowUtc, Guid? TenantId = null, TenantKind? TenantKind = null);

/// <summary>Identifica una organización afectada para procesarla en su propio alcance transaccional.</summary>
public sealed record AccountDeletionTenant(Guid TenantId, TenantKind Kind);
