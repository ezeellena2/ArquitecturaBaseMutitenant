using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Encola el mensaje renderizado dentro de la transacción del caso de uso.</summary>
internal sealed class EmailLoginCodeChannel(IEmailTemplateRenderer templates, IOutbox outbox) : ILoginCodeChannel
{
    public string Key => OutboxChannel.Email;

    public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        JsonSerializer.Serialize(templates.RenderLoginCode(destination, code, lifetimeMinutes, culture));

    public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => outbox.Enqueue(Key, payload, userId);

    public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            templates.RenderSignupCode(destination, code, lifetimeMinutes, culture)));

    public void EnqueueVerification(Guid userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            templates.RenderVerifyEmailCode(destination, code, lifetimeMinutes, culture)), userId);
}
