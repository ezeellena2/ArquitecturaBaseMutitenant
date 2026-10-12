using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;

/// <summary>Captura en memoria para verificar secretos; nunca publica ni reenvía los mensajes.</summary>
internal sealed class AssistantTurnTestLogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();
    internal void Clear() => _messages.Clear();
    internal IReadOnlyList<string> Snapshot() => _messages.ToArray();
    public ILogger CreateLogger(string categoryName) => new Logger(_messages);
    public void Dispose() { }

    private sealed class Logger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue(formatter(state, exception));
            if (exception is not null) messages.Enqueue(exception.ToString());
        }
    }
}
