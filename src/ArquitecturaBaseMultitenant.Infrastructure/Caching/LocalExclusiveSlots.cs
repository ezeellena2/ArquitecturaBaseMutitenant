using System.Collections.Concurrent;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// Los lugares exclusivos de esta copia: sin Redis configurado (el exportador OpenAPI y los proveedores técnicos) y mientras
/// Redis no responde, el lugar es de la copia (ADR 0058). Se suelta al descartarlo y muere con el proceso.
/// </summary>
internal sealed class LocalExclusiveSlots : IExclusiveSlots
{
    private readonly ConcurrentDictionary<(string Scope, Guid Holder), byte> _taken = new();

    public Task<IAsyncDisposable?> TryTakeAsync(string scope, Guid holderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        var key = (scope, holderId);
        return Task.FromResult<IAsyncDisposable?>(_taken.TryAdd(key, 0) ? new Slot(_taken, key) : null);
    }

    private sealed class Slot(ConcurrentDictionary<(string Scope, Guid Holder), byte> taken, (string Scope, Guid Holder) key) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) taken.TryRemove(key, out _);
            return ValueTask.CompletedTask;
        }
    }
}
