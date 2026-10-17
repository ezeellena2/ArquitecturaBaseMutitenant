using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
/// <summary>
/// El bot de ingreso de la Etapa 8 en el número de Plataforma, en la UoW preparada por T06. Con las consultas prendidas
/// lo reemplaza el bot de atención, que manda este mismo enlace desde la opción del sistema «Entrar a mi cuenta».
/// </summary>
internal sealed class WhatsAppBotWriter(IWhatsAppChannelSettings settings, WhatsAppSignInLinkIssuer signIn,
    TimeProvider clock) : IWhatsAppInboundConsumer
{
    public async Task ConsumeAsync(WhatsAppInboundContext context, CancellationToken ct)
    {
        // Los números de las organizaciones los atiende su propio escritor; las consultas, el de Plataforma.
        if (context.Channel is { OrganizationId: not null } or { ConsultationsEnabled: true }) return;
        if (!settings.IsAttending || settings.PhoneNumberId is null || context.Contact is not { } contact
            || context.Message.Kind is not (WhatsAppMessageKind.Text or WhatsAppMessageKind.Interactive)
            || context.Message.Direction != WhatsAppMessageDirection.Inbound) return;
        var expiresAtUtc = contact.LastInboundAtUtc?.AddHours(24);
        if (expiresAtUtc is null || clock.GetUtcNow().UtcDateTime >= expiresAtUtc) return;
        await signIn.IssueAsync(context, contact.WaId, expiresAtUtc.Value, consultations: false, ct);
    }
}
