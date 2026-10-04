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
    private SortMap<CompanyMembership> Sort => new()
    {
        ["displayName"] = membership => context.Members.AsNoTracking()
            .Where(member => member.TenantId == membership.TenantId && member.Id == membership.MemberId)
            .Select(member => context.Users.AsNoTracking().Where(user => user.Id == member.UserId)
                .Select(user => user.DisplayName ?? string.Empty).FirstOrDefault()).FirstOrDefault(),
        ["email"] = membership => context.Members.AsNoTracking()
            .Where(member => member.TenantId == membership.TenantId && member.Id == membership.MemberId)
            .Select(member => context.LoginMethods.AsNoTracking()
                .Where(method => method.UserId == member.UserId && method.Type == LoginMethodType.Email
                    && method.VerifiedAtUtc != null)
                .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                .Select(method => method.Value).FirstOrDefault()).FirstOrDefault(),
    };

    private static readonly SortDescriptor DefaultSort = new("displayName", Descending: false);

    public async Task<PagedResult<CompanyMemberRow>> ListAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var rows = await Memberships(tenantId, companyId, request, cancellationToken);
        var totalCount = await rows.CountAsync(cancellationToken);
        var ordered = rows.ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort,
            membership => membership.MemberId);
        var page = await ordered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToArrayAsync(cancellationToken);
        if (page.Length == 0)
            return new PagedResult<CompanyMemberRow>([], request.Page, request.PageSize, totalCount);

        var memberIds = page.Select(membership => membership.MemberId).ToArray();
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
        var invitationEmails = await context.Invitations.AsNoTracking()
            .Where(invitation => invitation.TenantId == tenantId && memberIds.Contains(invitation.MemberId)
                && invitation.Status == Domain.Invitations.InvitationStatus.Pending)
            .OrderByDescending(invitation => invitation.IssuedAtUtc)
            .Select(invitation => new { invitation.MemberId, invitation.Destination })
            .ToArrayAsync(cancellationToken);
        var pendingEmails = invitationEmails.GroupBy(item => item.MemberId)
            .ToDictionary(group => group.Key, group => group.First().Destination.DisplayValue);

        var mapped = page.Select(membership => rowsById[membership.MemberId])
            .Select(member => new CompanyMemberRow(member.MemberId, member.UserId,
            member.DisplayName.Length == 0 ? pendingEmails.GetValueOrDefault(member.MemberId, "") : member.DisplayName,
            ToEmail(member.Email ?? pendingEmails.GetValueOrDefault(member.MemberId)), member.MemberStatus,
            member.UserStatus, member.MembershipStatus, rolesByMember.GetValueOrDefault(member.MemberId, []),
            member.Version, member.CreatedAtUtc)).ToArray();
        return new PagedResult<CompanyMemberRow>(mapped, request.Page, request.PageSize, totalCount);
    }

    public async Task<CompanyMemberFilterCountsResponse> GetFilterCountsAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var rows = await Memberships(tenantId, companyId, request with { State = null }, cancellationToken);
        return new CompanyMemberFilterCountsResponse(
            await rows.CountAsync(cancellationToken),
            await WithState(rows, tenantId, CompanyMemberState.Active).CountAsync(cancellationToken),
            await WithState(rows, tenantId, CompanyMemberState.PendingDeletion).CountAsync(cancellationToken),
            await WithState(rows, tenantId, CompanyMemberState.Inactive).CountAsync(cancellationToken),
            await WithState(rows, tenantId, CompanyMemberState.Invited).CountAsync(cancellationToken));
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

    private async Task<IQueryable<CompanyMembership>> Memberships(Guid tenantId, Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        var rows = context.CompanyMemberships.AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membership.CompanyId == companyId);

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
            rows = rows.Where(membership => invitationMatches.Contains(membership.MemberId)
                || context.Members.AsNoTracking().Any(member => member.TenantId == tenantId
                    && member.Id == membership.MemberId
                    && (context.Users.AsNoTracking().Any(user => user.Id == member.UserId
                            && user.DisplayName != null && EF.Functions.ILike(user.DisplayName, pattern, "\\"))
                        || context.LoginMethods.AsNoTracking().Any(method => method.UserId == member.UserId
                            && method.Type == LoginMethodType.Email && method.VerifiedAtUtc != null
                            && EF.Functions.ILike(method.Value, pattern, "\\")))));
        }

        if (request.State is { } state)
            rows = WithState(rows, tenantId, state);

        if (request.RoleId is { } roleId)
            rows = rows.Where(membership => context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                && assignment.CompanyId == companyId && assignment.MemberId == membership.MemberId
                && assignment.RoleId == roleId));
        if (request.WithoutRoles is true)
            rows = rows.Where(membership => !context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                && assignment.CompanyId == companyId && assignment.MemberId == membership.MemberId));
        return rows;
    }

    private IQueryable<CompanyMembership> WithState(IQueryable<CompanyMembership> rows, Guid tenantId,
        CompanyMemberState state) => state switch
    {
        CompanyMemberState.Active => rows.Where(membership => membership.Status == CompanyMembershipStatus.Active
            && context.Members.Any(member => member.TenantId == tenantId && member.Id == membership.MemberId
                && member.Status == MemberStatus.Active
                && context.Users.Any(user => user.Id == member.UserId && user.Status == UserStatus.Active))),
        CompanyMemberState.PendingDeletion => rows.Where(membership => membership.Status == CompanyMembershipStatus.Active
            && context.Members.Any(member => member.TenantId == tenantId && member.Id == membership.MemberId
                && member.Status == MemberStatus.Active
                && context.Users.Any(user => user.Id == member.UserId && user.Status == UserStatus.PendingDeletion))),
        CompanyMemberState.Inactive => rows.Where(membership => membership.Status == CompanyMembershipStatus.Inactive
            || context.Members.Any(member => member.TenantId == tenantId && member.Id == membership.MemberId
                && (member.Status == MemberStatus.Inactive || member.Status == MemberStatus.Removed
                    || context.Users.Any(user => user.Id == member.UserId
                        && (user.Status == UserStatus.Suspended || user.Status == UserStatus.Deleted))))),
        CompanyMemberState.Invited => rows.Where(membership => context.Members.Any(member =>
            member.TenantId == tenantId && member.Id == membership.MemberId && member.Status == MemberStatus.Invited)),
        _ => rows,
    };

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
            member.Status, user == null ? null : user.Status, membership.Status,
            membership.Version, membership.CreatedAtUtc))
        .ToArrayAsync(cancellationToken);

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static Email? ToEmail(string? value) => value is null ? null : Email.Create(value).Value;

    private sealed record MemberProjection(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
        MemberStatus MemberStatus, UserStatus? UserStatus, CompanyMembershipStatus MembershipStatus,
        uint Version, DateTime CreatedAtUtc);
}
