using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Consume un ticket de reautenticación por hash solo si su método de origen sigue disponible para la cuenta y acción esperadas.</summary>
internal sealed class ReauthTicketConsumer(IReauthTicketRepository tickets, ISecureTokenGenerator secrets,
    ILoginMethodRepository methods, LoginMethodAvailability availability, TimeProvider timeProvider)
{
    public async Task<Result> ConsumeAsync(Guid userId, ReauthAction action, Guid? targetMethodId,
        string secret, CancellationToken ct)
    {
        if (!ISecureTokenGenerator.HasTokenFormat(secret)) return ReauthErrors.Invalid;
        var ticket = await tickets.GetByHashAsync(secrets.Hash(secret), ct);
        if (ticket is null || ticket.SourceMethodId is not { } sourceId) return ReauthErrors.Invalid;
        var available = await availability.AvailableAsync(await methods.ListByUserIdAsync(userId, ct), ct);
        if (!available.Any(method => method.Id == sourceId)) return ReauthErrors.Invalid;
        return ticket.Consume(userId, action, targetMethodId, timeProvider.GetUtcNow().UtcDateTime);
    }
}
