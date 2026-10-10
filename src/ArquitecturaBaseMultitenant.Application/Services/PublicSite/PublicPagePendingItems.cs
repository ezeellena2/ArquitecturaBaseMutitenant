using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Authorization;

namespace ArquitecturaBaseMultitenant.Application.Services.PublicSite;

/// <summary>
/// «Para resolver» de la Página pública (ADR 0040): en borrador, o despublicada por la plataforma, que es urgente.
/// Solo para quien administra la página.
/// </summary>
internal sealed class PublicPagePendingItems(IPublicPageReader pages, IPermissionService permissions) : IPendingItemSource
{
    public const string Area = "publicPage";
    public const string Draft = "publicPageDraft";
    public const string Blocked = "publicPageBlocked";

    public Task<bool> CanResolveAsync(PendingItem item, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Area == Area && item.Kind is Draft or Blocked && item.CompanyId is null
            ? permissions.HasPermissionAsync(userId, Permissions.PublicPage.Manage, cancellationToken)
            : Task.FromResult(false);
    }

    public async Task<IReadOnlyList<PendingItem>> ListAsync(PendingItemContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Can(Permissions.PublicPage.Manage)) return [];
        var page = await pages.FindCurrentAsync(cancellationToken);
        if (page?.PublishBlockedAtUtc is { } blockedAtUtc)
            return [new PendingItem(Area, Blocked, Subject: null, Urgent: true, blockedAtUtc, DueAtUtc: null, TargetId: null)];
        return page is { IsPublished: true }
            ? []
            : [new PendingItem(Area, Draft, Subject: null, Urgent: false, SinceUtc: null, DueAtUtc: null, TargetId: null)];
    }
}
