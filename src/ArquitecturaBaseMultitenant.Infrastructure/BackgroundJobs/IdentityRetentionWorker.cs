using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Retention;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>
/// ADR 0062: una vez por día borra los códigos de ingreso, los ingresos y las sesiones de OpenIddict vencidos de plazo,
/// en el scope global, sin una organización. Cada lote o tramo va en su scope y su transacción, hasta que no queda nada.
/// Sin lease todavía (llega con la B6 de la E10): cada purga es idempotente y dos copias borran a lo sumo lo mismo. Un
/// error deja solo su tipo en el log, y la corrida siguiente sigue desde donde quedó.
/// </summary>
internal sealed partial class IdentityRetentionWorker(IServiceScopeFactory scopes, TimeProvider timeProvider,
    ILogger<IdentityRetentionWorker> logger) : PeriodicWorker(TimeSpan.FromDays(1), timeProvider, logger)
{
    internal async Task RunOnceAsync(CancellationToken ct)
    {
        await RunStepAsync(PurgeLoginCodesAsync, ct);
        await RunStepAsync(PurgeLoginAuditsAsync, ct);
        await RunStepAsync(PruneSessionsAsync, ct);
    }

    protected override Task RunIterationAsync(CancellationToken ct) => RunOnceAsync(ct);

    // Una purga que falla no frena a las otras.
    private async Task RunStepAsync(Func<CancellationToken, Task> step, CancellationToken ct)
    {
        try { await step(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception) { ExceptionLog.Write(Logger, nameof(IdentityRetentionWorker), exception); }
    }

    private async Task PurgeLoginCodesAsync(CancellationToken ct)
    {
        var total = 0;
        while (!ct.IsCancellationRequested)
        {
            var removed = await InScopeAsync(service => service.PurgeLoginCodesAsync(ct));
            if (removed.IsFailure) { LogFailed(Logger, removed.Error.Code); break; }
            total += removed.Value;
            if (removed.Value == 0) break;
        }
        if (total > 0) LogRemoved(Logger, total, "login codes");
    }

    private async Task PurgeLoginAuditsAsync(CancellationToken ct)
    {
        var total = 0;
        RetentionCursor? after = null;
        do
        {
            var batch = await InScopeAsync(service => service.PurgeLoginAuditsAsync(after, ct));
            if (batch.IsFailure) { LogFailed(Logger, batch.Error.Code); break; }
            total += batch.Value.Removed;
            after = batch.Value.Next;
        }
        while (after is not null && !ct.IsCancellationRequested);
        if (total > 0) LogRemoved(Logger, total, "sign-ins");
    }

    private async Task PruneSessionsAsync(CancellationToken ct)
    {
        long tokens = 0;
        long authorizations = 0;
        DateTime? from = null;
        do
        {
            var tranche = await InScopeAsync(service => service.PruneSessionsAsync(from, ct));
            if (tranche.IsFailure) { LogFailed(Logger, tranche.Error.Code); break; }
            tokens += tranche.Value.Tokens;
            authorizations += tranche.Value.Authorizations;
            from = tranche.Value.NextFromUtc;
        }
        while (from is not null && !ct.IsCancellationRequested);
        if (tokens > 0) LogRemoved(Logger, tokens, "OpenIddict tokens");
        if (authorizations > 0) LogRemoved(Logger, authorizations, "OpenIddict authorizations");
    }

    // Un lote o un tramo, cada uno en su scope y su transacción.
    private async Task<Result<T>> InScopeAsync<T>(Func<IIdentityRetentionService, Task<Result<T>>> step)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await step(scope.ServiceProvider.GetRequiredService<IIdentityRetentionService>());
    }

    // Cuántas filas, nunca cuáles.
    [LoggerMessage(Level = LogLevel.Information, Message = "Identity retention removed {Count} {Kind}")]
    private static partial void LogRemoved(ILogger logger, long count, string kind);

    [LoggerMessage(Level = LogLevel.Error, Message = "Identity retention failed: {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);
}
