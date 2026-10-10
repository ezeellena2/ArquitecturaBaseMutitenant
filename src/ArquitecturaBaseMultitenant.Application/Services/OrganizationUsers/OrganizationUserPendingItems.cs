using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// «Para resolver» de Usuarios (ADR 0040): las invitaciones vencidas, para quien puede invitar, y las bajas pedidas,
/// para quien administra usuarios. Sin permisos de la organización (Inicio-Miembro), las invitaciones vencidas de cada
/// empresa donde administra los miembros, con la empresa: solo las que puede reenviar desde la ficha, que sin
/// <c>users.invite</c> son las únicamente de esa empresa (<see cref="CompanyInvitationPolicy"/>). Usa los mismos estados
/// que el listado de Usuarios.
/// </summary>
internal sealed class OrganizationUserPendingItems(IOrganizationUserReader users, IPermissionService permissions,
    TimeProvider timeProvider)
    : IPendingItemSource
{
    public const string Area = "users";
    public const string InvitationExpired = "invitationExpired";
    public const string DeletionRequested = "deletionRequested";

    public async Task<IReadOnlyList<PendingItem>> ListAsync(PendingItemContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var items = new List<PendingItem>();
        if (context.Can(Permissions.Users.Invite))
        {
            items.AddRange((await AllAsync(OrganizationUserState.Expired, companyId: null, nowUtc, cancellationToken))
                .Select(row => Expired(row, company: null)));
        }
        foreach (var companyId in context.CompaniesWhere(Permissions.Company.Members.Manage))
        {
            var scope = new CompanyInvitationScope(companyId, context.Can(Permissions.Users.Invite));
            items.AddRange((await AllAsync(OrganizationUserState.Expired, companyId, nowUtc, cancellationToken))
                .Where(row => CompanyInvitationPolicy.CanResolve(scope, row.Roles.Count > 0,
                    [.. row.Companies.Select(company => (company.CompanyId, company.Status))]))
                .SelectMany(row => row.Companies.Where(company => company.CompanyId == companyId).Take(1)
                    .Select(company => Expired(row, company))));
        }
        if (context.Can(Permissions.Users.Manage))
        {
            items.AddRange((await AllAsync(OrganizationUserState.PendingDeletion, companyId: null, nowUtc,
                    cancellationToken))
                .Select(row => new PendingItem(Area, DeletionRequested, row.DisplayName, Urgent: false,
                    row.DeletionRequestedAtUtc, row.DeletionScheduledForUtc, row.MemberId)));
        }
        return items;
    }

    public async Task<bool> CanResolveAsync(PendingItem item, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Area != Area) return false;
        if (item.Kind == DeletionRequested && item.CompanyId is null)
            return await permissions.HasPermissionAsync(userId, Permissions.Users.Manage, cancellationToken);
        if (item.Kind != InvitationExpired) return false;
        if (item.CompanyId is not { } companyId)
            return await permissions.HasPermissionAsync(userId, Permissions.Users.Invite, cancellationToken);
        if (item.TargetId is not { } memberId) return false;
        var row = await users.FindByIdAsync(memberId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (row is not { State: OrganizationUserState.Expired }
            || !await permissions.HasCompanyPermissionAsync(userId, companyId, Permissions.Company.Members.Manage,
                cancellationToken)) return false;
        var canInvite = await permissions.HasPermissionAsync(userId, Permissions.Users.Invite, cancellationToken);
        return CompanyInvitationPolicy.CanResolve(new CompanyInvitationScope(companyId, canInvite), row.Roles.Count > 0,
            [.. row.Companies.Select(company => (company.CompanyId, company.Status))]);
    }

    private static PendingItem Expired(OrganizationUserRow row, OrganizationUserCompanyRow? company) =>
        new(Area, InvitationExpired, row.Email?.DisplayValue, Urgent: false, row.InvitationExpiresAtUtc,
            DueAtUtc: null, row.MemberId, company?.CompanyId, company?.CompanyName);

    private async Task<IReadOnlyList<OrganizationUserRow>> AllAsync(OrganizationUserState state, Guid? companyId,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var rows = new List<OrganizationUserRow>();
        for (var page = 1; ; page++)
        {
            var result = await users.ListAsync(new ListOrganizationUsersRequest
            {
                State = state,
                CompanyId = companyId,
                Page = page,
                PageSize = PagedRequest.MaxPageSize,
            }, nowUtc, cancellationToken);
            rows.AddRange(result.Items);
            if (result.Items.Count == 0 || rows.Count >= result.TotalCount) return rows;
        }
    }
}
