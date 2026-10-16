using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyecta roles del tenant activo y mantiene el orden y búsqueda del catálogo.</summary>
internal sealed class RoleReader(ApplicationDbContext context, ITenantContext tenantContext) : IRoleReader
{
    private static readonly SortMap<Role> Sort = new()
    {
        ["name"] = role => role.Name,
        ["createdAtUtc"] = role => role.CreatedAtUtc,
    };

    private static readonly SortDescriptor DefaultSort = new("name", Descending: false);

    public async Task<RoleFilterCountsResponse> GetFilterCountsAsync(string? search, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var all = ApplyRoleFilters(tenantId, new ListRolesRequest());
        var total = await CountScopesAsync(all, cancellationToken);
        var filtered = ApplyRoleFilters(tenantId, new ListRolesRequest { Search = search });
        var matching = string.IsNullOrWhiteSpace(search) ? total : await CountScopesAsync(filtered, cancellationToken);
        var anyCompany = matching.GetValueOrDefault(RoleScope.AnyCompany);
        var companies = await filtered.Where(role => role.Scope == RoleScope.SpecificCompany && role.CompanyId != null)
            .GroupBy(role => role.CompanyId!.Value)
            .Select(group => new { CompanyId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.CompanyId, row => row.Count + anyCompany, cancellationToken);
        return new RoleFilterCountsResponse(ScopeCounts(matching), ScopeCounts(total), anyCompany, companies);
    }

    private static Task<Dictionary<RoleScope, int>> CountScopesAsync(IQueryable<Role> roles, CancellationToken ct) =>
        roles.GroupBy(role => role.Scope).Select(group => new { Scope = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Scope, row => row.Count, ct);

    private static RoleScopeCounts ScopeCounts(IReadOnlyDictionary<RoleScope, int> counts) =>
        new(counts.Values.Sum(), counts.GetValueOrDefault(RoleScope.Organization),
            counts.GetValueOrDefault(RoleScope.AnyCompany) + counts.GetValueOrDefault(RoleScope.SpecificCompany));

    public async Task<IReadOnlyList<RoleRow>> ListAllAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var roles = context.Roles.AsNoTracking().Where(role => role.TenantId == tenantId)
            .OrderBy(role => role.Name).ThenBy(role => role.Id);
        var rows = await Project(roles, tenantId)
            .ToArrayAsync(cancellationToken);
        return [.. rows.Select(SortPermissions)];
    }

    public async Task<PagedResult<RoleRow>> ListAsync(ListRolesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var roles = ApplyRoleFilters(tenantId, request)
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, role => role.Id);
        var page = await roles.ToPagedResultAsync(request, cancellationToken);
        if (page.Items.Count == 0)
            return new PagedResult<RoleRow>([], page.Page, page.PageSize, page.TotalCount);

        var roleIds = page.Items.Select(role => role.Id).ToArray();
        var rows = await Project(context.Roles.AsNoTracking().Where(role => roleIds.Contains(role.Id)), tenantId)
            .ToDictionaryAsync(role => role.Id, cancellationToken);
        return new PagedResult<RoleRow>([.. page.Items.Select(role => SortPermissions(rows[role.Id]))],
            page.Page, page.PageSize, page.TotalCount);
    }

    private IQueryable<Role> ApplyRoleFilters(Guid tenantId, ListRolesRequest request)
    {
        var roles = context.Roles.AsNoTracking().Where(role => role.TenantId == tenantId)
            .ApplySearch(request.Search, role => role.Name, role => role.Description);
        if (request.SystemRoleKey is { } systemKey)
            roles = roles.Where(role => role.IsSystemRole && role.Name == systemKey);
        roles = request.Scope switch
        {
            RoleScopeFilter.Organization => roles.Where(role => role.Scope == RoleScope.Organization),
            RoleScopeFilter.Company => roles.Where(role => role.Scope != RoleScope.Organization),
            _ => roles,
        };
        if (request.CompanyId is { } companyId)
            roles = roles.Where(role => role.Scope == RoleScope.AnyCompany
                || role.Scope == RoleScope.SpecificCompany && role.CompanyId == companyId);
        if (request.OnlyOwn)
            roles = roles.Where(role => role.Scope == RoleScope.SpecificCompany && role.CompanyId == request.CompanyId);
        return roles;
    }

