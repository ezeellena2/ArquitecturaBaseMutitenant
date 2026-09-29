using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

/// <summary>Membresía visible solo desde el tenant activo.</summary>
public sealed record MemberRow(Guid TenantId, Guid UserId, MemberStatus Status,
    UserStatus UserStatus, string? DisplayName);
