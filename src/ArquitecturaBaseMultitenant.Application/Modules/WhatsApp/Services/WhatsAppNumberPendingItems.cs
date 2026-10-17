using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>«Para resolver» de WhatsApp (ADR 0040): los números sin medio de pago, para quien administra WhatsApp con el módulo prendido.</summary>
internal sealed class WhatsAppNumberPendingItems(IWhatsAppChannelReader numbers, IFeatureService features, IPermissionService permissions)
    : IPendingGroupSource
{
    public const string Area = "whatsapp";
    public const string PaymentMissing = "numberPaymentMissing";

    public async Task<IReadOnlyList<PendingGroup>> ListGroupsAsync(PendingItemContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Can(Permissions.WhatsApp.Manage) || !await features.IsEnabledAsync(WhatsAppFeature.Key, cancellationToken)) return [];
        var total = await numbers.CountOrganizationAsync(WhatsAppNumberStatus.PaymentMissing, cancellationToken);
        if (total == 0) return [];
        var rows = await numbers.ListPaymentMissingAsync(PendingGroup.PreviewLimit, cancellationToken);
        return [new PendingGroup(Area, PaymentMissing, total, rows.Select(Item).ToArray())];
    }

    public async Task<IReadOnlyList<PendingGroup>> RevalidateGroupsAsync(IReadOnlyList<PendingGroup> groups, PendingItemContext context,
        CancellationToken cancellationToken) =>
        context.Can(Permissions.WhatsApp.Manage) && await features.IsEnabledAsync(WhatsAppFeature.Key, cancellationToken)
            ? groups.Where(group => group.Area == Area && group.Kind == PaymentMissing && group.CompanyId is null).ToArray()
            : [];

    public async Task<bool> CanResolveAsync(PendingItem item, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Area == Area && item.Kind == PaymentMissing && item.CompanyId is null
            && await features.IsEnabledAsync(WhatsAppFeature.Key, cancellationToken)
            && await permissions.HasPermissionAsync(userId, Permissions.WhatsApp.Manage, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingItem>> ListAsync(PendingItemContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Can(Permissions.WhatsApp.Manage) || !await features.IsEnabledAsync(WhatsAppFeature.Key, cancellationToken)) return [];
        var total = await numbers.CountOrganizationAsync(WhatsAppNumberStatus.PaymentMissing, cancellationToken);
        return [.. (await numbers.ListPaymentMissingAsync(total, cancellationToken)).Select(Item)];
    }

    private static PendingItem Item(WhatsAppNumberPendingRow row) =>
        new(Area, PaymentMissing, row.DisplayNumber, Urgent: true, row.SinceUtc, DueAtUtc: null, row.Id);
}
