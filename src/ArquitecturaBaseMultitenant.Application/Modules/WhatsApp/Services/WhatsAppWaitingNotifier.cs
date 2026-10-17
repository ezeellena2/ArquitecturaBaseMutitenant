using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Arma y encola el correo «Chats esperando» de un equipo (D6): uno por integrante activo, a su correo principal, en su
/// cultura y zona, con la cantidad y el enlace a la bandeja. Nunca lleva teléfonos ni textos de los chats.
/// </summary>
internal sealed class WhatsAppWaitingNotifier(IWhatsAppTeamReader teams, IUserRepository users, IWhatsAppWaitingEmailRenderer renderer,
    IOutbox outbox, IPublicOrigin origin)
{
    private const string OrganizationInbox = "org/whatsapp/chats";
    private const string PlatformInbox = "plataforma/whatsapp";

    internal async Task EnqueueAsync(WhatsAppTeamRow team, int waiting, bool platform, CancellationToken ct)
    {
        var accounts = platform ? team.MemberIds
            : [.. (await teams.ListActiveMembersAsync(team.MemberIds, ct)).Select(member => member.UserId)];
        var url = origin.Absolute(platform ? PlatformInbox : OrganizationInbox);
        foreach (var userId in accounts.Distinct())
        {
            if (await users.GetByIdAsync(userId, ct) is not { Status: UserStatus.Active, PrimaryEmail: { } email } account) continue;
            var message = await renderer.RenderAsync(email.Value, team.Name, waiting, url, platform, account.Culture, account.TimeZoneId, ct);
            outbox.Enqueue(OutboxChannel.Email, JsonSerializer.Serialize(message), userId);
        }
    }
}
