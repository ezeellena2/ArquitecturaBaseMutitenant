using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>«Para resolver» de Configuración (ADR 0040): el dominio de correo sin verificar, para quien puede verificarlo.</summary>
internal sealed class OrganizationDomainPendingItems(ITenantDomainReader domains) : IPendingItemSource
{
    public const string Area = "settings";
    public const string DomainUnverified = "domainUnverified";

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
