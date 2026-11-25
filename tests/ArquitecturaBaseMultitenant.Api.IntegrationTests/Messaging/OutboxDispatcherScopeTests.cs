using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>SMTP conserva un scope por lote; claim y complete no comparten el scope transaccional.</summary>
public sealed class OutboxDispatcherScopeTests
{
    [Fact]
    public async Task Batch_reuses_sender_and_claim_complete_use_independent_scopes()
    {
        var observed = new DispatchScopes();
        using var services = new ServiceCollection().AddSingleton(observed).AddSingleton(TimeProvider.System)
            .AddSingleton(Options.Create(new OutboxOptions { BatchSize = 3, PollIntervalSeconds = 60 }))
            .AddSingleton<IPayloadProtector>(new PayloadProtector())
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<OutboxDeliveryProcessor>>(NullLogger<OutboxDeliveryProcessor>.Instance)
            .AddScoped<OutboxDeliveryProcessor>()
            .AddScoped<IChannelSender>(_ => new ScopedSender(observed))
            .AddScoped<IOutboxDispatchService>(_ => new ScopedDispatchService(observed)).BuildServiceProvider();
        using var worker = new OutboxDispatcher(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            Options.Create(new OutboxOptions { BatchSize = 3, PollIntervalSeconds = 60 }), [new KeyRing(true)], NullLogger<OutboxDispatcher>.Instance);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        try { await observed.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken); }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.Equal(2, observed.Senders.Count);
        Assert.Same(observed.Senders[0], observed.Senders[1]);
        Assert.Equal(5, observed.Services.Count);
        Assert.Equal(5, observed.Services.Distinct().Count());
    }

    /// <summary>
    /// 0064 §11 (revisión de la B15): una copia con otro anillo no abre lo que las demás protegieron. Si tomara el aviso, gastaría
    /// sus intentos sin poder abrirlo hasta perderlo; no toma ninguno y lo deja para las copias que sí.
    /// </summary>
    [Fact]
    public async Task A_copy_whose_ring_does_not_match_claims_nothing()
    {
        var observed = new DispatchScopes();
        var ring = new KeyRing(false);
        using var services = new ServiceCollection().AddSingleton(observed).AddSingleton(TimeProvider.System)
            .AddSingleton(Options.Create(new OutboxOptions { BatchSize = 3, PollIntervalSeconds = 60 }))
            .AddSingleton<IPayloadProtector>(new PayloadProtector())
            .AddSingleton<Microsoft.Extensions.Logging.ILogger<OutboxDeliveryProcessor>>(NullLogger<OutboxDeliveryProcessor>.Instance)
            .AddScoped<OutboxDeliveryProcessor>()
            .AddScoped<IChannelSender>(_ => new ScopedSender(observed))
            .AddScoped<IOutboxDispatchService>(_ => new ScopedDispatchService(observed)).BuildServiceProvider();
        using var worker = new OutboxDispatcher(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            Options.Create(new OutboxOptions { BatchSize = 3, PollIntervalSeconds = 60 }), [new KeyRing(true), ring], NullLogger<OutboxDispatcher>.Instance);
        await worker.StartAsync(TestContext.Current.CancellationToken);
        try { await ring.Asked.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken); }
        finally { await worker.StopAsync(CancellationToken.None); }
        Assert.Equal(0, observed.Claims);
        Assert.Empty(observed.Services);
        Assert.Empty(observed.Senders);
    }
    private sealed class KeyRing(bool matches) : IKeyRingReadiness
    {
        public TaskCompletionSource Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<bool> MatchesAsync(CancellationToken cancellationToken) { Asked.TrySetResult(); return Task.FromResult(matches); }
    }
    private sealed class DispatchScopes
    {
        public List<IChannelSender> Senders { get; } = [];
        public List<IOutboxDispatchService> Services { get; } = [];
        public int Claims { get; set; }
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class ScopedSender(DispatchScopes observed) : IChannelSender
    {
        public string Key => "email";
        public Task SendAsync(string payload, CancellationToken ct) { observed.Senders.Add(this); return Task.CompletedTask; }
    }
    private sealed class PayloadProtector : IPayloadProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }
    private sealed class ScopedDispatchService(DispatchScopes observed) : IOutboxDispatchService
    {
        public Task<Result<OutboxDispatchClaim?>> ClaimNextAsync(IReadOnlyCollection<string> channels, CancellationToken ct)
        {
            observed.Services.Add(this);
            if (++observed.Claims == 3) { observed.Completed.TrySetResult(); return Task.FromResult(Result.Success<OutboxDispatchClaim?>(null)); }
            return Task.FromResult(Result.Success<OutboxDispatchClaim?>(new(Guid.NewGuid(), Guid.NewGuid(), "email", "payload")));
        }
        public Task<Result> CompleteAsync(Guid messageId, Guid leaseId, OutboxDeliveryReceipt receipt, CancellationToken ct)
        { observed.Services.Add(this); return Task.FromResult(Result.Success()); }
        public Task<Result> ReleaseAsync(Guid messageId, Guid leaseId, CancellationToken ct)
        { observed.Services.Add(this); return Task.FromResult(Result.Success()); }
    }
}
