using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Notifications;

/// <summary>
/// «Gamma S.A. espera aprobación», en la campana de Plataforma: una por organización pendiente, para quien puede verlas
/// (<c>platform.tenants.read</c>, como «Para resolver» del inicio). Las 50 más nuevas, con la fecha del alta; cada una
/// lleva a su ficha. Una organización sin fecha de alta registrada no se avisa.
/// </summary>
internal sealed class OrganizationPendingNotificationSource(IPlatformReader platform) : INotificationSource
{
    public const string Kind = "organizationPending";

    public async Task<IReadOnlyList<NotificationItem>> ListAsync(NotificationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Access != Access.Platform || !context.Can(PlatformPermissions.Tenants.Read)) return [];
        var page = await platform.ListTenantsAsync(new ListPlatformTenantsRequest
        {
            Status = TenantStatus.PendingApproval,
            PageSize = NotificationListResponse.Limit,
            Sort = "-createdAtUtc",
        }, cancellationToken);
        return
        [
            .. page.Items.Where(tenant => tenant.CreatedAtUtc is not null).Select(tenant => new NotificationItem(
                $"{Kind}:{tenant.Id:N}", Kind, new Dictionary<string, string> { ["name"] = tenant.Name },
                tenant.CreatedAtUtc!.Value, $"{ReturnUrls.PlatformOrganizationsPath}/{tenant.Id:D}", Read: false)),
        ];
    }
}
