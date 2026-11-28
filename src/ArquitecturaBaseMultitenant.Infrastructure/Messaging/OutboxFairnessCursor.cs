using System.Collections.Concurrent;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>
/// Recuerda, por prioridad y en esta instancia, la última organización despachada para rotar en round-robin y evitar
/// que una difusión grande bloquee los avisos de otras organizaciones.
/// </summary>
internal sealed class OutboxFairnessCursor
{
    private readonly ConcurrentDictionary<OutboxPriority, Guid> _lastByPriority = new();

    internal Guid? LastFor(OutboxPriority priority) =>
        _lastByPriority.TryGetValue(priority, out var tenantId) ? tenantId : null;

    internal void Record(OutboxPriority priority, Guid? tenantId) =>
        _lastByPriority[priority] = tenantId ?? Guid.Empty;
}
