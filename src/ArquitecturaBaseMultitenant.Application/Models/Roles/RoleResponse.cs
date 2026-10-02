using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

/// <summary>Representación HTTP-independent de un rol; version conserva el xmin de PostgreSQL.</summary>
public sealed record RoleResponse(Guid Id, string Name, string? Description, RoleScope Scope, Guid? CompanyId,
    bool IsSystemRole, int UserCount, IReadOnlyList<string> Permissions, uint Version, DateTime CreatedAtUtc);
