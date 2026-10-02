using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Pagina miembros de empresa en SQL y proyecta roles/correo sin materializar identidades completas.</summary>
internal sealed class CompanyMembershipReader(ApplicationDbContext context, ITenantContext tenantContext)
    : ICompanyMembershipReader
{
    private static readonly SortMap<MemberProjection> Sort = new()
    {
        ["displayName"] = member => member.DisplayName,
        ["email"] = member => member.Email,
    };

    private static readonly SortDescriptor DefaultSort = new("displayName", Descending: false);

    public async Task<PagedResult<CompanyMemberRow>> ListAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var rows = Members(tenantId, companyId, request);
        rows = rows.ApplySort(SortDescriptor.Parse(request.Sort), Sort, DefaultSort, member => member.MemberId);
        var page = await rows.ToPagedResultAsync(request, cancellationToken);
        if (page.Items.Count == 0)
            return new PagedResult<CompanyMemberRow>([], page.Page, page.PageSize, page.TotalCount);

        var memberIds = page.Items.Select(member => member.MemberId).ToArray();
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

        var mapped = page.Items.Select(member => new CompanyMemberRow(member.MemberId, member.UserId,
            member.DisplayName.Length == 0 ? pendingEmails.GetValueOrDefault(member.MemberId, "") : member.DisplayName,
            member.Email ?? pendingEmails.GetValueOrDefault(member.MemberId), member.MemberStatus,
            member.UserStatus, member.MembershipStatus, rolesByMember.GetValueOrDefault(member.MemberId, []),
            member.Version, member.CreatedAtUtc)).ToArray();
        return new PagedResult<CompanyMemberRow>(mapped, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<CompanyMemberFilterCountsResponse> GetFilterCountsAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.RequiredTenantId;
        var rows = Members(tenantId, companyId, request with { State = null });
        return new CompanyMemberFilterCountsResponse(
            await rows.CountAsync(cancellationToken),
            await rows.CountAsync(row => row.MembershipStatus == CompanyMembershipStatus.Active
                && row.MemberStatus == MemberStatus.Active && row.UserStatus == UserStatus.Active, cancellationToken),
            await rows.CountAsync(row => row.MembershipStatus == CompanyMembershipStatus.Active
                && row.MemberStatus == MemberStatus.Active && row.UserStatus == UserStatus.PendingDeletion,
                cancellationToken),
            await rows.CountAsync(row => row.MembershipStatus == CompanyMembershipStatus.Inactive
                || row.MemberStatus == MemberStatus.Inactive || row.MemberStatus == MemberStatus.Removed
                || row.UserStatus == UserStatus.Suspended || row.UserStatus == UserStatus.Deleted,
                cancellationToken),
            await rows.CountAsync(row => row.MemberStatus == MemberStatus.Invited, cancellationToken));
    }

    private IQueryable<MemberProjection> Members(Guid tenantId, Guid companyId,
        ListCompanyMembersRequest request)
    {
        var rows = from membership in context.CompanyMemberships.AsNoTracking()
                   join member in context.Members.AsNoTracking()
                       on new { membership.TenantId, MemberId = membership.MemberId }
                       equals new { member.TenantId, MemberId = member.Id }
                   join userCandidate in context.Users.AsNoTracking()
                       on member.UserId equals (Guid?)userCandidate.Id into userGroup
                   from user in userGroup.DefaultIfEmpty()
                   where membership.TenantId == tenantId && membership.CompanyId == companyId
                   select new MemberProjection(membership.MemberId, member.UserId,
                       user == null ? string.Empty : user.DisplayName ?? string.Empty,
                       member.UserId == null ? null : context.LoginMethods.AsNoTracking()
                           .Where(method => method.UserId == member.UserId && method.Type == LoginMethodType.Email
                               && method.VerifiedAtUtc != null)
                           .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.Id)
                           .Select(method => method.Value).FirstOrDefault(),
                       member.Status, user == null ? null : user.Status, membership.Status,
                        membership.Version, membership.CreatedAtUtc);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            rows = rows.Where(member => EF.Functions.ILike(member.DisplayName, pattern)
                || member.Email != null && EF.Functions.ILike(member.Email, pattern));
        }

        if (request.State is { } state)
        {
            rows = state switch
            {
                CompanyMemberState.Active => rows.Where(member => member.MembershipStatus == CompanyMembershipStatus.Active
                    && member.MemberStatus == MemberStatus.Active && member.UserStatus == UserStatus.Active),
                CompanyMemberState.PendingDeletion => rows.Where(member =>
                    member.MembershipStatus == CompanyMembershipStatus.Active
                    && member.MemberStatus == MemberStatus.Active && member.UserStatus == UserStatus.PendingDeletion),
                CompanyMemberState.Inactive => rows.Where(member => member.MembershipStatus == CompanyMembershipStatus.Inactive
                    || member.MemberStatus == MemberStatus.Inactive || member.MemberStatus == MemberStatus.Removed
                    || member.UserStatus == UserStatus.Suspended || member.UserStatus == UserStatus.Deleted),
                CompanyMemberState.Invited => rows.Where(member => member.MemberStatus == MemberStatus.Invited),
                _ => rows,
            };
        }

        if (request.RoleId is { } roleId)
            rows = rows.Where(member => context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                && assignment.CompanyId == companyId && assignment.MemberId == member.MemberId
                && assignment.RoleId == roleId));
        if (request.WithoutRoles is true)
            rows = rows.Where(member => !context.RoleAssignments.Any(assignment => assignment.TenantId == tenantId
                && assignment.CompanyId == companyId && assignment.MemberId == member.MemberId));
        return rows;
    }

    private sealed record MemberProjection(Guid MemberId, Guid? UserId, string DisplayName, string? Email,
        MemberStatus MemberStatus, UserStatus? UserStatus, CompanyMembershipStatus MembershipStatus,
        uint Version, DateTime CreatedAtUtc);
}
