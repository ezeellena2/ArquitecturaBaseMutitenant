using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record RoleListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search);

public sealed record CreateRoleHttpRequest(string? Name, string? Description, RoleScope? Scope, Guid? CompanyId,
    IReadOnlyCollection<string>? Permissions);

public sealed record UpdateRoleHttpRequest(string? Name, string? Description, RoleScope? Scope, Guid? CompanyId,
    IReadOnlyCollection<string>? Permissions, uint? Version);

public sealed record DeleteRoleHttpRequest(uint? Version);
