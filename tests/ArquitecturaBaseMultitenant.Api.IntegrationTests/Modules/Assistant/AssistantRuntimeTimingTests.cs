using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantRuntimeTimingTests
{
    [Fact]
    public void A_claimed_turn_keeps_its_persisted_deadline_with_the_injected_clock()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
        var admittedAtUtc = clock.GetUtcNow().UtcDateTime;
        var deadlineAtUtc = admittedAtUtc.AddMinutes(3);
        clock.Advance(TimeSpan.FromMinutes(2.5));
        using var cancellation = AssistantTurnWorker.CreateDeadline(deadlineAtUtc, clock);
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(cancellation.IsCancellationRequested);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(cancellation.IsCancellationRequested);
    }

    [Fact]
    public void An_expired_turn_is_cancelled_before_any_provider_work()
    {
        var clock = new FakeTimeProvider();
        using var cancellation = AssistantTurnWorker.CreateDeadline(clock.GetUtcNow().UtcDateTime.AddTicks(-1), clock);
        Assert.True(cancellation.IsCancellationRequested);
    }
}
