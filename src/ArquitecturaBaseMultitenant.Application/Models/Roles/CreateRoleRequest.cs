using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

public sealed record CreateRoleRequest(string? Name, string? Description, RoleScope? Scope, Guid? CompanyId,
    IReadOnlyCollection<string>? Permissions);
