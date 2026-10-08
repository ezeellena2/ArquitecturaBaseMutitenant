using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>Reinicio, leases viejos y atomicidad del receipt contra PostgreSQL real.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class OutboxLeaseTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Claim_commits_before_transport_and_restart_skips_lease_then_reclaims_without_old_completion()
    {
        using var client = factory.CreateClient();
        var channel = "lease-" + Guid.NewGuid().ToString("N");
        var id = await SeedAsync(channel);
        OutboxDispatchClaim first;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            first = (await Dispatch(scope.ServiceProvider).ClaimNextAsync([channel], Ct)).Value!;
            Assert.NotNull(first);
            Assert.Null(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction);
            Assert.DoesNotContain("encrypted-secret", first.ToString());
        }
        await using var restart = factory.Services.CreateAsyncScope();
        var context = restart.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await context.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == id, Ct);
        Assert.Equal(first.LeaseId, saved.LeaseId);
        Assert.Null((await Dispatch(restart.ServiceProvider).ClaimNextAsync([channel], Ct)).Value);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var second = (await Dispatch(restart.ServiceProvider).ClaimNextAsync([channel], Ct)).Value!;
        var observer = new Observer(channel);
        await Dispatch(restart.ServiceProvider, observer).CompleteAsync(id, first.LeaseId, OutboxDeliveryReceipt.Accepted("old"), Ct);
        Assert.Equal(0, observer.Calls);
        await Dispatch(restart.ServiceProvider, observer).CompleteAsync(id, second.LeaseId, OutboxDeliveryReceipt.Accepted("receipt"), Ct);
        await Dispatch(restart.ServiceProvider, observer).CompleteAsync(id, second.LeaseId, OutboxDeliveryReceipt.Accepted("replay"), Ct);
        Assert.Equal(1, observer.Calls);
        saved = await context.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == id, Ct);
        Assert.Equal("receipt", saved.ProviderMessageId);
        Assert.Equal(2, saved.Attempts);
        Assert.Empty(saved.EncryptedPayload);
    }

    [Fact]
    public async Task Cancellation_and_observer_failure_never_resurrect_or_half_commit_acceptance()
    {
        using var client = factory.CreateClient();
        var channel = "cancel-" + Guid.NewGuid().ToString("N");
        var id = await SeedAsync(channel);
        await using var first = factory.Services.CreateAsyncScope();
        var claim = (await Dispatch(first.ServiceProvider).ClaimNextAsync([channel], Ct)).Value!;
        var observer = new Observer(channel) { Fail = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Dispatch(first.ServiceProvider, observer)
            .CompleteAsync(id, claim.LeaseId, OutboxDeliveryReceipt.Accepted("receipt"), Ct));
        var context = first.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(OutboxStatus.Pending, (await context.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == id, Ct)).Status);
        await InTransactionAsync(async (provider, ct) =>
        {
            (await provider.GetRequiredService<ApplicationDbContext>().OutboxMessages.SingleAsync(value => value.Id == id, ct)).Cancel();
        });
        observer.Fail = false;
        await Dispatch(first.ServiceProvider, observer).CompleteAsync(id, claim.LeaseId, OutboxDeliveryReceipt.Accepted("late"), Ct);
        Assert.Equal(1, observer.Calls); // Solo el intento que abortó; el resultado cancelado no invoca el observer.
        Assert.Equal(OutboxStatus.Cancelled, (await context.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == id, Ct)).Status);
    }

    private OutboxDispatchService Dispatch(IServiceProvider provider, params IOutboxDeliveryObserver[] observers) =>
        new(provider.GetRequiredService<IUnitOfWork>(), new OutboxDispatchStore(provider.GetRequiredService<ApplicationDbContext>(),
            _clock, Options.Create(new OutboxOptions())), observers, _clock, NullLogger<OutboxDispatchService>.Instance);
    private async Task<Guid> SeedAsync(string channel)
    {
        var row = OutboxMessage.Enqueue(channel, "encrypted-secret", _clock.GetUtcNow().UtcDateTime);
        await InTransactionAsync((provider, _) =>
        { provider.GetRequiredService<ApplicationDbContext>().OutboxMessages.Add(row); return Task.CompletedTask; });
        return row.Id;
    }
    private async Task InTransactionAsync(Func<IServiceProvider, CancellationToken, Task> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        { await work(scope.ServiceProvider, ct); return Result.Success(); }, CommitPolicy.OnSuccess, Ct);
    }
    private sealed class Observer(string key) : IOutboxDeliveryObserver
    {
        public string Key => key;
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public Task RecordAcceptedAsync(Guid id, OutboxDeliveryReceipt receipt, DateTime acceptedAtUtc, CancellationToken ct)
        { Calls++; if (Fail) throw new InvalidOperationException("Fixture observer failure."); return Task.CompletedTask; }
    }
}
