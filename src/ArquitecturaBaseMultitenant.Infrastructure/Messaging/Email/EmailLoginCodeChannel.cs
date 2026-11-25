using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>
/// Encola el mensaje renderizado dentro de la transacción del caso de uso. Un código que vence en la cola no se manda: lleva
/// <see cref="OutboxEnqueueOptions.DiscardAfterUtc"/> en su vigencia (O3).
/// </summary>
internal sealed class EmailLoginCodeChannel(IEmailTemplateRenderer templates, IOutbox outbox, TimeProvider clock) : ILoginCodeChannel
{
    public string Key => OutboxChannel.Email;
    public LoginMethodType SupportedMethodType => LoginMethodType.Email;
    public LoginAuditMethod AuditMethod => LoginAuditMethod.Code;
    public bool IsAvailable => true;
    public IReadOnlyList<string> Countries => [];
    public string? ContactPhone => null;
    public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => destination.Channel == Key
        ? Result.Success() : Result.Failure(LoginChannelErrors.Unavailable);

    public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        JsonSerializer.Serialize(templates.RenderLoginCode(destination, code, lifetimeMinutes, culture));

    public void EnqueueRenderedLoginCode(string payload, int lifetimeMinutes, Guid? userId = null) =>
        outbox.Enqueue(Key, payload, userId, Expiring(lifetimeMinutes));

    public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            templates.RenderSignupCode(destination, code, lifetimeMinutes, culture)), options: Expiring(lifetimeMinutes));

    public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            templates.RenderVerifyEmailCode(destination, code, lifetimeMinutes, culture)), userId, Expiring(lifetimeMinutes));

    private OutboxEnqueueOptions Expiring(int lifetimeMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lifetimeMinutes, 1);
        return new(DiscardAfterUtc: clock.GetUtcNow().UtcDateTime.AddMinutes(lifetimeMinutes));
    }
}
