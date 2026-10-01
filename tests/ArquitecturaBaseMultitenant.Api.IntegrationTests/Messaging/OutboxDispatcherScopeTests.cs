using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

/// <summary>
/// Comprueba que un lote pueda reutilizar el sender pero abra un alcance de despacho por mensaje. Evita
/// compartir el estado transaccional entre envíos.
/// </summary>
public sealed class OutboxDispatcherScopeTests
{
    [Fact]
    public async Task Batch_reuses_sender_but_opens_a_new_dispatch_scope_for_each_message()
    {
        var observed = new DispatchScopes();
        using var services = new ServiceCollection()
            .AddSingleton(observed)
            .AddScoped<IChannelSender>(_ => new ScopedSender())
            .AddScoped<IOutboxDispatchService>(provider =>
                new ScopedDispatchService(provider.GetRequiredService<DispatchScopes>()))
            .BuildServiceProvider();
        using var worker = new OutboxDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            Options.Create(new OutboxOptions { BatchSize = 3, PollIntervalSeconds = 60 }),
            NullLogger<OutboxDispatcher>.Instance);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await observed.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.Equal(3, observed.Senders.Count);
        Assert.Same(observed.Senders[0], observed.Senders[1]);
        Assert.Same(observed.Senders[1], observed.Senders[2]);
        Assert.NotSame(observed.Services[0], observed.Services[1]);
        Assert.NotSame(observed.Services[1], observed.Services[2]);
    }

    private sealed class DispatchScopes
    {
        public List<IChannelSender> Senders { get; } = [];
        public List<IOutboxDispatchService> Services { get; } = [];
        public TaskCompletionSource Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ScopedSender : IChannelSender
    {
        public string Key => "email";
        public Task SendAsync(string payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ScopedDispatchService(DispatchScopes observed) : IOutboxDispatchService
    {
        public Task<Result<int>> DispatchOnceAsync(IReadOnlyCollection<IChannelSender> senders,
            CancellationToken cancellationToken)
        {
            observed.Senders.Add(Assert.Single(senders));
            observed.Services.Add(this);
            if (observed.Services.Count == 3)
            {
                observed.Completed.TrySetResult();
            }

            return Task.FromResult(Result.Success(observed.Services.Count < 3 ? 1 : 0));
        }
    }
}
