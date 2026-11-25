using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppLoginCodeChannel(IWhatsAppChannelSettings settings, IWhatsAppTemplateRenderer templates, IOutbox outbox) : ILoginCodeChannel
{
    public string Key => "whatsapp";
    public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
    public LoginAuditMethod AuditMethod => LoginAuditMethod.WhatsAppCode;
    public bool IsAvailable => settings.IsEnabled;
    public IReadOnlyList<string> Countries => settings.Countries;
    public string? ContactPhone => settings.ContactPhone;
    public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => settings.ValidateNewDestination(destination);
    public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        JsonSerializer.Serialize(templates.RenderCode(destination, code, lifetimeMinutes, culture));
    // WhatsApp encola el código sin plazo en la cola: la vigencia solo la usa el correo para descartar un código vencido. Un
    // código contesta a quien lo pidió: sale antes que los avisos, detrás de lo anterior a ese número (ADR 0066).
    public void EnqueueRenderedLoginCode(string payload, int lifetimeMinutes, Guid? userId = null)
    {
        if (!IsAvailable) return;
        var message = JsonSerializer.Deserialize<WhatsAppOutboundMessage>(payload)
            ?? throw new InvalidOperationException("A rendered WhatsApp code is required.");
        outbox.Enqueue(Key, payload, userId, message.QueueOptions(OutboxPriority.Reply));
    }
    public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture)
    {
        if (IsAvailable) EnqueueRenderedLoginCode(RenderLoginCode(destination, code, lifetimeMinutes, culture), lifetimeMinutes);
    }
    public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture)
    {
        if (IsAvailable) EnqueueRenderedLoginCode(RenderLoginCode(destination, code, lifetimeMinutes, culture), lifetimeMinutes, userId);
    }
}
