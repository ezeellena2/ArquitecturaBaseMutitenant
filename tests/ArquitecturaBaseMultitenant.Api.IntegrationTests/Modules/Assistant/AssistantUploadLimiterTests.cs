using ArquitecturaBaseMultitenant.Api.Modules.Assistant;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

// ADR 0064 §4: the copy's slots for Home's uploads. With all of them taken the next ones wait by order of arrival in a short
// queue, where one organization takes at most its places, so it never leaves another without one.
public sealed class AssistantUploadLimiterTests
{
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(10);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_organization_waits_in_at_most_its_places_of_the_queue_and_another_organization_still_finds_one()
    {
        using var limiter = Limiter(concurrency: 1, queue: 3, perOrganization: 2);
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        var running = await limiter.TryAcquireAsync(first, Ct);
        Assert.NotNull(running);

        // Two more of the first organization wait; a third of it is refused at once, though the queue still has room.
        var waitingA = limiter.TryAcquireAsync(first, Ct).AsTask();
        var waitingB = limiter.TryAcquireAsync(first, Ct).AsTask();
        Assert.False(waitingA.IsCompleted);
        Assert.False(waitingB.IsCompleted);
        Assert.Null(await limiter.TryAcquireAsync(first, Ct));

        // Another organization still finds its place; after it the queue is full for everyone.
        var other = limiter.TryAcquireAsync(second, Ct).AsTask();
        Assert.False(other.IsCompleted);
        Assert.Null(await limiter.TryAcquireAsync(Guid.NewGuid(), Ct));

        // A freed slot goes to the oldest in the queue, and its organization gets that place in the queue back.
        running.Dispose();
        var next = await waitingA.WaitAsync(Soon, Ct);
        Assert.NotNull(next);
        Assert.False(waitingB.IsCompleted);
        var again = limiter.TryAcquireAsync(first, Ct).AsTask();
        Assert.False(again.IsCompleted);
        Assert.Null(await limiter.TryAcquireAsync(first, Ct));

        next.Dispose();
        var third = await waitingB.WaitAsync(Soon, Ct);
        Assert.NotNull(third);
        Assert.False(other.IsCompleted);
        third.Dispose();
        var fromOther = await other.WaitAsync(Soon, Ct);
        Assert.NotNull(fromOther);
        fromOther.Dispose();
        (await again.WaitAsync(Soon, Ct))!.Dispose();
    }

    [Fact]
    public async Task A_wait_the_request_cancels_gives_its_place_in_the_queue_back_to_its_organization()
    {
        using var limiter = Limiter(concurrency: 1, queue: 2, perOrganization: 1);
        var organization = Guid.NewGuid();
        var running = await limiter.TryAcquireAsync(Guid.NewGuid(), Ct);
        Assert.NotNull(running);
        using var cut = new CancellationTokenSource();
        var cancelled = limiter.TryAcquireAsync(organization, cut.Token).AsTask();
        Assert.Null(await limiter.TryAcquireAsync(organization, Ct));

        await cut.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var waiting = limiter.TryAcquireAsync(organization, Ct).AsTask();
        Assert.False(waiting.IsCompleted);
        running.Dispose();
        (await waiting.WaitAsync(Soon, Ct))!.Dispose();
    }

    [Fact]
    public async Task Without_a_queue_the_next_upload_is_refused_while_the_slots_are_taken_and_enters_when_one_is_freed()
    {
        using var limiter = Limiter(concurrency: 2, queue: 0, perOrganization: 2);
        var organization = Guid.NewGuid();
        var first = await limiter.TryAcquireAsync(organization, Ct);
        var second = await limiter.TryAcquireAsync(Guid.NewGuid(), Ct);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(await limiter.TryAcquireAsync(organization, Ct));
        first.Dispose();
        var third = await limiter.TryAcquireAsync(organization, Ct);
        Assert.NotNull(third);
        third.Dispose();
        second.Dispose();
    }

    private static AssistantUploadLimiter Limiter(int concurrency, int queue, int perOrganization) =>
        new(Options.Create(new AssistantOptions
        {
            Uploads = new AssistantUploadOptions { ConcurrencyLimit = concurrency, QueueLimit = queue, QueuePerOrganization = perOrganization },
        }));
}
