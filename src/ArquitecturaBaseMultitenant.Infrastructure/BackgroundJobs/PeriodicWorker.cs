using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Logging;

namespace ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;

/// <summary>Ejecuta mantenimiento periódico con un único reloj y política de fallos.</summary>
public abstract class PeriodicWorker : BackgroundService
{
    private readonly TimeSpan interval;
    protected TimeProvider Clock { get; }
    protected ILogger Logger { get; }
    protected PeriodicWorker(TimeSpan interval, TimeProvider timeProvider, ILogger logger)
    {
        this.interval = interval;
        Clock = timeProvider;
        Logger = logger;
    }
    protected virtual bool Enabled => true;
    protected abstract Task RunIterationAsync(CancellationToken ct);
    protected virtual ValueTask<bool> WaitForNextIterationAsync(PeriodicTimer timer, CancellationToken ct) => timer.WaitForNextTickAsync(ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled || System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider") return;
        using var timer = new PeriodicTimer(interval, Clock);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunIterationAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { ExceptionLog.Write(Logger, GetType().Name, exception); }
            try { if (!await WaitForNextIterationAsync(timer, stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                ExceptionLog.Write(Logger, GetType().Name, exception);
                try { await Task.Delay(interval, Clock, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
