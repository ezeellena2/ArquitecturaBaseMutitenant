using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Arma y encola el correo «Chats esperando» de un equipo (D6): uno por integrante activo, a su correo principal, en su
/// cultura y zona, con la cantidad, hace cuánto espera el más viejo, la organización (o la plataforma) y el enlace a la
/// bandeja. Nunca lleva teléfonos ni textos de los chats.
/// </summary>
internal sealed class WhatsAppWaitingNotifier(IWhatsAppTeamReader teams, IUserRepository users, IWhatsAppWaitingEmailRenderer renderer,
    IOutbox outbox, IPublicOrigin origin, ITenantReader tenants, ITenantContext tenant, IWhatsAppChannelSettings settings)
{
    private const string OrganizationInbox = "org/whatsapp/chats";
    private const string PlatformInbox = "plataforma/whatsapp";

    internal async Task EnqueueAsync(WhatsAppTeamRow team, int waiting, TimeSpan oldestWaiting, bool platform, CancellationToken ct)
    {
        var accounts = platform ? team.MemberIds
            : [.. (await teams.ListActiveMembersAsync(team.MemberIds, ct)).Select(member => member.UserId)];
        var url = origin.Absolute(platform ? PlatformInbox : OrganizationInbox);
        // Las consultas de Plataforma son de la plataforma, que se nombra como la app.
        var place = platform ? settings.AppName
            : (await tenants.FindByIdAsync(tenant.RequiredTenantId, ct))?.Name
                ?? throw new InvalidOperationException("The organization of the waiting notice is missing.");
        foreach (var userId in accounts.Distinct())
        {
            if (await users.GetByIdAsync(userId, ct) is not { Status: UserStatus.Active, PrimaryEmail: { } email } account) continue;
            var message = await renderer.RenderAsync(new WhatsAppWaitingNotice(email.Value, account.DisplayName, team.Name, waiting,
                oldestWaiting, place, url, platform, account.Culture, account.TimeZoneId), ct);
            outbox.Enqueue(OutboxChannel.Email, JsonSerializer.Serialize(message), userId);
        }
    }
}
