using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
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
    private SortMap<Member> Sort => new()
    {
        ["displayName"] = member => context.Users.AsNoTracking()
            .Where(user => user.Id == member.UserId)
            .Select(user => user.DisplayName ?? string.Empty).FirstOrDefault(),
        ["email"] = member => context.LoginMethods.AsNoTracking()
            .Where(method => method.UserId == member.UserId && method.Type == LoginMethodType.Email
                && method.VerifiedAtUtc != null)
            .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
            .Select(method => method.Value).FirstOrDefault()
            ?? context.Invitations.AsNoTracking()
                .Where(invitation => invitation.TenantId == member.TenantId && invitation.MemberId == member.Id
                    && invitation.Status == InvitationStatus.Pending)
                .OrderByDescending(invitation => invitation.IssuedAtUtc)
                .Select(invitation => invitation.Destination.Value).FirstOrDefault(),
        ["createdAtUtc"] = member => member.CreatedAtUtc,
    };

    private static readonly SortDescriptor DefaultSort = new("displayName", Descending: false);

    public async Task<PagedResult<OrganizationUserRow>> ListAsync(ListOrganizationUsersRequest request,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(nowUtc);
        var tenantId = tenantContext.RequiredTenantId;
        var rows = await Filtered(tenantId, request, nowUtc, cancellationToken);
        var ordered = rows.ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort,
            member => member.Id);
        var page = await ordered.ToPagedResultAsync(request, cancellationToken);
        if (page.Items.Count == 0)
            return new PagedResult<OrganizationUserRow>([], page.Page, page.PageSize, page.TotalCount);

        var projected = await ProjectAsync(tenantId, page.Items, cancellationToken);
        return new PagedResult<OrganizationUserRow>(await HydrateAsync(tenantId, projected, cancellationToken),
            page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<OrganizationUserFilterCountsResponse> GetFilterCountsAsync(
        ListOrganizationUsersRequest request, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireUtc(nowUtc);
        var tenantId = tenantContext.RequiredTenantId;
        var rows = await Filtered(tenantId, request with { State = null }, nowUtc, cancellationToken);
        return new OrganizationUserFilterCountsResponse(
            await rows.CountAsync(cancellationToken),
            await WithState(rows, OrganizationUserState.Active, tenantId, nowUtc).CountAsync(cancellationToken),
            await WithState(rows, OrganizationUserState.PendingDeletion, tenantId, nowUtc).CountAsync(cancellationToken),
            await WithState(rows, OrganizationUserState.Invited, tenantId, nowUtc).CountAsync(cancellationToken),
            await WithState(rows, OrganizationUserState.Expired, tenantId, nowUtc).CountAsync(cancellationToken),
            await WithState(rows, OrganizationUserState.Inactive, tenantId, nowUtc).CountAsync(cancellationToken));
    }

    public async Task<OrganizationUserRow?> FindByIdAsync(Guid memberId, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (memberId == Guid.Empty) throw new ArgumentException("The member id cannot be empty.", nameof(memberId));
        RequireUtc(nowUtc);
        var tenantId = tenantContext.RequiredTenantId;
        var row = await Members(tenantId).SingleOrDefaultAsync(member => member.Id == memberId,
            cancellationToken);
        if (row is null || row.Status == MemberStatus.Invited
            && !await context.Invitations.AsNoTracking().AnyAsync(invitation => invitation.TenantId == tenantId
                && invitation.MemberId == memberId && invitation.Status == InvitationStatus.Pending,
                cancellationToken))
            return null;
        var projected = await ProjectAsync(tenantId, [row], cancellationToken);
        return (await HydrateAsync(tenantId, projected, cancellationToken))[0];
    }

    private async Task<IQueryable<Member>> Filtered(Guid tenantId, ListOrganizationUsersRequest request,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var rows = Members(tenantId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            var invitationMatches = await context.Database.SqlQuery<Guid>($"""
                    SELECT DISTINCT "MemberId" AS "Value"
                    FROM tenant."Invitations"
                    WHERE "TenantId" = {tenantId}
                      AND "Status" = 'Pending'
                      AND "Destination" ILIKE {pattern} ESCAPE '\'
                    """).ToArrayAsync(cancellationToken);
            rows = rows.Where(member => context.Users.AsNoTracking().Any(user => user.Id == member.UserId
                    && user.DisplayName != null && EF.Functions.ILike(user.DisplayName, pattern, "\\"))
                || context.LoginMethods.AsNoTracking().Any(method => method.UserId == member.UserId
                    && method.Type == LoginMethodType.Email && method.VerifiedAtUtc != null
                    && EF.Functions.ILike(method.Value, pattern, "\\"))
                || invitationMatches.Contains(member.Id));
        }

        if (request.CompanyId is { } companyId)
            rows = rows.Where(member => context.CompanyMemberships.Any(membership => membership.TenantId == tenantId
                && membership.MemberId == member.Id && membership.CompanyId == companyId));
        if (request.WithoutCompany)
            rows = rows.Where(member => !context.CompanyMemberships.Any(membership => membership.TenantId == tenantId
                && membership.MemberId == member.Id));
        if (request.State is { } state) rows = WithState(rows, state, tenantId, nowUtc);
        return rows;
    }

    private IQueryable<Member> Members(Guid tenantId)
    {
        return context.Members.AsNoTracking().Where(member => member.TenantId == tenantId
            && member.Status != MemberStatus.Removed
            && (member.Status != MemberStatus.Invited
                || context.Invitations.Any(invitation => invitation.TenantId == tenantId
                    && invitation.MemberId == member.Id && invitation.Status == InvitationStatus.Pending)));
    }

    private IQueryable<Member> WithState(IQueryable<Member> rows, OrganizationUserState state,
        Guid tenantId, DateTime nowUtc) => state switch
    {
        OrganizationUserState.Active => rows.Where(member => member.Status == MemberStatus.Active
            && context.Users.Any(user => user.Id == member.UserId && user.Status == UserStatus.Active)),
        OrganizationUserState.PendingDeletion => rows.Where(member => member.Status == MemberStatus.Active
            && context.Users.Any(user => user.Id == member.UserId && user.Status == UserStatus.PendingDeletion)),
        OrganizationUserState.Invited => rows.Where(member => member.Status == MemberStatus.Invited
            && context.Invitations.Any(invitation => invitation.TenantId == tenantId
                && invitation.MemberId == member.Id && invitation.Status == InvitationStatus.Pending
                && invitation.ExpiresAtUtc > nowUtc)),
        OrganizationUserState.Expired => rows.Where(member => member.Status == MemberStatus.Invited
            && context.Invitations.Any(invitation => invitation.TenantId == tenantId
                && invitation.MemberId == member.Id && invitation.Status == InvitationStatus.Pending
                && invitation.ExpiresAtUtc <= nowUtc)),
        OrganizationUserState.Inactive => rows.Where(member => member.Status != MemberStatus.Invited
            && (member.Status != MemberStatus.Active || !context.Users.Any(user => user.Id == member.UserId
                && (user.Status == UserStatus.Active || user.Status == UserStatus.PendingDeletion)))),
        _ => rows,
    };

    private async Task<IReadOnlyList<UserProjection>> ProjectAsync(Guid tenantId,
        IReadOnlyCollection<Member> members, CancellationToken cancellationToken)
    {
        var memberIds = members.Select(member => member.Id).ToArray();
        var rows = await (from member in context.Members.AsNoTracking()
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
                              user == null ? null : user.Status, member.Status, member.Version, member.CreatedAtUtc,
                              user == null ? null : user.DeletionScheduledForUtc))
            .ToArrayAsync(cancellationToken);
        var byId = rows.ToDictionary(row => row.MemberId);
        return members.Select(member => byId[member.Id]).ToArray();
    }

    private async Task<IReadOnlyList<OrganizationUserRow>> HydrateAsync(Guid tenantId,
        IReadOnlyList<UserProjection> page, CancellationToken cancellationToken)
    {
        var memberIds = page.Select(row => row.MemberId).ToArray();
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
                               select new { membership.MemberId, membership.CompanyId, company.Name, membership.Status })
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
                    companyRolesByMembership.GetValueOrDefault((row.MemberId, row.CompanyId), []))) ]);

        var invitations = await context.Invitations.AsNoTracking()
            .Where(invitation => invitation.TenantId == tenantId && memberIds.Contains(invitation.MemberId)
                && invitation.Status == InvitationStatus.Pending)
            .OrderByDescending(invitation => invitation.IssuedAtUtc)
            .Select(invitation => new { invitation.MemberId, invitation.Id, invitation.Status,
                invitation.Destination, invitation.IssuedAtUtc, invitation.ExpiresAtUtc })
            .ToArrayAsync(cancellationToken);
        var invitationByMember = invitations.GroupBy(row => row.MemberId)
            .ToDictionary(group => group.Key, group => group.First());
        var userIds = page.Where(row => row.UserId is not null).Select(row => row.UserId!.Value).ToArray();
        var managedMethods = await context.LoginMethods.AsNoTracking()
            .Where(method => method.ManagedByTenantId == tenantId && userIds.Contains(method.UserId))
            .Select(method => method.UserId)
            .Distinct().ToArrayAsync(cancellationToken);
        var managedUserIds = managedMethods.ToHashSet();

        return [.. page.Select(row =>
        {
            var invitation = invitationByMember.GetValueOrDefault(row.MemberId);
            var emailText = row.Email ?? invitation?.Destination.DisplayValue;
            var email = emailText is null ? null : Email.Create(emailText).Value;
            return new OrganizationUserRow(row.MemberId, row.UserId, row.DisplayName, email,
                row.UserId is { } userId && managedUserIds.Contains(userId), row.MemberStatus, row.UserStatus,
                invitation?.Status, invitation?.Id, invitation?.IssuedAtUtc, invitation?.ExpiresAtUtc,
                rolesByMember.GetValueOrDefault(row.MemberId, []),
                companiesByMember.GetValueOrDefault(row.MemberId, []), row.Version, row.CreatedAtUtc,
                row.DeletionScheduledForUtc);
        })];
    }

    private static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(value));
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private sealed record UserProjection(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
        UserStatus? UserStatus, MemberStatus MemberStatus, uint Version, DateTime CreatedAtUtc,
        DateTime? DeletionScheduledForUtc);
}
