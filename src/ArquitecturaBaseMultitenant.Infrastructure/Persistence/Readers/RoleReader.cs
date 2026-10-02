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
        var roles = context.Roles.AsNoTracking().Where(role => role.TenantId == tenantId)
            .ApplySearch(request.Search, role => role.Name, role => role.Description);
        roles = request.Scope switch
        {
            RoleScopeFilter.Organization => roles.Where(role => role.Scope == RoleScope.Organization),
            RoleScopeFilter.Company => roles.Where(role => role.Scope != RoleScope.Organization),
            _ => roles,
        };
        roles = roles
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

    public async Task<RoleRow?> FindByIdAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var row = await Project(context.Roles.AsNoTracking().Where(role =>
            role.TenantId == tenantId && role.Id == roleId), tenantId).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : SortPermissions(row);
    }

    public Task<bool> ExistsByNameAsync(string name, Guid? excludedRoleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var tenantId = tenantContext.RequiredTenantId;
        var normalized = Role.NormalizeName(name);
        return context.Roles.AsNoTracking().AnyAsync(role => role.TenantId == tenantId
            && role.NormalizedName == normalized
            && (excludedRoleId == null || role.Id != excludedRoleId), cancellationToken);
    }

    public Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        return context.Members.AsNoTracking().CountAsync(member => member.TenantId == tenantId
            && member.Status == MemberStatus.Active
            && context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                && assignment.RoleId == roleId && assignment.MemberId == member.Id), cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListRoleIdsForUserAsync(Guid userId, Guid? companyId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The user id cannot be empty.", nameof(userId));
        var tenantId = tenantContext.RequiredTenantId;
        var assignments = context.RoleAssignments.AsNoTracking().Where(assignment =>
            assignment.TenantId == tenantId && context.Members.Any(member => member.TenantId == tenantId
                && member.Id == assignment.MemberId && member.UserId == userId && member.Status == MemberStatus.Active));

        if (companyId is null)
        {
            return await assignments.Where(assignment => assignment.CompanyId == null
                    && context.Roles.Any(role => role.TenantId == tenantId && role.Id == assignment.RoleId
                        && role.Scope == RoleScope.Organization))
                .Select(assignment => assignment.RoleId).Distinct().ToArrayAsync(cancellationToken);
        }

        return await assignments.Where(assignment =>
                context.Companies.Any(company => company.TenantId == tenantId && company.Id == companyId
                    && company.Status == CompanyStatus.Active)
                && context.Roles.Any(role => role.TenantId == tenantId && role.Id == assignment.RoleId
                    && ((assignment.CompanyId == companyId && role.Scope != RoleScope.Organization
                            && (role.Scope != RoleScope.SpecificCompany || role.CompanyId == companyId))
                        || (assignment.CompanyId == null && role.Scope == RoleScope.Organization)))
                && (assignment.CompanyId == null || context.CompanyMemberships.Any(membership =>
                    membership.TenantId == tenantId && membership.CompanyId == companyId
                    && membership.MemberId == assignment.MemberId
                    && membership.Status == CompanyMembershipStatus.Active)))
            .Select(assignment => assignment.RoleId).Distinct().ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListPermissionsForRoleAsync(Guid roleId,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var permissions = await context.Roles.AsNoTracking().Where(role => role.TenantId == tenantId && role.Id == roleId)
            .Select(role => role.Permissions.ToArray()).SingleOrDefaultAsync(cancellationToken);
        return permissions is null ? [] : permissions.Order(StringComparer.Ordinal).ToArray();
    }

    private IQueryable<RoleRow> Project(IQueryable<Role> roles, Guid tenantId) => roles.Select(role => new RoleRow(
        role.Id,
        role.Name,
        role.Description,
        role.Scope,
        role.CompanyId,
        role.IsSystemRole,
        context.Members.Count(member => member.TenantId == tenantId && member.Status == MemberStatus.Active
            && context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                && assignment.RoleId == role.Id && assignment.MemberId == member.Id)),
        role.Permissions.ToArray(),
        role.Version,
        role.CreatedAtUtc));

    private static RoleRow SortPermissions(RoleRow role) =>
        role with { Permissions = role.Permissions.Order(StringComparer.Ordinal).ToArray() };
}
