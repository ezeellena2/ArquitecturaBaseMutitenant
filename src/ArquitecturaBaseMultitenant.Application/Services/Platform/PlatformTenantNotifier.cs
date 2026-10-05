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

    internal Task EnqueueApprovedAsync(Tenant tenant, Guid? stagedOwnerId, CancellationToken ct) =>
        EnqueueAsync(tenant, new AccountNotice.OrganizationApproved(tenant.Name), ct, stagedOwnerId, emailOnly: true);

    internal Task EnqueuePageModerationAsync(Tenant tenant, bool blocked, CancellationToken ct) =>
        EnqueueAsync(tenant, new AccountNotice.PublicPageModerated(tenant.Name, blocked), ct);

    private async Task EnqueueAsync(Tenant tenant, AccountNotice notice, CancellationToken ct,
        Guid? stagedOwnerId = null, bool emailOnly = false)
    {
        var ownerIds = await reader.ListActiveTenantOwnerIdsAsync(tenant.Id, ct);
        // El índice se actualiza con el commit. La aprobación ya preparó este Dueño en la misma UoW.
        foreach (var id in ownerIds.Concat(stagedOwnerId is { } staged ? [staged] : Array.Empty<Guid>()).Distinct())
        {
            var user = await users.GetByIdAsync(id, ct);
            if (user?.Status != UserStatus.Active) continue;
            var currentMethods = await methods.ListByUserIdAsync(id, ct);
            var primary = currentMethods
                .FirstOrDefault(method => method.IsPrimary && CanReceive(method, tenant.Id, emailOnly));
            IReadOnlyList<LoginMethod> recipients;
            if (primary is not null) recipients = [primary];
            else
            {
                // Aprobar conserva los correos propios; rechazo y moderación usan el resolver de salida.
                var candidates = emailOnly ? currentMethods : await availableMethods.ResolveAsync(id, tenant.Id, ct);
                recipients = candidates
                    .Where(method => CanReceive(method, tenant.Id, emailOnly)).ToArray();
            }
            await notices.EnqueueAsync(id, recipients, notice with { RecipientName = user.DisplayName },
                user.Culture, principalOnly: false, ct);
        }
    }

    private bool CanReceive(LoginMethod method, Guid tenantId, bool emailOnly) =>
        (!emailOnly || method.Type is LoginMethodType.Email or LoginMethodType.Google)
        && method.VerifiedAtUtc is not null
        && (method.ManagedByTenantId is null || method.ManagedByTenantId == tenantId)
        && (method.Type != LoginMethodType.Google || method.ContactEmail is not null)
        && channels.Any(channel => channel.MethodType ==
            (method.Type == LoginMethodType.Google ? LoginMethodType.Email : method.Type));
}
