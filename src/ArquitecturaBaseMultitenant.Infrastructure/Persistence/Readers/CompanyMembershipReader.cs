using System.Linq.Expressions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Pagina miembros de empresa en SQL y proyecta roles/correo sin materializar identidades completas.</summary>
internal sealed class CompanyMembershipReader(ApplicationDbContext context, ITenantContext tenantContext)
    : ICompanyMembershipReader
{
    private static readonly SortMap<StateRow> Sort = new()
    {
        ["displayName"] = row => row.DisplayName,
        ["email"] = row => row.Email,
    };

    private static readonly SortMap<CandidateRow> CandidateSort = new()
    {
        ["displayName"] = row => row.DisplayName,
    };

    private static readonly SortDescriptor DefaultSort = new("displayName", Descending: false);

    /// <summary>
    /// El estado de un miembro de la empresa en SQL: la única expresión que usan el filtro, la fila y los conteos.
    /// Sigue a <c>CompanyMemberStatePolicy</c>, y un test de paridad las compara.
    /// </summary>
    internal static readonly Expression<Func<StateInput, StateRow>> WithState = input => new StateRow
    {
        MemberId = input.MemberId,
        DisplayName = input.DisplayName,
        Email = input.Email,
        State = input.MemberStatus == MemberStatus.Invited
            ? CompanyMemberState.Invited
            : input.MembershipStatus == CompanyMembershipStatus.Inactive || input.MemberStatus != MemberStatus.Active
                || input.UserStatus == UserStatus.Suspended || input.UserStatus == UserStatus.Deleted
                ? CompanyMemberState.Inactive
                : input.UserStatus == UserStatus.PendingDeletion
                    ? CompanyMemberState.PendingDeletion
                    : CompanyMemberState.Active,
    };

    public async Task<PagedResult<CompanyMemberRow>> ListAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var page = await Memberships(tenantId, companyId, request)
            .ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, row => row.MemberId)
            .ToPagedResultAsync(request, cancellationToken);
        if (page.Items.Count == 0)
            return new PagedResult<CompanyMemberRow>([], page.Page, page.PageSize, page.TotalCount);

        var memberIds = page.Items.Select(row => row.MemberId).ToArray();
        var pageRows = await ProjectAsync(tenantId, companyId, memberIds, cancellationToken);
        var rowsById = pageRows.ToDictionary(member => member.MemberId);
        var roleRows = await (from assignment in context.RoleAssignments.AsNoTracking()
                              join role in context.Roles.AsNoTracking()
                                  on new { assignment.TenantId, assignment.RoleId }
                                  equals new { role.TenantId, RoleId = role.Id }
                              where assignment.TenantId == tenantId && assignment.CompanyId == companyId
                                  && memberIds.Contains(assignment.MemberId)
                              orderby role.Name
                              select new { assignment.MemberId, Role = new CompanyMemberRoleRow(role.Id,
                                  role.Name, role.IsSystemRole, role.Scope, role.CompanyId) })
            .ToArrayAsync(cancellationToken);
        var rolesByMember = roleRows.GroupBy(item => item.MemberId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CompanyMemberRoleRow>)[.. group.Select(item => item.Role)]);
        var pendingInvitations = (await context.Invitations.AsNoTracking()
                .Where(invitation => invitation.TenantId == tenantId && memberIds.Contains(invitation.MemberId)
                    && invitation.Status == Domain.Invitations.InvitationStatus.Pending)
                .OrderByDescending(invitation => invitation.IssuedAtUtc)
                .Select(invitation => new PendingInvitation(invitation.Id, invitation.MemberId,
                    invitation.Destination, invitation.ExpiresAtUtc))
                .ToArrayAsync(cancellationToken))
            .GroupBy(item => item.MemberId)
            .ToDictionary(group => group.Key, group => group.First());

        var mapped = page.Items.Select(state => (state.State, Member: rowsById[state.MemberId],
                Invitation: pendingInvitations.GetValueOrDefault(state.MemberId)))
            .Select(item => new CompanyMemberRow(item.Member.MemberId, item.Member.UserId,
                item.Member.DisplayName.Length == 0 ? item.Invitation?.Destination.DisplayValue ?? "" : item.Member.DisplayName,
                ToEmail(item.Member.Email ?? item.Invitation?.Destination.DisplayValue), item.State,
                item.Member.MembershipStatus, rolesByMember.GetValueOrDefault(item.Member.MemberId, []),
                item.Member.Version, item.Member.CreatedAtUtc,
                // Solo la invitación que incluye a esta empresa: si se la sacaron, se reenvía desde Usuarios.
                item.Member.MembershipStatus == CompanyMembershipStatus.Active ? item.Invitation?.Id : null,
                item.Member.MembershipStatus == CompanyMembershipStatus.Active ? item.Invitation?.ExpiresAtUtc : null))
            .ToArray();
        return new PagedResult<CompanyMemberRow>(mapped, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<CompanyMemberFilterCountsResponse> GetFilterCountsAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var counts = await Memberships(tenantId, companyId, request with { State = null }).GroupBy(row => row.State)
            .Select(group => new { State = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.State, group => group.Count, cancellationToken);
        int Count(CompanyMemberState state) => counts.GetValueOrDefault(state);
        return new CompanyMemberFilterCountsResponse(counts.Values.Sum(), Count(CompanyMemberState.Active),
            Count(CompanyMemberState.PendingDeletion), Count(CompanyMemberState.Inactive),
            Count(CompanyMemberState.Invited));
    }

    public async Task<IReadOnlyList<UserCompanyRow>> ListActiveForUserAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var companies = await (from membership in context.CompanyMemberships.AsNoTracking()
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
                               orderby company.Name, company.Id
                               select new { membership.MemberId, CompanyId = company.Id, company.Name })
            .ToArrayAsync(cancellationToken);
        if (companies.Length == 0) return [];

        var memberIds = companies.Select(item => item.MemberId).Distinct().ToArray();
        var companyIds = companies.Select(item => item.CompanyId).ToArray();
        var roles = await (from assignment in context.RoleAssignments.AsNoTracking()
                           join role in context.Roles.AsNoTracking()
                               on new { assignment.TenantId, assignment.RoleId }
                               equals new { role.TenantId, RoleId = role.Id }
                           where assignment.TenantId == tenantId && memberIds.Contains(assignment.MemberId)
                               && assignment.CompanyId != null && companyIds.Contains(assignment.CompanyId.Value)
                           orderby role.Name
                           select new { CompanyId = assignment.CompanyId!.Value, Role = new CompanyMemberRoleRow(role.Id,
                               role.Name, role.IsSystemRole, role.Scope, role.CompanyId) })
            .ToArrayAsync(cancellationToken);
        var rolesByCompany = roles.GroupBy(item => item.CompanyId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<CompanyMemberRoleRow>)[.. group.Select(item => item.Role)]);
        return [.. companies.Select(item => new UserCompanyRow(item.CompanyId, item.Name,
            rolesByCompany.GetValueOrDefault(item.CompanyId, [])))];
    }

    public async Task<PagedResult<CompanyCandidateRow>> ListCandidatesAsync(Guid companyId,
        ListCompanyCandidatesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        // Activas como en Usuarios (miembro y cuenta activos) y sin membresía en la empresa, en ningún estado: a quien
        // quedó inactivo en ella se lo reactiva desde Miembros, y sumarlo de nuevo daría AlreadyExists.
        var members = context.Members.AsNoTracking().Where(member => member.TenantId == tenantId
            && member.Status == MemberStatus.Active
            && context.Users.Any(user => user.Id == member.UserId && user.Status == UserStatus.Active)
            && !context.CompanyMemberships.Any(membership => membership.TenantId == tenantId
                && membership.CompanyId == companyId && membership.MemberId == member.Id));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Solo por nombre: buscar por correo destaparía de a una letra el correo que la fila muestra tapado.
            var byName = context.Users.AsNoTracking()
                .ApplySearch(request.Search, user => user.DisplayName)
                .Select(user => (Guid?)user.Id);
            members = members.Where(member => byName.Contains(member.UserId));
        }

        var page = await members.Select(member => new CandidateRow
            {
                MemberId = member.Id,
                DisplayName = context.Users.Where(user => user.Id == member.UserId)
                    .Select(user => user.DisplayName ?? string.Empty).FirstOrDefault(),
                Email = context.LoginMethods
                    .Where(method => method.UserId == member.UserId && method.Type == LoginMethodType.Email
                        && method.VerifiedAtUtc != null)
                    .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                    .Select(method => method.Value).FirstOrDefault(),
            })
            .ApplySort(SortDescriptor.Parse(request.Sort), CandidateSort, DefaultSort, row => row.MemberId)
            .ToPagedResultAsync(request, cancellationToken);
        return new PagedResult<CompanyCandidateRow>([.. page.Items.Select(row => new CompanyCandidateRow(
            row.MemberId, row.DisplayName ?? string.Empty, ToEmail(row.Email)))], page.Page, page.PageSize,
            page.TotalCount);
    }

    private IQueryable<StateRow> Memberships(Guid tenantId, Guid companyId, ListCompanyMembersRequest request)
    {
        var rows = context.CompanyMemberships.AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membership.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var matching = MemberSearch.MatchingMemberIds(context, tenantId, request.Search);
            rows = rows.Where(membership => matching.Contains(membership.MemberId));
        }

        if (request.RoleId is { } roleId)
            rows = rows.Where(membership => context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                && assignment.CompanyId == companyId && assignment.MemberId == membership.MemberId
                && assignment.RoleId == roleId));
        if (request.WithoutRoles is true)
            rows = rows.Where(membership => !context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                && assignment.CompanyId == companyId && assignment.MemberId == membership.MemberId));
        var states = States(rows, tenantId);
        return request.State is { } state ? states.Where(row => row.State == state) : states;
    }

    // Lee una sola vez lo que decide el estado y el orden, y le aplica la expresión del estado.
    private IQueryable<StateRow> States(IQueryable<CompanyMembership> memberships, Guid tenantId) =>
        (from membership in memberships
         join member in context.Members.AsNoTracking()
             on new { membership.TenantId, membership.MemberId } equals new { member.TenantId, MemberId = member.Id }
         where member.TenantId == tenantId
         select new StateInput
         {
             MemberId = membership.MemberId,
             MemberStatus = member.Status,
             UserStatus = context.Users.Where(user => user.Id == member.UserId)
                 .Select(user => (UserStatus?)user.Status).FirstOrDefault(),
             MembershipStatus = membership.Status,
             DisplayName = context.Users.Where(user => user.Id == member.UserId)
                 .Select(user => user.DisplayName ?? string.Empty).FirstOrDefault(),
             Email = context.LoginMethods
                 .Where(method => method.UserId == member.UserId && method.Type == LoginMethodType.Email
                     && method.VerifiedAtUtc != null)
                 .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                 .Select(method => method.Value).FirstOrDefault(),
         })
        .Select(WithState);

    private async Task<MemberProjection[]> ProjectAsync(Guid tenantId, Guid companyId, Guid[] memberIds,
        CancellationToken cancellationToken) => await (
        from membership in context.CompanyMemberships.AsNoTracking()
        join member in context.Members.AsNoTracking()
            on new { membership.TenantId, MemberId = membership.MemberId }
            equals new { member.TenantId, MemberId = member.Id }
        join userCandidate in context.Users.AsNoTracking()
            on member.UserId equals (Guid?)userCandidate.Id into userGroup
        from user in userGroup.DefaultIfEmpty()
        where membership.TenantId == tenantId && membership.CompanyId == companyId
            && memberIds.Contains(membership.MemberId)
        select new MemberProjection(membership.MemberId, member.UserId,
            user == null ? string.Empty : user.DisplayName ?? string.Empty,
            member.UserId == null ? null : context.LoginMethods.AsNoTracking()
                .Where(method => method.UserId == member.UserId && method.Type == LoginMethodType.Email
                    && method.VerifiedAtUtc != null)
                .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                .Select(method => method.Value).FirstOrDefault(),
            membership.Status, membership.Version, membership.CreatedAtUtc))
        .ToArrayAsync(cancellationToken);

    private static Email? ToEmail(string? value) => value is null ? null : Email.Create(value).Value;

    /// <summary>Lo que decide el estado y el orden de un miembro de la empresa, leído en SQL.</summary>
    internal sealed class StateInput
    {
        public Guid MemberId { get; init; }
        public MemberStatus MemberStatus { get; init; }
        public UserStatus? UserStatus { get; init; }
        public CompanyMembershipStatus MembershipStatus { get; init; }
        public string? DisplayName { get; init; }
        public string? Email { get; init; }
    }

    /// <summary>Un miembro con su estado: lo que se filtra, se ordena y se cuenta.</summary>
    internal sealed class StateRow
    {
        public Guid MemberId { get; init; }
        public CompanyMemberState State { get; init; }
        public string? DisplayName { get; init; }
        public string? Email { get; init; }
    }

    /// <summary>Una persona que se puede sumar a la empresa: lo que se ordena y se muestra.</summary>
    internal sealed class CandidateRow
    {
        public Guid MemberId { get; init; }
        public string? DisplayName { get; init; }
        public string? Email { get; init; }
    }

    private sealed record PendingInvitation(Guid Id, Guid MemberId, Email Destination, DateTime ExpiresAtUtc);

    private sealed record MemberProjection(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
        CompanyMembershipStatus MembershipStatus, uint Version, DateTime CreatedAtUtc);
}
