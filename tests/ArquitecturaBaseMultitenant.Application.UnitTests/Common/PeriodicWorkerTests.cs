using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class PeriodicWorkerTests
{
    [Fact]
    public async Task Unexpected_failure_is_logged_privately_and_the_next_iteration_still_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider();
        var logger = new FakeLogger<PeriodicWorkerTests>();
        using var worker = new FailingWorker(clock, logger);
        await worker.StartAsync(ct);
        await worker.First.Task.WaitAsync(ct);
        clock.Advance(TimeSpan.FromSeconds(5));
        var completed = await Task.WhenAny(worker.Second.Task, worker.ExecuteTask!);
        Assert.Same(worker.Second.Task, completed);
        await worker.Second.Task.WaitAsync(ct);
        await worker.StopAsync(ct);
        Assert.Null(worker.ExecuteTask!.Exception);
        var records = logger.Collector.GetSnapshot();
        Assert.Contains(records, record => record.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
            && record.Message.Contains("RunIterationAsync", StringComparison.Ordinal));
        Assert.All(records, record => Assert.Null(record.Exception));
        Assert.DoesNotContain("PRIVATE_WORKER_MESSAGE", string.Join(' ', records.Select(record => record.Message)));
    }

    private sealed class FailingWorker(FakeTimeProvider clock, FakeLogger<PeriodicWorkerTests> logger)
        : PeriodicWorker(TimeSpan.FromSeconds(5), clock, logger)
    {
        private int iterations;
        internal TaskCompletionSource First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Second { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task RunIterationAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (++iterations == 1)
            {
                First.TrySetResult();
                throw new InvalidOperationException("PRIVATE_WORKER_MESSAGE");
            }
            Second.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
