using System.Threading.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

/// <summary>
/// Los lugares de las subidas del Inicio en esta copia (ADR 0064 §4), una instancia por copia: protege la memoria de la
/// copia, así que no se comparte entre copias ni es una cuota. Corren a la vez <c>ConcurrencyLimit</c>; con todas ocupadas,
/// esperan por orden de llegada hasta <c>QueueLimit</c>, y una organización ocupa como mucho <c>QueuePerOrganization</c>
/// de esos lugares, así no deja sin lugar a las otras. Lo que no entra se rechaza enseguida.
/// </summary>
internal sealed class AssistantUploadLimiter : IDisposable
{
    private readonly ConcurrencyLimiter _copy;
    private readonly int _perOrganization;
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, int> _waiting = [];

    public AssistantUploadLimiter(IOptions<AssistantOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var uploads = options.Value.Uploads;
        _perOrganization = uploads.QueuePerOrganization;
        _copy = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = uploads.ConcurrencyLimit,
            QueueLimit = uploads.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    /// <summary>
    /// Un lugar de la copia para la organización (<see cref="Guid.Empty"/> fuera de una: el lado personal y Plataforma, juntos),
    /// que se devuelve al descartarlo;
    /// null si la cola está llena o la organización ya ocupa sus lugares en ella.
    /// </summary>
    public async ValueTask<IDisposable?> TryAcquireAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var lease = _copy.AttemptAcquire();
        if (lease.IsAcquired) return lease;
        lease.Dispose();
        if (!TryWait(organizationId)) return null;
        try
        {
            lease = await _copy.AcquireAsync(1, cancellationToken);
        }
        finally
        {
            StopWaiting(organizationId);
        }
        if (lease.IsAcquired) return lease;
        lease.Dispose();
        return null;
    }

    public void Dispose() => _copy.Dispose();

    private bool TryWait(Guid organizationId)
    {
        lock (_gate)
        {
            var waiting = _waiting.GetValueOrDefault(organizationId);
            if (waiting >= _perOrganization) return false;
            _waiting[organizationId] = waiting + 1;
            return true;
        }
    }

    private void StopWaiting(Guid organizationId)
    {
        lock (_gate)
        {
            var waiting = _waiting[organizationId] - 1;
            if (waiting == 0) _waiting.Remove(organizationId);
            else _waiting[organizationId] = waiting;
        }
    }
}
