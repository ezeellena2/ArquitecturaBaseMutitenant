using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Ejecuta la limpieza de cada organización y luego purga credenciales y cierra la cuenta; invalida cachés después del commit.</summary>
internal sealed class AccountDeletionProcessor(IAccountDeletionRepository repository, IUserRepository users,
    ILoginMethodRepository methods, IEnumerable<IAccountDeletionParticipant> participants, AccountDeletionNotifier notifier,
    IAccessStatusCache statuses, ITenantStatusCache tenantStatuses)
{
    internal async Task<Result> ProcessTenantAsync(AccountDeletionWork work, AccountDeletionTenant tenant, DateTime nowUtc, CancellationToken ct)
    {
        await methods.LockUserAsync(work.UserId, ct);
        if (!await repository.HasValidLeaseAsync(work.UserId, work.LeaseId, nowUtc, ct)) return AccountDeletionErrors.ReauthRequired;
        var context = new AccountDeletionContext(work.UserId, nowUtc, tenant.TenantId, tenant.Kind);
        foreach (var participant in participants) await participant.ExecuteAsync(context, ct);
        return Result.Success();
    }

    internal async Task<Result> CompleteAsync(AccountDeletionWork work, DateTime nowUtc, CancellationToken ct)
    {
        await methods.LockUserAsync(work.UserId, ct);
        if (!await repository.HasValidLeaseAsync(work.UserId, work.LeaseId, nowUtc, ct)) return AccountDeletionErrors.ReauthRequired;
        var account = await users.GetByIdAsync(work.UserId, ct);
        if (account is null) return AccountDeletionErrors.ReauthRequired;
        var context = new AccountDeletionContext(work.UserId, nowUtc);
        foreach (var participant in participants) await participant.ExecuteAsync(context, ct);
        await notifier.DeletedAsync(account, await methods.ListByUserIdAsync(work.UserId, ct), nowUtc, ct);
        await repository.PurgeCredentialsAsync(work.UserId, ct);
        return await users.CompleteDeletionAsync(work.UserId, nowUtc, ErrorTexts.Get("Legal.AccountDeletion.DeletedDisplayName"), ct);
    }

    internal async Task InvalidateAsync(AccountDeletionWork work, AccountDeletionTenant? tenant, CancellationToken ct)
    {
        await statuses.InvalidateUserAsync(work.UserId, ct);
        if (tenant is not null)
        {
            await statuses.InvalidateMemberAsync(work.UserId, tenant.TenantId, ct);
            await tenantStatuses.InvalidateAsync(tenant.TenantId, ct);
        }
    }
}
