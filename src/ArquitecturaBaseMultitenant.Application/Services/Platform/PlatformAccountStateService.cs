using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Suspende, reactiva o cierra las sesiones de una cuenta desde Plataforma. Cada acción exige motivo y la
/// versión vista; dentro de la transacción toma los locks, relee la cuenta, aplica las reglas y guarda el
/// cambio con su SecurityEvent. Suspender y cerrar sesiones revoca todos los accesos; la caché de estado se
/// invalida después del commit. No hay avisos previstos para estas acciones.
/// </summary>
internal sealed class PlatformAccountStateService(IUserRepository users, ISignInService sessions,
    ILoginMethodRepository accounts, PlatformOperatorGuard operators, PlatformAudit audit,
    IAccessStatusCache statuses, ICurrentUser currentUser, IUnitOfWork unitOfWork, IRequestValidator validator,
    TimeProvider clock, ILogger<PlatformAccountStateService> logger) : IPlatformAccountStateService
{
    private enum Change { Suspend, Reactivate, RevokeSessions }

    public Task<Result> SuspendAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "SuspendPlatformAccount", () =>
            ChangeAsync(accountId, request, Change.Suspend, ct));

    public Task<Result> ReactivateAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ReactivatePlatformAccount", () =>
            ChangeAsync(accountId, request, Change.Reactivate, ct));

    public Task<Result> RevokeSessionsAsync(Guid accountId, PlatformAccountActionRequest request, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RevokePlatformAccountSessions", () =>
            ChangeAsync(accountId, request, Change.RevokeSessions, ct));

    private async Task<Result> ChangeAsync(Guid accountId, PlatformAccountActionRequest request, Change change,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
        // Un operador administra su propia cuenta desde Mi cuenta, no desde Plataforma.
        if (currentUser.UserId == accountId) return UserErrors.CannotModifySelf;
        var result = await unitOfWork.ExecuteInTransactionAsync(tx => ChangeCoreAsync(accountId, request, change, tx),
            CommitPolicy.OnSuccess, ct);
        if (result.IsSuccess) await statuses.InvalidateUserAsync(accountId, ct);
        return result;
    }

    private async Task<Result> ChangeCoreAsync(Guid accountId, PlatformAccountActionRequest request, Change change,
        CancellationToken ct)
    {
        // Lock global de operadores antes que el de la cuenta, en el mismo orden que el resto de Plataforma.
        if (change == Change.Suspend && await operators.CheckAnotherManagerRemainsAsync(accountId, ct) is { IsFailure: true } kept)
            return kept;
        await accounts.LockUserAsync(accountId, ct);
        var account = await users.GetByIdAsync(accountId, ct);
        if (account is null) return UserErrors.NotFound;
        if (account.Version != request.Version!.Value) throw new ConcurrencyConflictException();
        if (account.Status == UserStatus.Deleted) return UserErrors.InvalidTransition;

        var after = account.Status;
        switch (change)
        {
            case Change.Suspend:
                if (await users.SuspendAsync(accountId, ct) is { IsFailure: true } suspended) return suspended;
                after = UserStatus.Suspended;
                break;
            case Change.Reactivate:
                var reactivated = await users.ReactivateAsync(accountId, ct);
                if (reactivated.IsFailure) return reactivated.Error;
                after = reactivated.Value;
                break;
        }

        if (change != Change.Reactivate) await sessions.RevokeSessionsAsync(accountId, ct);
        var changes = new SecurityEventChanges();
        changes.AddEnum("status", account.Status, after);
        audit.RecordAccount(accountId, change switch
        {
            Change.Suspend => SecurityEventType.AccountSuspended,
            Change.Reactivate => SecurityEventType.AccountReactivated,
            _ => SecurityEventType.AccountSessionsRevoked,
        }, request.Reason!, changes);
        return Result.Success();
    }
}
