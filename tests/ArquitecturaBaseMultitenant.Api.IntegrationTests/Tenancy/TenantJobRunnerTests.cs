using System.Collections.Concurrent;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que cada organización reciba un alcance y contexto nuevos. Un fallo debe liberar su alcance
/// antes de continuar y no frenar a las organizaciones siguientes; la cancelación pedida sí se propaga.
/// </summary>
public sealed class TenantJobRunnerTests
{
    [Fact]
    public async Task Every_tenant_gets_a_fresh_scope_and_only_its_own_context()
    {
        using var provider = CreateProvider();
        var runner = provider.GetRequiredService<TenantJobRunner>();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var seen = new List<(ITenantContext Context, Guid TenantId)>();

        await runner.RunAsync(ids, (scope, ct) =>
        {
            var context = scope.GetRequiredService<ITenantContext>();
            Assert.Equal(ids[seen.Count], context.RequiredTenantId);
            seen.Add((context, context.RequiredTenantId));
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(ids, seen.Select(item => item.TenantId));
        Assert.Equal(3, seen.Select(item => item.Context).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.All(seen, item => Assert.Null(item.Context.TenantId));
    }

    [Fact]
    public async Task A_failed_job_disposes_its_scope_before_the_next_run()
    {
        using var provider = CreateProvider();
        var runner = provider.GetRequiredService<TenantJobRunner>();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        ITenantContext? failedContext = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunForTenantAsync(first, (scope, ct) =>
            {
                failedContext = scope.GetRequiredService<ITenantContext>();
                Assert.Equal(first, failedContext.RequiredTenantId);
                throw new InvalidOperationException("Expected test failure.");
            }, TestContext.Current.CancellationToken));

        Assert.NotNull(failedContext);
        Assert.Null(failedContext.TenantId);
        await runner.RunForTenantAsync(second, (scope, ct) =>
        {
            var context = scope.GetRequiredService<ITenantContext>();
            Assert.NotSame(failedContext, context);
            Assert.Equal(second, context.RequiredTenantId);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_failed_tenant_is_logged_and_does_not_stop_following_tenants()
    {
        using var logs = new RecordingLoggerProvider();
        using var provider = CreateProvider(logs);
        var runner = provider.GetRequiredService<TenantJobRunner>();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        ITenantContext? failedContext = null;
        var succeeded = new List<(ITenantContext Context, Guid TenantId)>();

        await runner.RunAsync([first, second, third], (scope, ct) =>
        {
            var context = scope.GetRequiredService<ITenantContext>();
            if (context.RequiredTenantId == first)
            {
                failedContext = context;
                throw new InvalidOperationException("Sensitive failure detail must not leak.");
            }

            succeeded.Add((context, context.RequiredTenantId));
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(failedContext);
        Assert.Null(failedContext.TenantId);
        Assert.Equal([second, third], succeeded.Select(item => item.TenantId));
        Assert.All(succeeded, item =>
        {
            Assert.NotSame(failedContext, item.Context);
            Assert.Null(item.Context.TenantId);
        });
        Assert.Equal(2, succeeded.Select(item => item.Context).Distinct(ReferenceEqualityComparer.Instance).Count());

        var error = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Null(error.Exception);
        Assert.Contains(nameof(TenantJobRunner), error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Sensitive failure detail must not leak.", error.Message, StringComparison.Ordinal);
        Assert.Contains($"TenantId:{first}", error.Scopes);
    }

    [Fact]
    public async Task Cancellation_propagates_and_stops_following_tenants()
    {
        using var logs = new RecordingLoggerProvider();
        using var provider = CreateProvider(logs);
        var runner = provider.GetRequiredService<TenantJobRunner>();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var seen = new List<Guid>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync([first, second, third], (scope, ct) =>
            {
                var tenantId = scope.GetRequiredService<ITenantContext>().RequiredTenantId;
                seen.Add(tenantId);
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }, cts.Token));

        Assert.Equal([first], seen);
        Assert.DoesNotContain(logs.Entries, entry => entry.Level == LogLevel.Error);
    }

    private static ServiceProvider CreateProvider(RecordingLoggerProvider? logs = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            if (logs is not null) builder.AddProvider(logs);
        });
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantScope>(provider => provider.GetRequiredService<TenantContext>());
        services.AddBackgroundJobs();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception, IReadOnlyList<string> Scopes);

    private sealed class RecordingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();
        private IExternalScopeProvider? _scopeProvider;

        public IReadOnlyList<LogEntry> Entries => [.. _entries];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(RecordingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
                owner._scopeProvider?.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var scopes = new List<string>();
                owner._scopeProvider?.ForEachScope((scope, list) => list.Add(scope?.ToString() ?? string.Empty), scopes);
                owner._entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception, scopes));
            }
        }
    }
}
