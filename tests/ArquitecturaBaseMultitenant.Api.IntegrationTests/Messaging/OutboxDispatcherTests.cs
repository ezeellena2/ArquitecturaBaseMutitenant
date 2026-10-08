using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// Comprueba despacho, confirmación y reintentos de mensajes pendientes. Protege el backoff y que un
/// mensaje enviado no vuelva a procesarse.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class OutboxDispatcherTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stop_during_inflight_transport_does_not_cancel_delivery_and_still_confirms_sent()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var clock = CreateClock(); var protector = CreateProtector();
        const string channel = "email-stop-inflight";
        var id = await SeedAsync(context, services.GetRequiredService<IUnitOfWork>(), protector, "inflight", clock, Ct, channel);
        using var stop = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new TestSender { Key = channel, DuringSend = async token =>
        { entered.TrySetResult(); await release.Task.WaitAsync(token); Assert.False(token.IsCancellationRequested); } };
        var dispatcher = CreateDispatcher(services, protector, clock);
        var sending = dispatcher.DispatchOnceAsync([sender], stop.Token);
        await entered.Task.WaitAsync(Ct);
        stop.Cancel(); release.TrySetResult();
        Assert.Equal(1, (await sending).Value);
        Assert.Equal(OutboxStatus.Sent, (await context.OutboxMessages.AsNoTracking().SingleAsync(value => value.Id == id, Ct)).Status);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.DispatchOnceAsync([sender], stop.Token));
        Assert.Single(sender.Payloads);
    }

    [Fact]
    public async Task Due_message_is_sent_once_and_marked_sent()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var protector = CreateProtector();
        var clock = CreateClock();
        const string payload = "delivery payload";
        const string channel = "email-dispatch-once-test";
        var id = await SeedAsync(context, unitOfWork, protector, payload, clock, Ct, channel);
        var sender = new TestSender { Key = channel };
        var dispatcher = CreateDispatcher(services, protector, clock);

        Assert.Equal(1, (await dispatcher.DispatchOnceAsync([sender], Ct)).Value);
        Assert.Equal(0, (await dispatcher.DispatchOnceAsync([sender], Ct)).Value);

        Assert.Equal([payload], sender.Payloads);
        var saved = await context.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == id, Ct);
        Assert.Equal(OutboxStatus.Sent, saved.Status);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, saved.SentAtUtc);
        Assert.Null(saved.NextAttemptAtUtc);
    }

    [Fact]
    public async Task Failure_retries_with_backoff_and_stops_after_limit()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var protector = CreateProtector();
        var clock = CreateClock();
        var id = await SeedAsync(context, unitOfWork, protector, "retry payload", clock, Ct, "email-retry");
        var sender = new TestSender { Key = "email-retry", Fail = true };
        var dispatcher = CreateDispatcher(services, protector, clock, maxAttempts: 3);

        Assert.Equal(1, (await dispatcher.DispatchOnceAsync([sender], Ct)).Value);
        var first = await context.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == id, Ct);
        Assert.Equal(OutboxStatus.Failed, first.Status);
        Assert.Equal(1, first.Attempts);
        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddSeconds(30), first.NextAttemptAtUtc);
        Assert.Equal(0, (await dispatcher.DispatchOnceAsync([sender], Ct)).Value);

        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(1, (await dispatcher.DispatchOnceAsync([sender], Ct)).Value);
        var second = await context.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == id, Ct);
        Assert.Equal(2, second.Attempts);
        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddSeconds(60), second.NextAttemptAtUtc);

        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(1, (await dispatcher.DispatchOnceAsync([sender], Ct)).Value);
        var terminal = await context.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == id, Ct);
        Assert.Equal(3, terminal.Attempts);
        Assert.Null(terminal.NextAttemptAtUtc);
        Assert.Equal(0, (await dispatcher.DispatchOnceAsync([sender], Ct)).Value);
        Assert.Equal(3, sender.Payloads.Count);
    }

    [Fact]
    public async Task Retry_delay_starts_when_delivery_fails_not_when_batch_was_locked()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var protector = CreateProtector();
        var clock = CreateClock();
        var id = await SeedAsync(context, unitOfWork, protector, "slow delivery", clock, Ct, "email-slow");
        var sender = new TestSender(() =>
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            return Task.CompletedTask;
        }) { Key = "email-slow", Fail = true };
        var dispatcher = CreateDispatcher(services, protector, clock);

        Assert.Equal(1, (await dispatcher.DispatchOnceAsync([sender], Ct)).Value);

        var saved = await context.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == id, Ct);
        Assert.Equal(clock.GetUtcNow().UtcDateTime.AddSeconds(30), saved.NextAttemptAtUtc);
    }

    [Fact]
    public async Task Concurrent_dispatcher_skips_locked_message()
    {
        using var client = factory.CreateClient();
        var protector = CreateProtector();
        var clock = CreateClock();
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider;
        var second = secondScope.ServiceProvider;
        var firstContext = first.GetRequiredService<ApplicationDbContext>();
        var firstUnit = first.GetRequiredService<IUnitOfWork>();
        var id = await SeedAsync(firstContext, firstUnit, protector, "concurrent payload", clock, Ct,
            "email-concurrent");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new TestSender(async () =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Ct);
        }) { Key = "email-concurrent" };
        var firstDispatcher = CreateDispatcher(first, protector, clock);
        var secondDispatcher = CreateDispatcher(second, protector, clock);

        var sending = firstDispatcher.DispatchOnceAsync([sender], Ct);
        await entered.Task.WaitAsync(Ct);
        var skipped = await secondDispatcher.DispatchOnceAsync([sender], Ct);
        release.TrySetResult();

        Assert.Equal(0, skipped.Value);
        Assert.Equal(1, (await sending).Value);
        Assert.Single(sender.Payloads);
        var saved = await firstContext.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == id, Ct);
        Assert.Equal(OutboxStatus.Sent, saved.Status);
    }

    [Fact]
    public async Task Stopping_after_first_delivery_commits_it_without_resending_on_restart()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var protector = CreateProtector();
        var clock = CreateClock();
        const string channel = "email-stop";
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var firstId = await SeedAsync(context, unitOfWork, protector, "first", clock, Ct, channel);
        var secondId = await SeedAsync(context, unitOfWork, protector, "second", clock, Ct, channel);
        using var stop = new CancellationTokenSource();
        var sender = new TestSender(() =>
        {
            stop.Cancel();
            return Task.CompletedTask;
        }) { Key = channel };
        var dispatcher = CreateDispatcher(services, protector, clock);

        Assert.Equal(1, (await dispatcher.DispatchOnceAsync([sender], stop.Token)).Value);

        var states = await context.OutboxMessages.AsNoTracking()
            .Where(message => message.Id == firstId || message.Id == secondId)
            .Select(message => message.Status).ToListAsync(Ct);
        Assert.Equal(1, states.Count(status => status == OutboxStatus.Sent));
        Assert.Equal(1, states.Count(status => status == OutboxStatus.Pending));
        Assert.Single(sender.Payloads);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await dispatcher.DispatchOnceAsync([sender], stop.Token));
    }

    private static TestDispatcher CreateDispatcher(IServiceProvider services, PayloadProtector protector,
        FakeTimeProvider clock,
        int maxAttempts = 5) =>
        new(services, protector, clock, maxAttempts);

    private sealed class TestDispatcher(IServiceProvider services, PayloadProtector protector, FakeTimeProvider clock, int maxAttempts)
    {
        public async Task<Result<int>> DispatchOnceAsync(IReadOnlyCollection<IChannelSender> senders, CancellationToken ct) =>
            Result.Success(await new OutboxDeliveryProcessor(new DispatchScopeFactory(services, clock, maxAttempts),
                protector, clock, Options.Create(new OutboxOptions { MaxAttempts = maxAttempts }),
                NullLogger<OutboxDeliveryProcessor>.Instance).ProcessOnceAsync(senders, ct));
    }

    private sealed class DispatchScopeFactory(IServiceProvider services, FakeTimeProvider clock, int maxAttempts) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new DispatchScope(services.CreateScope(), clock, maxAttempts);
    }
    private sealed class DispatchScope(IServiceScope inner, FakeTimeProvider clock, int maxAttempts) : IServiceScope, IServiceProvider
    {
        public IServiceProvider ServiceProvider => this;
        public object? GetService(Type type) => type == typeof(IOutboxDispatchService)
            ? new OutboxDispatchService(inner.ServiceProvider.GetRequiredService<IUnitOfWork>(),
                new OutboxDispatchStore(inner.ServiceProvider.GetRequiredService<ApplicationDbContext>(), clock,
                    Options.Create(new OutboxOptions { MaxAttempts = maxAttempts })), [], clock, NullLogger<OutboxDispatchService>.Instance)
            : inner.ServiceProvider.GetService(type);
        public void Dispose() => inner.Dispose();
    }

    private static PayloadProtector CreateProtector() =>
        new PayloadProtector(new EphemeralDataProtectionProvider());

    private static FakeTimeProvider CreateClock() =>
        new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

    private static async Task<Guid> SeedAsync(ApplicationDbContext context, IUnitOfWork unitOfWork,
        PayloadProtector protector, string payload, FakeTimeProvider clock, CancellationToken cancellationToken,
        string channel = OutboxChannel.Email)
    {
        Guid id = Guid.Empty;
        await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            var message = OutboxMessage.Enqueue(channel, protector.Protect(payload),
                clock.GetUtcNow().UtcDateTime);
            id = message.Id;
            context.OutboxMessages.Add(message);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, cancellationToken);
        return id;
    }

    private sealed class TestSender(Func<Task>? beforeSend = null) : IChannelSender
    {
        public string Key { get; init; } = OutboxChannel.Email;

        public bool Fail { get; init; }
        public Func<CancellationToken, Task>? DuringSend { get; init; }

        public List<string> Payloads { get; } = [];

        public async Task SendAsync(string payload, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Payloads.Add(payload);
            if (DuringSend is not null) await DuringSend(cancellationToken);
            if (beforeSend is not null)
            {
                await beforeSend();
            }

            if (Fail)
            {
                throw new InvalidOperationException("Test transport failure.");
            }
        }
    }
}
