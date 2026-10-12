using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using Microsoft.Extensions.DependencyInjection;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
internal sealed class WhatsAppInboundProcessor(WhatsAppInboundSignal signal, IServiceScopeFactory scopes,
    IOptions<WhatsAppOptions> options, TimeProvider clock, ILogger<WhatsAppInboundProcessor> logger)
    : PeriodicWorker(TimeSpan.FromSeconds(options.Value.InboundPollSeconds), clock, logger)
{
    protected override bool Enabled => options.Value.ProcessInboundInBackground && !string.IsNullOrWhiteSpace(options.Value.PhoneNumberId);
    internal async Task<int> ProcessPendingAsync(CancellationToken ct)
    {
        var processed = 0;
        // Una vuelta acotada; un poison queda con backoff y no bloquea al siguiente.
        for (var index = 0; index < 50 && !ct.IsCancellationRequested; index++)
        {
            WhatsAppInboundClaim? claim;
            await using (var scope = scopes.CreateAsyncScope())
                claim = (await scope.ServiceProvider.GetRequiredService<IWhatsAppInboundService>().ClaimAsync(ct)).Value;
            if (claim is null) break;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IWhatsAppInboundService>().ProcessAsync(claim, ct);
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
        return processed;
    }
    protected override Task RunIterationAsync(CancellationToken ct) => ProcessPendingAsync(ct);
    protected override async ValueTask<bool> WaitForNextIterationAsync(PeriodicTimer timer, CancellationToken ct)
    {
        await signal.WaitAsync(TimeSpan.FromSeconds(options.Value.InboundPollSeconds), Clock, ct);
        return !ct.IsCancellationRequested;
    }
}