    public async Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var row = await Project(context.Roles.AsNoTracking().Where(role =>
            role.TenantId == tenantId && role.Id == roleId), tenantId).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : SortPermissions(row);
    }

    public Task<bool> ExistsByNameAsync(string name, Guid? companyId, Guid? excludedRoleId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var tenantId = tenantContext.RequiredTenantId;
        var normalized = Role.NormalizeName(name);
        return context.Roles.AsNoTracking().AnyAsync(role => role.TenantId == tenantId
            && role.NormalizedName == normalized && role.CompanyId == companyId
            && (excludedRoleId == null || role.Id != excludedRoleId), cancellationToken);
    }


    /// <summary>La misma cuenta que muestran la lista y la ficha (<see cref="Project"/>).</summary>
    public async Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken) =>
        (await FindByIdAsync(roleId, cancellationToken))?.UserCount ?? 0;

    public async Task<IReadOnlyList<Guid>> ListOrganizationRoleIdsAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        var tenantId = tenantContext.RequiredTenantId;
        return await (from assignment in context.RoleAssignments.AsNoTracking()
                      join member in context.Members.AsNoTracking()
                          on new { assignment.TenantId, assignment.MemberId }
                          equals new { member.TenantId, MemberId = member.Id }
                      join role in context.Roles.AsNoTracking()
                          on new { assignment.TenantId, assignment.RoleId } equals new { role.TenantId, RoleId = role.Id }
                      where assignment.TenantId == tenantId && member.UserId == userId
                          && member.Status == MemberStatus.Active
                          && assignment.CompanyId == null && role.Scope == RoleScope.Organization
                      select assignment.RoleId)
            .Distinct().ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> ListCompanyRoleIdsAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        var tenantId = tenantContext.RequiredTenantId;
        // Las mismas empresas que «Tus empresas» del inicio: membresía, miembro y empresa activos.
        var memberships = await (from membership in context.CompanyMemberships.AsNoTracking()
                                 join member in context.Members.AsNoTracking()
                                     on new { membership.TenantId, membership.MemberId }
                                     equals new { member.TenantId, MemberId = member.Id }
                                 join company in context.Companies.AsNoTracking()
                                     on new { membership.TenantId, membership.CompanyId }
                                     equals new { company.TenantId, CompanyId = company.Id }
                                 where membership.TenantId == tenantId && member.UserId == userId
                                     && member.Status == MemberStatus.Active
                                     && membership.Status == CompanyMembershipStatus.Active
                                     && company.Status == CompanyStatus.Active
                                 select new { membership.CompanyId, membership.MemberId })
            .ToArrayAsync(cancellationToken);
        if (memberships.Length == 0) return new Dictionary<Guid, IReadOnlyList<Guid>>();

        var companyIds = memberships.Select(row => row.CompanyId).Distinct().ToArray();
        var memberIds = memberships.Select(row => row.MemberId).Distinct().ToArray();
        // Un rol de empresa vale donde se asignó, mientras su alcance no haya cambiado por fuera del servicio.
        var assigned = await (from assignment in context.RoleAssignments.AsNoTracking()
                              join role in context.Roles.AsNoTracking()
                                  on new { assignment.TenantId, assignment.RoleId }
                                  equals new { role.TenantId, RoleId = role.Id }
                              where assignment.TenantId == tenantId && memberIds.Contains(assignment.MemberId)
                                  && assignment.CompanyId != null && companyIds.Contains(assignment.CompanyId.Value)
                                  && role.Scope != RoleScope.Organization
                                  && (role.Scope != RoleScope.SpecificCompany || role.CompanyId == assignment.CompanyId)
                              select new { CompanyId = assignment.CompanyId!.Value, assignment.MemberId, assignment.RoleId })
            .ToArrayAsync(cancellationToken);
        var valid = memberships.Select(row => (row.CompanyId, row.MemberId)).ToHashSet();
        return companyIds.ToDictionary(companyId => companyId, companyId => (IReadOnlyList<Guid>)assigned
            .Where(row => row.CompanyId == companyId && valid.Contains((row.CompanyId, row.MemberId)))
            .Select(row => row.RoleId).Distinct().ToArray());
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ListPermissionsByRoleAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        if (roleIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<string>>();
        var tenantId = tenantContext.RequiredTenantId;
        Guid[] ids = [.. roleIds];
        var rows = await context.Roles.AsNoTracking().Where(role => role.TenantId == tenantId && ids.Contains(role.Id))
            .Select(role => new { role.Id, Permissions = role.Permissions.ToArray() })
            .ToArrayAsync(cancellationToken);
        return rows.ToDictionary(row => row.Id,
            row => (IReadOnlyList<string>)[.. row.Permissions.Order(StringComparer.Ordinal)]);
    }

    // Personas asignadas a un rol (decisión 42): cada persona con una asignación del rol, activa, invitada o
    // desactivada. Mientras haya una, el rol no cambia dónde vale ni se borra (la clave foránea es Restrict).
    private IQueryable<RoleRow> Project(IQueryable<Role> roles, Guid tenantId) => roles.Select(role => new RoleRow(
        role.Id,
        role.Name,
        role.Description,
        role.Scope,
        role.CompanyId,
        role.IsSystemRole,
        context.RoleAssignments.Where(assignment => assignment.TenantId == tenantId && assignment.RoleId == role.Id)
            .Select(assignment => assignment.MemberId).Distinct().Count(),
        role.Permissions.ToArray(),
        role.Version,
        role.CreatedAtUtc,
        context.Companies.Where(company => company.TenantId == tenantId && company.Id == role.CompanyId)
            .Select(company => company.Name).FirstOrDefault()));

    private static RoleRow SortPermissions(RoleRow role) =>
        role with { Permissions = role.Permissions.Order(StringComparer.Ordinal).ToArray() };
}
