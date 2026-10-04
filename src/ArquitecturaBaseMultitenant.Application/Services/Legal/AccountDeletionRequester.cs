using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>La baja que pide la persona: comprueba su estado, los bloqueos y la reautenticación antes de programarla.</summary>
internal sealed class AccountDeletionRequester(ILoginMethodRepository methods, IUserRepository users,
    ReauthTicketConsumer tickets, AccountDeletionScheduler scheduler, TimeProvider timeProvider)
{
    internal async Task<Result<AccountDeletionResponse>> RequestAsync(Guid userId,
        RequestAccountDeletionRequest request, CancellationToken ct)
    {
        await methods.LockUserAsync(userId, ct);
        var account = await users.GetByIdAsync(userId, ct);
        if (account is null) return UserErrors.NotFound;
        if (AccountDeletionPolicy.CheckRequest(account) is { } protectedState) return protectedState;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (await scheduler.CheckBlocksAsync(userId, nowUtc, ct) is { } blocked) return blocked;
        var proof = await tickets.ConsumeAsync(userId, ReauthAction.DeleteAccount, null, request.ReauthTicket ?? string.Empty, ct);
        if (proof.IsFailure) return AccountDeletionErrors.ReauthRequired;
        var scheduled = await scheduler.ScheduleAsync(account, request.Reason!, nowUtc, operatorId: null, ct);
        return scheduled.IsFailure ? scheduled.Error : new AccountDeletionResponse(scheduled.Value);
    }
}
