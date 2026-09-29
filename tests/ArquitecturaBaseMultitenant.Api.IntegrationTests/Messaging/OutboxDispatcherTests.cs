using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
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

[Collection(ApiTestGroup.Name)]
public sealed class OutboxDispatcherTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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
        var id = await SeedAsync(context, unitOfWork, protector, payload, clock, Ct);
        var sender = new TestSender();
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

    private static OutboxDispatchService CreateDispatcher(IServiceProvider services, PayloadProtector protector,
        FakeTimeProvider clock,
        int maxAttempts = 5) =>
        new(services.GetRequiredService<IUnitOfWork>(),
            new OutboxDispatchStore(services.GetRequiredService<ApplicationDbContext>(), protector, clock,
                Options.Create(new OutboxOptions { MaxAttempts = maxAttempts }),
                NullLogger<OutboxDispatchStore>.Instance),
            clock, NullLogger<OutboxDispatchService>.Instance);

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

        public List<string> Payloads { get; } = [];

        public async Task SendAsync(string payload, CancellationToken cancellationToken)
        {
            Payloads.Add(payload);
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
