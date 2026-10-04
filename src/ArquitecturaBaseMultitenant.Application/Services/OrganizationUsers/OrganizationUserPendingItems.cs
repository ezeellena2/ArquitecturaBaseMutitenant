using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// «Para resolver» de Usuarios (ADR 0040): las invitaciones vencidas, para quien puede invitar, y las bajas pedidas,
/// para quien administra usuarios. Usa los mismos estados que el listado de Usuarios.
/// </summary>
internal sealed class OrganizationUserPendingItems(IOrganizationUserReader users, TimeProvider timeProvider)
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
            items.AddRange((await AllAsync(OrganizationUserState.Expired, nowUtc, cancellationToken))
                .Select(row => new PendingItem(Area, InvitationExpired, row.Email?.DisplayValue, Urgent: false,
                    row.InvitationExpiresAtUtc, DueAtUtc: null, row.MemberId)));
        }
        if (context.Can(Permissions.Users.Manage))
        {
            items.AddRange((await AllAsync(OrganizationUserState.PendingDeletion, nowUtc, cancellationToken))
                .Select(row => new PendingItem(Area, DeletionRequested, row.DisplayName, Urgent: false,
                    row.DeletionRequestedAtUtc, row.DeletionScheduledForUtc, row.MemberId)));
        }
        return items;
    }

    private async Task<IReadOnlyList<OrganizationUserRow>> AllAsync(OrganizationUserState state, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var rows = new List<OrganizationUserRow>();
        for (var page = 1; ; page++)
        {
            var result = await users.ListAsync(new ListOrganizationUsersRequest
            {
                State = state,
                Page = page,
                PageSize = PagedRequest.MaxPageSize,
            }, nowUtc, cancellationToken);
            rows.AddRange(result.Items);
            if (result.Items.Count == 0 || rows.Count >= result.TotalCount) return rows;
        }
    }
}
