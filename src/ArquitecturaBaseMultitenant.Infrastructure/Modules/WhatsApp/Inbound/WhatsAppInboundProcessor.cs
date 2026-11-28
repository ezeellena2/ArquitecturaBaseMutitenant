using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;

internal sealed class WhatsAppInboundProcessor(WhatsAppInboundSignal signal, IServiceScopeFactory scopes,
    IOptions<WhatsAppOptions> options, IEnumerable<IKeyRingReadiness> keyRings, TimeProvider clock, ILogger<WhatsAppInboundProcessor> logger)
    : PeriodicWorker(TimeSpan.FromSeconds(options.Value.InboundPollSeconds), clock, logger)
{
    private const int BatchSize = 50;
    private const int MaxBatchesPerIteration = 20;
    private bool _hasMorePending;

    // Un número puede conectarse desde la pantalla después del arranque: la cola se revisa siempre que el proceso la atienda.
    protected override bool Enabled => options.Value.ProcessInboundInBackground;
    internal async Task<int> ProcessPendingAsync(CancellationToken ct)
    {
        var totalProcessed = 0;
        _hasMorePending = false;
        // Drena en lotes acotados de 50 sin dormir mientras la cola siga llena; un poison queda con backoff y no bloquea al siguiente.
        for (var batch = 0; batch < MaxBatchesPerIteration && !ct.IsCancellationRequested; batch++)
        {
            var (claimed, processed) = await ProcessBatchAsync(ct);
            totalProcessed += processed;
            if (claimed < BatchSize) return totalProcessed;
        }
        _hasMorePending = !ct.IsCancellationRequested;
        return totalProcessed;
    }
    private async Task<(int Claimed, int Processed)> ProcessBatchAsync(CancellationToken ct)
    {
        var claimed = 0;
        var processed = 0;
        for (var index = 0; index < BatchSize && !ct.IsCancellationRequested; index++)
        {
            WhatsAppInboundClaim? claim;
            await using (var scope = scopes.CreateAsyncScope())
                claim = (await scope.ServiceProvider.GetRequiredService<IWhatsAppInboundService>().ClaimAsync(ct)).Value;
            if (claim is null) break;
            claimed++;
            try
            {
                var result = await ProcessAsync(claim, ct);
                if (result.IsSuccess) processed++;
                else
                {
                    await using var failed = scopes.CreateAsyncScope();
                    await failed.ServiceProvider.GetRequiredService<IWhatsAppInboundService>().FailAsync(claim, ct);
                }
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                using var logScope = Logger.BeginScope(new Dictionary<string, object> { ["MessageId"] = claim.MessageId });
                ExceptionLog.Write(Logger, nameof(WhatsAppInboundProcessor), exception);
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IWhatsAppInboundService>().FailAsync(claim, ct);
            }
        }
        return (claimed, processed);
    }
    /// <summary>
    /// La entrada de un número de organización se procesa en un scope propio, como la organización (el vínculo con una cuenta
    /// es un dato compartido y su parte la sella el guardado, como en RolePermissionCache); la de Plataforma, en un scope común.
    /// </summary>
    private async Task<Result> ProcessAsync(WhatsAppInboundClaim claim, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (claim.OrganizationId is { } organizationId)
            scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(organizationId, TenantKind.Business);
        return await scope.ServiceProvider.GetRequiredService<IWhatsAppInboundService>().ProcessAsync(claim, ct);
    }
    protected override async Task RunIterationAsync(CancellationToken ct)
    {
        _hasMorePending = false;
        // 0064 §11: una copia con otro anillo no abre lo que guardaron las demás ni escribe lo que ellas no podrían abrir.
        foreach (var keyRing in keyRings) if (!await keyRing.MatchesAsync(ct)) return;
        await ProcessPendingAsync(ct);
    }
    protected override async ValueTask<bool> WaitForNextIterationAsync(PeriodicTimer timer, CancellationToken ct)
    {
        if (_hasMorePending) return !ct.IsCancellationRequested;
        await signal.WaitAsync(TimeSpan.FromSeconds(options.Value.InboundPollSeconds), Clock, ct);
        return !ct.IsCancellationRequested;
    }
}
