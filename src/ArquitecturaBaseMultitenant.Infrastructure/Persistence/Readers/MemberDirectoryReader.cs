using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>
/// El directorio de miembros (C2 de la E12): parte siempre de los miembros de la organización del claim y une la cuenta solo
/// para su nombre visible, como <see cref="MemberReader"/>. Las condiciones son de una lista cerrada y los valores van como
/// parámetros de EF (ADR 0053); los roles y las empresas de cada fila se leen después, en una sola consulta para toda la lista.
/// Lee la cuenta global (identity) y por eso está en la lista cerrada de IdentityAccessTests.
/// </summary>
internal sealed class MemberDirectoryReader(ApplicationDbContext context, ITenantContext tenantContext) : IMemberDirectoryReader
{
    public async Task<MemberDirectoryResponse> ListAsync(MemberDirectoryQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Limit, 1);
        var tenantId = tenantContext.RequiredTenantId;
        using var timeout = ShortQueryTimeout.Apply(context);
        var members = Filter(Named(tenantId, activeOnly: false), query, tenantId);
        var ordered = query.Descending
            ? members.OrderByDescending(member => member.DisplayName).ThenByDescending(member => member.MemberId)
            : members.OrderBy(member => member.DisplayName).ThenBy(member => member.MemberId);
        var rows = await ordered.Take(query.Limit + 1).ToListAsync(ct);
        var exceedsLimit = rows.Count > query.Limit;
        if (exceedsLimit) rows.RemoveAt(rows.Count - 1);
        // El total exacto lo cuenta un COUNT con el mismo timeout, solo si pasa el tope.
        var total = exceedsLimit ? await members.CountAsync(ct) : rows.Count;
        var ids = rows.Select(row => row.MemberId).ToArray();
        var details = query.Detail switch
        {
            MemberDirectoryDetail.Roles => await RoleNamesAsync(tenantId, ids, ct),
            MemberDirectoryDetail.Companies => await CompanyNamesAsync(tenantId, ids, ct),
            _ => new Dictionary<Guid, IReadOnlyList<string>>(),
        };
        return new MemberDirectoryResponse([.. rows.Select(row => new MemberDirectoryRow(row.MemberId, row.DisplayName,
            details.GetValueOrDefault(row.MemberId) ?? []))], total, exceedsLimit);
    }

    public async Task<MemberDirectorySampleRow?> FindSampleAsync(Guid memberId, CancellationToken ct)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var row = await Named(tenantId, activeOnly: true).Where(member => member.MemberId == memberId).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        return (await SamplesAsync(tenantId, [row], ct))[0];
    }

    public async Task<PagedResult<MemberDirectorySampleRow>> ListSamplesAsync(PagedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var members = Named(tenantId, activeOnly: true).ApplySearch(request.Search, member => member.DisplayName);
        var page = await (Descending(request)
                ? members.OrderByDescending(member => member.DisplayName).ThenByDescending(member => member.MemberId)
                : members.OrderBy(member => member.DisplayName).ThenBy(member => member.MemberId))
            .ToPagedResultAsync(request, ct);
        return new PagedResult<MemberDirectorySampleRow>(await SamplesAsync(tenantId, page.Items, ct), page.Page, page.PageSize,
            page.TotalCount);
    }

    public Task<PagedResult<MemberDirectoryNameRow>> ListRolesAsync(PagedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var roles = context.Roles.AsNoTracking().Where(role => role.TenantId == tenantId).ApplySearch(request.Search, role => role.Name);
        return (Descending(request)
                ? roles.OrderByDescending(role => role.Name).ThenByDescending(role => role.Id)
                : roles.OrderBy(role => role.Name).ThenBy(role => role.Id))
            .Select(role => new MemberDirectoryNameRow(role.Id, role.Name)).ToPagedResultAsync(request, ct);
    }

    public Task<PagedResult<MemberDirectoryNameRow>> ListCompaniesAsync(PagedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var companies = context.Companies.AsNoTracking().Where(company => company.TenantId == tenantId)
            .ApplySearch(request.Search, company => company.Name);
        return (Descending(request)
                ? companies.OrderByDescending(company => company.Name).ThenByDescending(company => company.Id)
                : companies.OrderBy(company => company.Name).ThenBy(company => company.Id))
            .Select(company => new MemberDirectoryNameRow(company.Id, company.Name)).ToPagedResultAsync(request, ct);
    }

    public async Task<IReadOnlyList<MemberDirectoryNameRow>> ListRolesByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0) return [];
        var tenantId = tenantContext.RequiredTenantId;
        var wanted = ids.ToArray();
        return await context.Roles.AsNoTracking().Where(role => role.TenantId == tenantId && wanted.Contains(role.Id))
            .OrderBy(role => role.Name).ThenBy(role => role.Id)
            .Select(role => new MemberDirectoryNameRow(role.Id, role.Name)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<MemberDirectoryNameRow>> ListCompaniesByIdAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0) return [];
        var tenantId = tenantContext.RequiredTenantId;
        var wanted = ids.ToArray();
        return await context.Companies.AsNoTracking().Where(company => company.TenantId == tenantId && wanted.Contains(company.Id))
            .OrderBy(company => company.Name).ThenBy(company => company.Id)
            .Select(company => new MemberDirectoryNameRow(company.Id, company.Name)).ToArrayAsync(ct);
    }

    /// <summary>
    /// Los miembros con nombre visible: activos, o también inactivos para la lista (la columna <c>active</c> los separa). Una
    /// cuenta cuya baja empezó no entra, como en <c>UserDisplayNameReader</c>.
    /// </summary>
    private IQueryable<NamedMember> Named(Guid tenantId, bool activeOnly) =>
        from member in context.Members.AsNoTracking()
        where member.TenantId == tenantId
            && (member.Status == MemberStatus.Active || (!activeOnly && member.Status == MemberStatus.Inactive))
        join user in context.Users.AsNoTracking() on member.UserId equals user.Id
        where user.DeletedAtUtc == null && user.DeletionLeaseId == null && user.DisplayName != null && user.DisplayName != string.Empty
        select new NamedMember { MemberId = member.Id, DisplayName = user.DisplayName!, Active = member.Status == MemberStatus.Active };

    private IQueryable<NamedMember> Filter(IQueryable<NamedMember> members, MemberDirectoryQuery query, Guid tenantId)
    {
        foreach (var name in query.Names)
        {
            if (!name.Exact) { members = members.ApplySearch(name.Value, member => member.DisplayName); continue; }
            var exact = name.Value.Trim().ToLowerInvariant();
            // ToLower() se traduce a lower() de PostgreSQL, como ApplySearch: la cultura del proceso no interviene.
#pragma warning disable CA1304, CA1311, CA1862
            members = members.Where(member => SearchFunctions.Unaccent(member.DisplayName.ToLower()) == SearchFunctions.Unaccent(exact));
#pragma warning restore CA1304, CA1311, CA1862
        }

        foreach (var role in query.Roles)
        {
            var ids = role.Ids.ToArray();
            members = role.Include
                ? members.Where(member => context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                    && assignment.MemberId == member.MemberId && ids.Contains(assignment.RoleId)))
                : members.Where(member => !context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                    && assignment.MemberId == member.MemberId && ids.Contains(assignment.RoleId)));
        }

        foreach (var company in query.Companies)
        {
            var ids = company.Ids.ToArray();
            members = company.Include
                ? members.Where(member => context.CompanyMemberships.Any(membership => membership.TenantId == tenantId
                    && membership.MemberId == member.MemberId && membership.Status == CompanyMembershipStatus.Active
                    && ids.Contains(membership.CompanyId)))
                : members.Where(member => !context.CompanyMemberships.Any(membership => membership.TenantId == tenantId
                    && membership.MemberId == member.MemberId && membership.Status == CompanyMembershipStatus.Active
                    && ids.Contains(membership.CompanyId)));
        }

        foreach (var active in query.Active) members = members.Where(member => member.Active == active);
        if (query.ExcludedMemberId is { } excluded) members = members.Where(member => member.MemberId != excluded);
        return members;
    }

    private async Task<IReadOnlyList<MemberDirectorySampleRow>> SamplesAsync(Guid tenantId, IReadOnlyList<NamedMember> members,
        CancellationToken ct)
    {
        var ids = members.Select(member => member.MemberId).ToArray();
        var companies = await CompanyNamesAsync(tenantId, ids, ct);
        var roles = await RoleNamesAsync(tenantId, ids, ct);
        return [.. members.Select(member => new MemberDirectorySampleRow(member.MemberId, member.DisplayName,
            companies.GetValueOrDefault(member.MemberId) ?? [], roles.GetValueOrDefault(member.MemberId) ?? []))];
    }

    /// <summary>Los roles de cada miembro, de organización o de empresa, sin repetir y en orden alfabético.</summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> RoleNamesAsync(Guid tenantId, Guid[] memberIds, CancellationToken ct)
    {
        if (memberIds.Length == 0) return new Dictionary<Guid, IReadOnlyList<string>>();
        var rows = await (from assignment in context.RoleAssignments.AsNoTracking()
                          where assignment.TenantId == tenantId && memberIds.Contains(assignment.MemberId)
                          join role in context.Roles.AsNoTracking() on new { assignment.TenantId, Id = assignment.RoleId }
                              equals new { role.TenantId, role.Id }
                          select new { assignment.MemberId, role.Name })
            .Distinct().ToArrayAsync(ct);
        return Group(rows.Select(row => (row.MemberId, row.Name)));
    }

    /// <summary>Las empresas con una membresía activa de cada miembro, en orden alfabético.</summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> CompanyNamesAsync(Guid tenantId, Guid[] memberIds, CancellationToken ct)
    {
        if (memberIds.Length == 0) return new Dictionary<Guid, IReadOnlyList<string>>();
        var rows = await (from membership in context.CompanyMemberships.AsNoTracking()
                          where membership.TenantId == tenantId && memberIds.Contains(membership.MemberId)
                              && membership.Status == CompanyMembershipStatus.Active
                          join company in context.Companies.AsNoTracking() on new { membership.TenantId, Id = membership.CompanyId }
                              equals new { company.TenantId, company.Id }
                          select new { membership.MemberId, company.Name })
            .Distinct().ToArrayAsync(ct);
        return Group(rows.Select(row => (row.MemberId, row.Name)));
    }

    private static Dictionary<Guid, IReadOnlyList<string>> Group(IEnumerable<(Guid MemberId, string Name)> rows) =>
        rows.GroupBy(row => row.MemberId).ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group
            .Select(row => row.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).ToArray());

    private static bool Descending(PagedRequest request) => SortDescriptor.Parse(request.Sort)?.Descending ?? false;

    private sealed class NamedMember
    {
        public Guid MemberId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public bool Active { get; init; }
    }
}
