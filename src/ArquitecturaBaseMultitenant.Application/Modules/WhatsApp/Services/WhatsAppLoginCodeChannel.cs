using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppLoginCodeChannel(IWhatsAppChannelSettings settings, IWhatsAppTemplateRenderer templates, IOutbox outbox) : ILoginCodeChannel
{
    public string Key => "whatsapp";
    public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
    public bool IsAvailable => settings.IsEnabled;
    public IReadOnlyList<string> Countries => settings.Countries;
    public string? ContactPhone => settings.ContactPhone;
    public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => settings.ValidateNewDestination(destination);
    public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        JsonSerializer.Serialize(templates.RenderCode(destination, code, lifetimeMinutes, culture));
    public void EnqueueRenderedLoginCode(string payload, Guid? userId = null)
    {
        if (IsAvailable) outbox.Enqueue(Key, payload, userId);
    }
    public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture)
    {
        if (IsAvailable) EnqueueRenderedLoginCode(RenderLoginCode(destination, code, lifetimeMinutes, culture));
    }
    public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture)
    {
        if (IsAvailable) EnqueueRenderedLoginCode(RenderLoginCode(destination, code, lifetimeMinutes, culture), userId);
    }
}
