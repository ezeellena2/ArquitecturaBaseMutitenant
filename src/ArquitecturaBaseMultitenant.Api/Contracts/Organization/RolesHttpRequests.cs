using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Application.Models.Roles;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Organization;

public sealed record RoleListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search, RoleScopeFilter? Scope,
    Guid? CompanyId);

/// <summary>Los roles que se pueden asignar en una empresa: sin filtros de alcance, que fija la ruta.</summary>
public sealed record CompanyRoleListHttpQuery(int? Page, int? PageSize, string? Sort, string? Search);

public sealed record CreateRoleHttpRequest(string? Name, string? Description, RoleScope? Scope, Guid? CompanyId,
    IReadOnlyCollection<string>? Permissions);

public sealed record UpdateRoleHttpRequest(string? Name, string? Description, RoleScope? Scope, Guid? CompanyId,
    IReadOnlyCollection<string>? Permissions, uint? Version);

public sealed record DeleteRoleHttpRequest(uint? Version);
