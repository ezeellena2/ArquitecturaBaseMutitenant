using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>Usa datos actuales y cultura/zona explícitas para encolar el canal disponible.</summary>
internal sealed class InvitationDeliveryIssuer(IEnumerable<IInvitationChannel> channels, UserCultures cultures,
    ITenantSettingsReader settings, IUserRepository users, ILoginMethodReader methods, IPublicOrigin origin)
{
    internal bool IsAvailable(string channel) => channels.Any(value => value.Key == channel);

    internal async Task EnqueueAsync(Invitation invitation, TenantRow organization, string token,
        Guid? recipientId, CancellationToken ct)
    {
        var channel = channels.Single(value => value.Key == invitation.Channel);
        var inviter = await users.GetByIdAsync(invitation.InviterUserId, ct)
            ?? throw new InvalidOperationException("The inviter must exist.");
        var tenantSettings = await settings.FindCurrentAsync(ct);
        var profile = await cultures.ResolveAsync(null, tenantSettings?.DefaultCulture ?? inviter.Culture, ct);
        var timeZoneId = tenantSettings?.DefaultTimeZoneId ?? inviter.TimeZoneId;
        var publicOrigin = origin.Value ?? throw new InvalidOperationException("The public origin is required for invitation links.");
        var url = new Uri(publicOrigin, "/invitacion").AbsoluteUri + "#" + token;
        var inviterName = inviter.Status == UserStatus.Deleted
            ? ErrorTexts.Get("Legal.AccountDeletion.DeletedDisplayName") : inviter.DisplayName ?? organization.Name;
        var usesEmailCode = recipientId is { } userId && (await methods.ListByUserIdAsync(userId, ct))
            .Any(method => method.Type == LoginMethodType.Email && method.VerifiedAtUtc is not null
                && method.Value == invitation.Destination.Value);
        await channel.EnqueueAsync(invitation.Destination, new InvitationNotice(organization.Name, inviterName,
            invitation.ExpiresAtUtc, timeZoneId, url, usesEmailCode), profile, recipientId, ct);
    }
}
