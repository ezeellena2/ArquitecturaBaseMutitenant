using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Encola los avisos previstos a Dueños activos, usando contactos disponibles y seguros.</summary>
internal sealed class PlatformTenantNotifier(IPlatformReader reader, IUserRepository users,
    ILoginMethodRepository methods, AccountNoticeIssuer notices,
    OrganizationAvailableLoginMethodsResolver availableMethods, IEnumerable<IAccountNoticeChannel> channels)
{
    internal Task EnqueueRejectedAsync(Tenant tenant, CancellationToken ct) =>
        EnqueueAsync(tenant, new AccountNotice.OrganizationRejected(tenant.Name), ct);

    internal Task EnqueuePageModerationAsync(Tenant tenant, bool blocked, CancellationToken ct) =>
        EnqueueAsync(tenant, new AccountNotice.PublicPageModerated(tenant.Name, blocked), ct);

    private async Task EnqueueAsync(Tenant tenant, AccountNotice notice, CancellationToken ct)
    {
        foreach (var id in await reader.ListActiveTenantOwnerIdsAsync(tenant.Id, ct))
        {
            var user = await users.GetByIdAsync(id, ct);
            if (user?.Status != UserStatus.Active) continue;
            var primary = (await methods.ListByUserIdAsync(id, ct))
                .FirstOrDefault(method => method.IsPrimary && CanReceive(method, tenant.Id));
            IReadOnlyList<LoginMethod> recipients = primary is not null ? [primary] :
                (await availableMethods.ResolveAsync(id, tenant.Id, ct))
                    .Where(method => CanReceive(method, tenant.Id)).ToArray();
            await notices.EnqueueAsync(id, recipients, notice with { RecipientName = user.DisplayName },
                user.Culture, principalOnly: false, ct);
        }
    }

    private bool CanReceive(LoginMethod method, Guid tenantId) =>
        method.VerifiedAtUtc is not null
        && (method.ManagedByTenantId is null || method.ManagedByTenantId == tenantId)
        && (method.Type != LoginMethodType.Google || method.ContactEmail is not null)
        && channels.Any(channel => channel.MethodType ==
            (method.Type == LoginMethodType.Google ? LoginMethodType.Email : method.Type));
}
