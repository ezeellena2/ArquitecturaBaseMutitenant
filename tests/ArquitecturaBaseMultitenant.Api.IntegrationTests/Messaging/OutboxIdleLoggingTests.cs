using System.Collections.Concurrent;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Messaging;

[Collection(ApiTestGroup.Name)]
public sealed class OutboxIdleLoggingTests(ApiFactory factory)
{
    [Fact]
    public async Task Empty_dispatch_does_not_write_information_logs()
    {
        var logs = new CapturingLoggerProvider();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = host.CreateClient();
        await using var scope = host.Services.CreateAsyncScope();
        var dispatch = scope.ServiceProvider.GetRequiredService<IOutboxDispatchService>();

        var result = await dispatch.DispatchOnceAsync([new UnusedSender()],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.DoesNotContain(logs.Entries, entry =>
            entry.Category == typeof(OutboxDispatchService).FullName
            && entry.Level == LogLevel.Information);
    }

    private sealed class UnusedSender : IChannelSender
    {
        public string Key => "outbox-idle-test";

        public Task SendAsync(string payload, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(string Category, LogLevel Level)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Entries.Enqueue((category, logLevel));
        }
    }
}
