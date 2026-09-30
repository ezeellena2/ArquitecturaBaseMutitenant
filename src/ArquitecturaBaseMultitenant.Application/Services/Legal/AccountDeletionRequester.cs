using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Comprueba bloqueos de los módulos y reautenticación antes de programar la baja y revocar sesiones.</summary>
internal sealed class AccountDeletionRequester(ILoginMethodRepository methods, IUserRepository users,
    ReauthTicketConsumer tickets, IEnumerable<IAccountDeletionParticipant> participants, ISignInService sessions,
    IPlatformSettingsReader settings, AccountDeletionNotifier notifier, TimeProvider timeProvider)
{
    internal async Task<Result<AccountDeletionResponse>> RequestAsync(Guid userId,
        RequestAccountDeletionRequest request, CancellationToken ct)
    {
        await methods.LockUserAsync(userId, ct);
        var account = await users.GetByIdAsync(userId, ct);
        if (account is null) return UserErrors.NotFound;
        if (AccountDeletionPolicy.CheckRequest(account) is { } protectedState) return protectedState;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var context = new AccountDeletionContext(userId, nowUtc);
        var blocks = new List<Error>();
        foreach (var participant in participants) blocks.AddRange(await participant.CheckAsync(context, ct));
        if (blocks.Count > 0) return AccountDeletionErrors.Blocked with
        {
            Metadata = new Dictionary<string, object?> { ["reasons"] = blocks.Select(error => ErrorTexts.Get(error.Code)).ToArray() },
        };
        var proof = await tickets.ConsumeAsync(userId, ReauthAction.DeleteAccount, null, request.ReauthTicket ?? string.Empty, ct);
        if (proof.IsFailure) return AccountDeletionErrors.ReauthRequired;
        var platform = await settings.FindAsync(ct)
            ?? throw new InvalidOperationException("The platform settings have not been seeded.");
        var requested = await users.RequestDeletionAsync(userId, request.Reason!, nowUtc, platform.AccountDeletionGraceDays, ct);
        if (requested.IsFailure) return requested.Error;
        await sessions.RevokeSessionsAsync(userId, ct);
        foreach (var participant in participants) await participant.OnRequestedAsync(context, ct);
        var scheduledForUtc = nowUtc.AddDays(platform.AccountDeletionGraceDays);
        await notifier.RequestedAsync(account, await methods.ListByUserIdAsync(userId, ct), nowUtc, scheduledForUtc, ct);
        return new AccountDeletionResponse(scheduledForUtc);
    }
}
