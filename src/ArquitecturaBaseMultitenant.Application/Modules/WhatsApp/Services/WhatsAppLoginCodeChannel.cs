using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppLoginCodeChannel(IWhatsAppChannelSettings settings, WhatsAppSettingsResolver configuration,
    IWhatsAppTemplateRenderer templates, IOutbox outbox) : ILoginCodeChannel
{
    public string Key => "whatsapp";
    public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
    public LoginAuditMethod AuditMethod => LoginAuditMethod.WhatsAppCode;
    public bool IsAvailable => settings.IsEnabled;
    public Task<IReadOnlyList<string>> CountriesAsync(CancellationToken ct) => configuration.AllowedCountriesAsync(ct);
    public string? ContactPhone => settings.ContactPhone;
    public async Task<Result> ValidateNewDestinationAsync(LoginCodeDestination destination, string? country, CancellationToken ct) =>
        settings.ValidateNewDestination(destination, await configuration.AllowedCountriesAsync(ct));
    public Task<string> RenderLoginCodeAsync(string destination, string code, int lifetimeMinutes, CultureProfile culture,
        CancellationToken ct) => Task.FromResult(Render(destination, code, lifetimeMinutes, culture));
    private string Render(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
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
    public Task EnqueueSignupAsync(string destination, string code, int lifetimeMinutes, CultureProfile culture,
        CancellationToken ct)
    {
        if (IsAvailable) EnqueueRenderedLoginCode(Render(destination, code, lifetimeMinutes, culture), lifetimeMinutes);
        return Task.CompletedTask;
    }
    public Task EnqueueVerificationAsync(Guid? userId, string destination, string code, int lifetimeMinutes,
        CultureProfile culture, CancellationToken ct)
    {
        if (IsAvailable) EnqueueRenderedLoginCode(Render(destination, code, lifetimeMinutes, culture), lifetimeMinutes, userId);
        return Task.CompletedTask;
    }
}
