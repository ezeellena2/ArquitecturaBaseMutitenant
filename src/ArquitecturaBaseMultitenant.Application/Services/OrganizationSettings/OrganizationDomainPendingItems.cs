using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>«Para resolver» de Configuración (ADR 0040): el dominio de correo sin verificar, para quien puede verificarlo.</summary>
internal sealed class OrganizationDomainPendingItems(ITenantDomainReader domains, IPermissionService permissions) : IPendingGroupSource
{
    public const string Area = "settings";
    public const string DomainUnverified = "domainUnverified";

    public async Task<IReadOnlyList<PendingGroup>> ListGroupsAsync(PendingItemContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Can(Permissions.Settings.Manage)) return [];
        var page = await domains.ListPendingAsync(cancellationToken);
        return page.TotalCount == 0 ? [] : [new PendingGroup(Area, DomainUnverified, page.TotalCount,
            page.Items.Select(domain => new PendingItem(Area, DomainUnverified, domain.Domain, Urgent: false,
                domain.CreatedAtUtc, DueAtUtc: null, domain.Id)).ToArray())];
    }

    public Task<IReadOnlyList<PendingGroup>> RevalidateGroupsAsync(IReadOnlyList<PendingGroup> groups,
        PendingItemContext context, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PendingGroup>>(context.Can(Permissions.Settings.Manage)
            ? groups.Where(group => group.Area == Area && group.Kind == DomainUnverified && group.CompanyId is null).ToArray()
            : []);

    public Task<bool> CanResolveAsync(PendingItem item, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Area == Area && item.Kind == DomainUnverified && item.CompanyId is null
            ? permissions.HasPermissionAsync(userId, Permissions.Settings.Manage, cancellationToken)
            : Task.FromResult(false);
    }

    public async Task<IReadOnlyList<PendingItem>> ListAsync(PendingItemContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Can(Permissions.Settings.Manage)) return [];
        return
        [
            .. (await domains.ListCurrentAsync(cancellationToken))
                .Where(domain => domain.Status == TenantDomainStatus.Pending)
                .Select(domain => new PendingItem(Area, DomainUnverified, domain.Domain, Urgent: false,
                    domain.CreatedAtUtc, DueAtUtc: null, domain.Id)),
        ];
    }
}
