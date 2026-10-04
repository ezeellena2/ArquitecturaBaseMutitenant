using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Encola, dentro de la UoW del llamador, el aviso «Invitación como operador» del lienzo v90: quién invita, los
/// permisos y el vencimiento, en la cultura y la zona de la cuenta invitada, con el botón para entrar a Plataforma.
/// Sale solo por correo (ADR 0042).
/// </summary>
internal sealed class PlatformOperatorInvitationNotifier(IEnumerable<IAccountNoticeChannel> channels,
    UserCultureResolver cultures, IPublicOrigin origin, IUserRepository users)
{
    internal async Task EnqueueAsync(PlatformOperatorInvitee invitee, Guid inviterId, CancellationToken ct)
    {
        var inviter = await users.GetByIdAsync(inviterId, ct)
            ?? throw new InvalidOperationException("The inviting operator must exist.");
        var url = origin.Value is { } value ? new Uri(value, "/plataforma").AbsoluteUri : "/plataforma";
        var channel = channels.FirstOrDefault(item => item.MethodType == LoginMethodType.Email)
            ?? throw new InvalidOperationException("The email notice channel is not registered.");
        await channel.EnqueueAsync(invitee.Id, invitee.Email.Value,
            new AccountNotice.OperatorInvited(inviter.DisplayName, invitee.Permissions, invitee.ExpiresAtUtc,
                invitee.TimeZoneId, url),
            await cultures.ResolveAsync(invitee.Culture, null, ct), ct);
    }
}
