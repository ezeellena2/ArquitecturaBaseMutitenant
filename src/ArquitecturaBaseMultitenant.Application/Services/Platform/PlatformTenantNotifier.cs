using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Encola a quien registró la organización y sus Dueños, sin abrir datos privados.</summary>
internal sealed class PlatformTenantNotifier(IPlatformReader reader, IUserRepository users,
    ILoginMethodRepository methods, AccountNoticeIssuer notices)
{
    internal async Task EnqueueAsync(Tenant tenant, string reason, CancellationToken ct)
    {
        foreach (var id in await reader.ListTenantNoticeRecipientsAsync(tenant.Id, ct))
        {
            var user = await users.GetByIdAsync(id, ct);
            if (user is null) continue;
            await notices.EnqueueAsync(id, await methods.ListByUserIdAsync(id, ct),
                new AccountNotice.OrganizationStateChanged(tenant.Name, tenant.Status, reason) { RecipientName = user.DisplayName },
                user.Culture, principalOnly: false, ct);
        }
    }
}
