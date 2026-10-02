using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

public sealed record UpdateRoleRequest(Guid RoleId, string? Name, string? Description, RoleScope? Scope,
    Guid? CompanyId, IReadOnlyCollection<string>? Permissions, uint? Version);
