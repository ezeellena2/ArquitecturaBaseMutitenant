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
    : IPendingGroupSource
{
    public const string Area = "users";
    public const string InvitationExpired = "invitationExpired";
    public const string DeletionRequested = "deletionRequested";

    public async Task<IReadOnlyList<PendingGroup>> ListGroupsAsync(PendingItemContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var groups = new List<PendingGroup>();
        if (context.Can(Permissions.Users.Invite))
            groups.Add(await GroupAsync(OrganizationUserState.Expired, null, context, cancellationToken));
        foreach (var companyId in context.CompaniesWhere(Permissions.Company.Members.Manage))
            groups.Add(await GroupAsync(OrganizationUserState.Expired, companyId, context, cancellationToken));
        if (context.Can(Permissions.Users.Manage))
            groups.Add(await GroupAsync(OrganizationUserState.PendingDeletion, null, context, cancellationToken));
        return groups.Where(group => group.TotalCount > 0).ToArray();
    }

    public async Task<IReadOnlyList<PendingGroup>> RevalidateGroupsAsync(IReadOnlyList<PendingGroup> groups,
        PendingItemContext context, CancellationToken cancellationToken)
    {
        var allowed = new List<PendingGroup>();
        var companies = context.CompaniesWhere(Permissions.Company.Members.Manage);
        foreach (var group in groups)
        {
            if (group.Area != Area) continue;
            if (group.CompanyId is { } companyId)
            {
                if (group.Kind == InvitationExpired && companies.Contains(companyId))
                    allowed.Add(await GroupAsync(OrganizationUserState.Expired, companyId, context, cancellationToken));
            }
            else if ((group.Kind == InvitationExpired && context.Can(Permissions.Users.Invite))
                || (group.Kind == DeletionRequested && context.Can(Permissions.Users.Manage))) allowed.Add(group);
        }
        return allowed.Where(group => group.TotalCount > 0).ToArray();
    }

    private async Task<PendingGroup> GroupAsync(OrganizationUserState state, Guid? companyId,
        PendingItemContext context, CancellationToken cancellationToken)
    {
        var page = await users.ListPendingAsync(new ListOrganizationUsersRequest
        {
            State = state,
            CompanyId = companyId,
            OnlyCompanyInvitations = companyId is not null && !context.Can(Permissions.Users.Invite),
        }, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        // SQL aplica la política antes del conteo y límite; la hidratación puede ver un cambio posterior de roles/empresas.
        var eligible = page.Items.Where(row => companyId is not { } id
            || CompanyInvitationPolicy.CanResolve(new CompanyInvitationScope(id, context.Can(Permissions.Users.Invite)),
                row.Roles.Count > 0, [.. row.Companies.Select(company => (company.CompanyId, company.Status))]));
        var items = eligible.Select(row => state == OrganizationUserState.Expired
            ? Expired(row, companyId is { } id ? row.Companies.Single(company => company.CompanyId == id) : null)
            : new PendingItem(Area, DeletionRequested, row.DisplayName, Urgent: false,
                row.DeletionRequestedAtUtc, row.DeletionScheduledForUtc, row.MemberId)).ToArray();
        return new PendingGroup(Area, state == OrganizationUserState.Expired ? InvitationExpired : DeletionRequested,
            page.TotalCount, items, companyId, items.FirstOrDefault()?.CompanyName);
    }

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
