using System.Linq.Expressions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Pagina miembros e invitaciones en SQL y completa roles/empresas solo para los resultados pedidos.</summary>
internal sealed class OrganizationUserReader(ApplicationDbContext context, ITenantContext tenantContext)
    : IOrganizationUserReader
{
    private static readonly SortMap<StateRow> Sort = new()
    {
        ["displayName"] = row => row.DisplayName,
        ["email"] = row => row.Email,
        ["createdAtUtc"] = row => row.CreatedAtUtc,
    };

    private static readonly SortDescriptor DefaultSort = new("displayName", Descending: false);

    /// <summary>
    /// El estado de una persona en SQL: la única expresión que usan el filtro, la fila y los conteos. Sigue a
    /// <c>OrganizationUserStatePolicy</c>, y un test de paridad las compara.
    /// </summary>
    internal static readonly Expression<Func<StateInput, StateRow>> WithState = input => new StateRow
    {
        MemberId = input.MemberId,
        DisplayName = input.DisplayName,
        Email = input.Email,
        CreatedAtUtc = input.CreatedAtUtc,
        PendingSinceUtc = input.MemberStatus == MemberStatus.Invited
            ? input.PendingInvitationExpiresAtUtc : input.DeletionRequestedAtUtc,
        State = input.MemberStatus == MemberStatus.Invited && input.PendingInvitationExpiresAtUtc != null
            ? input.PendingInvitationExpiresAtUtc <= input.NowUtc
                ? OrganizationUserState.Expired
                : OrganizationUserState.Invited
            : input.MemberStatus == MemberStatus.Active && input.UserStatus == UserStatus.PendingDeletion
                ? OrganizationUserState.PendingDeletion
                : input.MemberStatus == MemberStatus.Active && input.UserStatus == UserStatus.Active
                    ? OrganizationUserState.Active
                    : OrganizationUserState.Inactive,
    };

    public async Task<PagedResult<OrganizationUserRow>> ListAsync(ListOrganizationUsersRequest request,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(nowUtc);
        var tenantId = tenantContext.RequiredTenantId;
        var query = Filtered(tenantId, request, nowUtc);
        if (request.PendingPreview)
            request = request with { Page = PagedRequest.DefaultPage, PageSize = PendingGroup.PreviewLimit };
        var sorted = request.PendingPreview
            ? query.OrderBy(row => row.PendingSinceUtc == null).ThenBy(row => row.PendingSinceUtc).ThenBy(row => row.MemberId)
            : query.ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, row => row.MemberId);
        var page = await sorted.ToPagedResultAsync(request, cancellationToken);
        if (page.Items.Count == 0)
            return new PagedResult<OrganizationUserRow>([], page.Page, page.PageSize, page.TotalCount);

        return new PagedResult<OrganizationUserRow>(await HydrateAsync(tenantId, page.Items, cancellationToken),
            page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<OrganizationUserFilterCountsResponse> GetFilterCountsAsync(
        ListOrganizationUsersRequest request, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(nowUtc);
        var tenantId = tenantContext.RequiredTenantId;
        var counts = await Filtered(tenantId, request with { State = null }, nowUtc).GroupBy(row => row.State)
            .Select(group => new { State = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.State, group => group.Count, cancellationToken);
        int Count(OrganizationUserState state) => counts.GetValueOrDefault(state);
        var companyMembers = Filtered(tenantId, request with { CompanyId = null, WithoutCompany = false }, nowUtc);
        var companyAll = await companyMembers.CountAsync(cancellationToken);
        var withoutCompany = await companyMembers.CountAsync(member => !context.CompanyMemberships.Any(membership =>
            membership.TenantId == tenantId && membership.MemberId == member.MemberId), cancellationToken);
        var memberIds = companyMembers.Select(member => member.MemberId);
        var companies = await context.CompanyMemberships.AsNoTracking().Where(membership => membership.TenantId == tenantId
                && memberIds.Contains(membership.MemberId))
            .GroupBy(membership => membership.CompanyId)
            .Select(group => new { CompanyId = group.Key, Count = group.Select(membership => membership.MemberId).Distinct().Count() })
            .ToDictionaryAsync(row => row.CompanyId, row => row.Count, cancellationToken);
        return new OrganizationUserFilterCountsResponse(counts.Values.Sum(), Count(OrganizationUserState.Active),
            Count(OrganizationUserState.PendingDeletion), Count(OrganizationUserState.Invited),
            Count(OrganizationUserState.Expired), Count(OrganizationUserState.Inactive), companyAll, withoutCompany, companies);
    }

    public async Task<OrganizationUserRow?> FindByIdAsync(Guid memberId, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (memberId == Guid.Empty) throw new ArgumentException("The member id cannot be empty.", nameof(memberId));
        RequireUtc(nowUtc);
        var tenantId = tenantContext.RequiredTenantId;
        var row = await States(Members(tenantId).Where(member => member.Id == memberId), tenantId, nowUtc)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (await HydrateAsync(tenantId, [row], cancellationToken))[0];
    }

    private IQueryable<StateRow> Filtered(Guid tenantId, ListOrganizationUsersRequest request, DateTime nowUtc)
    {
        var members = Members(tenantId);
        if (request.RoleId is { } roleId)
            members = members.Where(member => context.RoleAssignments.Any(assignment =>
                assignment.TenantId == tenantId && assignment.MemberId == member.Id && assignment.RoleId == roleId));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var matching = MemberSearch.MatchingMemberIds(context, tenantId, request.Search);
            members = members.Where(member => matching.Contains(member.Id));
        }

        if (request.CompanyId is { } companyId)
            members = members.Where(member => context.CompanyMemberships.Any(membership =>
                membership.TenantId == tenantId && membership.MemberId == member.Id
                && membership.CompanyId == companyId
                && ((!request.PendingPreview && !request.OnlyCompanyInvitations)
                    || membership.Status == CompanyMembershipStatus.Active)));
        if (request.OnlyCompanyInvitations && request.CompanyId is { } invitationCompanyId)
            // CompanyInvitationPolicy, antes de contar y paginar: únicamente de esta empresa.
            members = members.Where(member => !context.RoleAssignments.Any(assignment =>
                    assignment.TenantId == tenantId && assignment.MemberId == member.Id && assignment.CompanyId == null)
                && !context.CompanyMemberships.Any(membership => membership.TenantId == tenantId
                    && membership.MemberId == member.Id && membership.CompanyId != invitationCompanyId
                    && membership.Status == CompanyMembershipStatus.Active));
        if (request.WithoutCompany)
            members = members.Where(member => !context.CompanyMemberships.Any(membership =>
                membership.TenantId == tenantId && membership.MemberId == member.Id));
        var rows = States(members, tenantId, nowUtc);
        if (request.PendingPreview)
            rows = rows.Where(row => row.State == OrganizationUserState.Expired || row.State == OrganizationUserState.PendingDeletion);
        return request.State is { } state ? rows.Where(row => row.State == state) : rows;
    }

    private IQueryable<Member> Members(Guid tenantId)
    {
        return context.Members.AsNoTracking().Where(member => member.TenantId == tenantId
            && member.Status != MemberStatus.Removed
            && (member.Status != MemberStatus.Invited
                || context.Invitations.Any(invitation => invitation.TenantId == tenantId
                    && invitation.MemberId == member.Id && invitation.Status == InvitationStatus.Pending)));
    }

    // Lee una sola vez lo que decide el estado, el orden y la búsqueda, y le aplica la expresión del estado.
    private IQueryable<StateRow> States(IQueryable<Member> members, Guid tenantId, DateTime nowUtc) => members
        .Select(member => new StateInput
        {
            MemberId = member.Id,
            MemberStatus = member.Status,
            UserStatus = context.Users.Where(user => user.Id == member.UserId)
                .Select(user => (UserStatus?)user.Status).FirstOrDefault(),
            DeletionRequestedAtUtc = context.Users.Where(user => user.Id == member.UserId)
                .Select(user => user.DeletionRequestedAtUtc).FirstOrDefault(),
            PendingInvitationExpiresAtUtc = context.Invitations.Where(invitation => invitation.TenantId == tenantId
                    && invitation.MemberId == member.Id && invitation.Status == InvitationStatus.Pending)
                .OrderByDescending(invitation => invitation.IssuedAtUtc)
                .Select(invitation => (DateTime?)invitation.ExpiresAtUtc).FirstOrDefault(),
            NowUtc = nowUtc,
            // Sin cuenta (una invitación) no hay nombre: queda null y va al final, como antes.
            DisplayName = context.Users.Where(user => user.Id == member.UserId)
                .Select(user => user.DisplayName ?? string.Empty).FirstOrDefault(),
            Email = context.LoginMethods
                    .Where(method => method.UserId == member.UserId && method.Type == LoginMethodType.Email
                        && method.VerifiedAtUtc != null)
                    .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                    .Select(method => method.Value).FirstOrDefault()
                ?? context.Invitations.Where(invitation => invitation.TenantId == tenantId
                        && invitation.MemberId == member.Id && invitation.Status == InvitationStatus.Pending)
                    .OrderByDescending(invitation => invitation.IssuedAtUtc)
                    .Select(invitation => EF.Property<string>(invitation, nameof(Invitation.Destination)))
                    .FirstOrDefault(),
            CreatedAtUtc = member.CreatedAtUtc,
        })
        .Select(WithState);

    private async Task<IReadOnlyList<OrganizationUserRow>> HydrateAsync(Guid tenantId,
        IReadOnlyList<StateRow> page, CancellationToken cancellationToken)
    {
        var memberIds = page.Select(row => row.MemberId).ToArray();
        var projected = await (from member in context.Members.AsNoTracking()
                               join userCandidate in context.Users.AsNoTracking()
                                   on member.UserId equals (Guid?)userCandidate.Id into userGroup
                               from user in userGroup.DefaultIfEmpty()
                               where member.TenantId == tenantId && memberIds.Contains(member.Id)
                               select new UserProjection(member.Id, member.UserId,
                                   user == null ? string.Empty : user.DisplayName ?? string.Empty,
                                   member.UserId == null ? null : context.LoginMethods.AsNoTracking()
                                       .Where(method => method.UserId == member.UserId
                                           && method.Type == LoginMethodType.Email && method.VerifiedAtUtc != null)
                                       .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                                       .Select(method => method.Value).FirstOrDefault(),
                                   member.Version, member.CreatedAtUtc,
                                   user == null ? null : user.DeletionScheduledForUtc,
                                   user == null ? null : user.DeletionRequestedAtUtc))
            .ToDictionaryAsync(row => row.MemberId, cancellationToken);
        var roles = await (from assignment in context.RoleAssignments.AsNoTracking()
                           join role in context.Roles.AsNoTracking()
                               on new { assignment.TenantId, assignment.RoleId }
                               equals new { role.TenantId, RoleId = role.Id }
                           where assignment.TenantId == tenantId && assignment.CompanyId == null
                               && memberIds.Contains(assignment.MemberId)
                           orderby role.Name
                           select new { assignment.MemberId, Role = new OrganizationUserRoleRow(role.Id,
                               role.Name, role.IsSystemRole, role.Scope, role.CompanyId) })
            .ToArrayAsync(cancellationToken);
        var rolesByMember = roles.GroupBy(row => row.MemberId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<OrganizationUserRoleRow>)[.. group.Select(row => row.Role)]);

        var companies = await (from membership in context.CompanyMemberships.AsNoTracking()
                               join company in context.Companies.AsNoTracking()
                                   on new { membership.TenantId, membership.CompanyId }
                                   equals new { company.TenantId, CompanyId = company.Id }
                               where membership.TenantId == tenantId && memberIds.Contains(membership.MemberId)
                               orderby company.Name
                               select new { membership.MemberId, membership.CompanyId, company.Name, membership.Status, CompanyStatus = company.Status })
            .ToArrayAsync(cancellationToken);
        var companyRoles = await (from assignment in context.RoleAssignments.AsNoTracking()
                                  join role in context.Roles.AsNoTracking()
                                      on new { assignment.TenantId, assignment.RoleId }
                                      equals new { role.TenantId, RoleId = role.Id }
                                  where assignment.TenantId == tenantId && assignment.CompanyId != null
                                      && memberIds.Contains(assignment.MemberId)
                                  orderby role.Name
                                  select new { assignment.MemberId, CompanyId = assignment.CompanyId!.Value,
                                      Role = new OrganizationUserRoleRow(role.Id, role.Name, role.IsSystemRole,
                                          role.Scope, role.CompanyId) })
            .ToArrayAsync(cancellationToken);
        var companyRolesByMembership = companyRoles.GroupBy(row => (row.MemberId, row.CompanyId))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<OrganizationUserRoleRow>)[.. group.Select(row => row.Role)]);
        var companiesByMember = companies.GroupBy(row => row.MemberId).ToDictionary(group => group.Key,
            group => (IReadOnlyList<OrganizationUserCompanyRow>)[.. group.Select(row =>
                new OrganizationUserCompanyRow(row.CompanyId, row.Name, row.Status,
                    companyRolesByMembership.GetValueOrDefault((row.MemberId, row.CompanyId), []), row.CompanyStatus)) ]);

        var invitations = await context.Invitations.AsNoTracking()
            .Where(invitation => invitation.TenantId == tenantId && memberIds.Contains(invitation.MemberId)
                && invitation.Status == InvitationStatus.Pending)
            .OrderByDescending(invitation => invitation.IssuedAtUtc)
            .Select(invitation => new { invitation.MemberId, invitation.Id,
                invitation.Destination, invitation.IssuedAtUtc, invitation.ExpiresAtUtc })
            .ToArrayAsync(cancellationToken);
        var invitationByMember = invitations.GroupBy(row => row.MemberId)
            .ToDictionary(group => group.Key, group => group.First());
        var userIds = projected.Values.Where(row => row.UserId is not null).Select(row => row.UserId!.Value).ToArray();
        var managedMethods = await context.LoginMethods.AsNoTracking()
            .Where(method => method.ManagedByTenantId == tenantId && userIds.Contains(method.UserId))
            .Select(method => method.UserId)
            .Distinct().ToArrayAsync(cancellationToken);
        var managedUserIds = managedMethods.ToHashSet();

        return [.. page.Select(state =>
        {
            var row = projected[state.MemberId];
            var invitation = invitationByMember.GetValueOrDefault(row.MemberId);
            var emailText = row.Email ?? invitation?.Destination.DisplayValue;
            var email = emailText is null ? null : Email.Create(emailText).Value;
            return new OrganizationUserRow(row.MemberId, row.UserId, row.DisplayName, email,
                row.UserId is { } userId && managedUserIds.Contains(userId), state.State,
                invitation?.Id, invitation?.IssuedAtUtc, invitation?.ExpiresAtUtc,
                rolesByMember.GetValueOrDefault(row.MemberId, []),
                companiesByMember.GetValueOrDefault(row.MemberId, []), row.Version, row.CreatedAtUtc,
                row.DeletionScheduledForUtc, row.DeletionRequestedAtUtc);
        })];
    }

    private static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(value));
    }

    /// <summary>Lo que decide el estado, el orden y la búsqueda de una persona, leído en SQL.</summary>
    internal sealed class StateInput
    {
        public Guid MemberId { get; init; }
        public MemberStatus MemberStatus { get; init; }
        public UserStatus? UserStatus { get; init; }
        public DateTime? PendingInvitationExpiresAtUtc { get; init; }
        public DateTime? DeletionRequestedAtUtc { get; init; }
        public DateTime NowUtc { get; init; }
        public string? DisplayName { get; init; }
        public string? Email { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }

    /// <summary>Una persona con su estado: lo que se filtra, se ordena y se cuenta.</summary>
    internal sealed class StateRow
    {
        public Guid MemberId { get; init; }
        public OrganizationUserState State { get; init; }
        public string? DisplayName { get; init; }
        public string? Email { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? PendingSinceUtc { get; init; }
    }

    private sealed record UserProjection(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
        uint Version, DateTime CreatedAtUtc, DateTime? DeletionScheduledForUtc, DateTime? DeletionRequestedAtUtc);
}
