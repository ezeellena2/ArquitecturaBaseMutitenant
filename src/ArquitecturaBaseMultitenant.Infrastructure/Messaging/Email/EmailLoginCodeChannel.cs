using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Encola el mensaje renderizado dentro de la transacción del caso de uso.</summary>
internal sealed class EmailLoginCodeChannel(IEmailTemplateRenderer templates, IOutbox outbox) : ILoginCodeChannel
{
    public string Key => OutboxChannel.Email;

    public void Enqueue(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            templates.RenderLoginCode(destination, code, lifetimeMinutes, culture)));

    public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            templates.RenderSignupCode(destination, code, lifetimeMinutes, culture)));
}
