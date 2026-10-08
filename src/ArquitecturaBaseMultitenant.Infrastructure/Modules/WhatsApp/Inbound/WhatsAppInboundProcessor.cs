using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
internal sealed partial class WhatsAppInboundProcessor(WhatsAppInboundSignal signal, IServiceScopeFactory scopes,
    IOptions<WhatsAppOptions> options, TimeProvider clock, ILogger<WhatsAppInboundProcessor> logger) : BackgroundService
{
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
                LogFailed(logger, claim.MessageId, exception.GetType().Name);
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IWhatsAppInboundService>().FailAsync(claim, ct);
            }
        }
        return processed;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ProcessInboundInBackground || string.IsNullOrWhiteSpace(options.Value.PhoneNumberId)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessPendingAsync(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested) { LogRoundFailed(logger, exception.GetType().Name); }
            await signal.WaitAsync(TimeSpan.FromSeconds(options.Value.InboundPollSeconds), clock, stoppingToken);
        }
    }
    [LoggerMessage(Level = LogLevel.Warning, Message = "WhatsApp inbound message {MessageId} failed with {ExceptionType}")]
    private static partial void LogFailed(ILogger logger, Guid messageId, string exceptionType);
    [LoggerMessage(Level = LogLevel.Warning, Message = "WhatsApp inbound round failed with {ExceptionType}")]
    private static partial void LogRoundFailed(ILogger logger, string exceptionType);
}
