using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>Periodic worker; the Application service commits each message separately.</summary>
internal sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), timeProvider);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // El transporte SMTP vive durante el lote; cada mensaje conserva su propia UoW.
                await using var senderScope = scopeFactory.CreateAsyncScope();
                var senders = senderScope.ServiceProvider.GetServices<IChannelSender>().ToArray();
                for (var count = 0; count < options.Value.BatchSize && senders.Length > 0 &&
                    !stoppingToken.IsCancellationRequested; count++)
                {
                    await using var messageScope = scopeFactory.CreateAsyncScope();
                    if ((await messageScope.ServiceProvider.GetRequiredService<IOutboxDispatchService>()
                            .DispatchOnceAsync(senders, stoppingToken)).Value == 0)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogBatchFailed(logger, exception.GetType().Name);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Outbox batch failed with {ExceptionType}")]
    private static partial void LogBatchFailed(ILogger logger, string exceptionType);
}
