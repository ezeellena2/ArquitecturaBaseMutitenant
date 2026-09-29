using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

/// <summary>Acceso de una cuenta leído del índice global y de la organización global.</summary>
public sealed record UserTenantAccessRow(Guid TenantId, TenantKind Kind, string Name, string? Slug,
    TenantStatus TenantStatus, MemberStatus MemberStatus, DateTime? JoinedAtUtc);
