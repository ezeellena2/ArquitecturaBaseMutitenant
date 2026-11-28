using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>
/// Encola el mensaje renderizado dentro de la transacción del caso de uso. Un código contesta a quien lo pidió: sale con
/// prioridad <see cref="OutboxPriority.Reply"/>, antes que los avisos (ADR 0066), y si vence en la cola no se manda porque
/// lleva <see cref="OutboxEnqueueOptions.DiscardAfterUtc"/> en su vigencia (O3).
/// </summary>
internal sealed class EmailLoginCodeChannel(IEmailTemplateRenderer templates, IOutbox outbox, TimeProvider clock) : ILoginCodeChannel
{
    public string Key => OutboxChannel.Email;
    public LoginMethodType SupportedMethodType => LoginMethodType.Email;
    public LoginAuditMethod AuditMethod => LoginAuditMethod.Code;
    public bool IsAvailable => true;
    public Task<IReadOnlyList<string>> CountriesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
    public string? ContactPhone => null;
    public Task<Result> ValidateNewDestinationAsync(LoginCodeDestination destination, string? country, CancellationToken ct) =>
        Task.FromResult(destination.Channel == Key ? Result.Success() : Result.Failure(LoginChannelErrors.Unavailable));

    public async Task<string> RenderLoginCodeAsync(string destination, string code, int lifetimeMinutes, CultureProfile culture,
        CancellationToken ct) =>
        JsonSerializer.Serialize(await templates.RenderLoginCodeAsync(destination, code, lifetimeMinutes, culture, ct));

    public void EnqueueRenderedLoginCode(string payload, int lifetimeMinutes, Guid? userId = null) =>
        outbox.Enqueue(Key, payload, userId, Expiring(lifetimeMinutes));

    public async Task EnqueueSignupAsync(string destination, string code, int lifetimeMinutes, CultureProfile culture,
        CancellationToken ct) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            await templates.RenderSignupCodeAsync(destination, code, lifetimeMinutes, culture, ct)), options: Expiring(lifetimeMinutes));

    public async Task EnqueueVerificationAsync(Guid? userId, string destination, string code, int lifetimeMinutes,
        CultureProfile culture, CancellationToken ct) =>
        outbox.Enqueue(Key, JsonSerializer.Serialize(
            await templates.RenderVerifyEmailCodeAsync(destination, code, lifetimeMinutes, culture, ct)), userId,
            Expiring(lifetimeMinutes));

    private OutboxEnqueueOptions Expiring(int lifetimeMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lifetimeMinutes, 1);
        return new(Priority: OutboxPriority.Reply, DiscardAfterUtc: clock.GetUtcNow().UtcDateTime.AddMinutes(lifetimeMinutes));
    }
}
