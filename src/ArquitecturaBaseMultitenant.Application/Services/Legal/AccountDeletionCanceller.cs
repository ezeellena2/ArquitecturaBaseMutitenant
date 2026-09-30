using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Valida y consume el comprobante de cancelación de baja, reactiva la cuenta y avisa a los módulos participantes dentro de la transacción.</summary>
internal sealed class AccountDeletionCanceller(IReauthTicketRepository tickets, ISecureTokenGenerator secrets,
    ILoginMethodRepository methods, IUserRepository users, IEnumerable<IAccountDeletionParticipant> participants,
    AccountDeletionNotifier notifier, TimeProvider timeProvider)
{
    internal async Task<Result<CancelAccountDeletionResponse>> CancelAsync(CancelAccountDeletionRequest request, CancellationToken ct)
    {
        if (!ISecureTokenGenerator.HasTokenFormat(request.CancelTicket)) return AccountDeletionErrors.ReauthRequired;
        var hash = secrets.Hash(request.CancelTicket!);
        var userId = await tickets.GetUserIdByHashAsync(hash, ct);
        if (userId is null) return AccountDeletionErrors.ReauthRequired;
        await methods.LockUserAsync(userId.Value, ct);
        var ticket = await tickets.GetByHashAsync(hash, ct);
        if (ticket is null || ticket.UserId != userId || ticket.ConsumedAtUtc is not null)
            return AccountDeletionErrors.ReauthRequired;
        if (!ReturnUrls.TryReadAccessSelection(ticket.ReturnUrl, out _)) return AccountDeletionErrors.ReauthRequired;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var account = await users.GetByIdAsync(userId.Value, ct);
        if (account is null) return AccountDeletionErrors.ReauthRequired;
        if (account.Status == Domain.Users.UserStatus.Suspended) return AccountErrors.Suspended;
        if (account.Status != Domain.Users.UserStatus.PendingDeletion) return AccountDeletionErrors.ReauthRequired;
        if (account.DeletionScheduledForUtc is not { } date || nowUtc >= date) return AccountDeletionErrors.GraceExpired;
        if (ticket.Consume(userId.Value, ReauthAction.CancelDeletion, null, nowUtc).IsFailure)
            return AccountDeletionErrors.ReauthRequired;
        var cancelled = await users.CancelDeletionAsync(userId.Value, nowUtc, ct);
        if (cancelled.IsFailure) return cancelled.Error;
        foreach (var participant in participants)
            await participant.OnCancelledAsync(new AccountDeletionContext(userId.Value, nowUtc), ct);
        await notifier.CancelledAsync(account, await methods.ListByUserIdAsync(userId.Value, ct), nowUtc, ct);
        return new CancelAccountDeletionResponse(userId.Value, ticket.ReturnUrl!);
    }
}
