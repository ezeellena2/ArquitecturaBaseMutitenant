using System.Diagnostics.Metrics;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>
/// Lo que sale de la cola, por canal: los enviados (el proveedor los aceptó) y los descartados (vencieron antes de salir). La
/// única etiqueta es el canal: nunca la organización, el destino ni un id (<c>logs.md</c>).
/// </summary>
internal sealed class OutboxMetrics
{
    internal const string MeterName = "ArquitecturaBaseMultitenant.Messaging";
    internal const string SentName = "arquitecturabasemt.outbox.sent";
    internal const string DiscardedName = "arquitecturabasemt.outbox.discarded";

    private readonly Counter<long> _sent;
    private readonly Counter<long> _discarded;

    public OutboxMetrics(IMeterFactory meters)
    {
        ArgumentNullException.ThrowIfNull(meters);
        var meter = meters.Create(MeterName);
        _sent = meter.CreateCounter<long>(SentName);
        _discarded = meter.CreateCounter<long>(DiscardedName);
    }

    internal void Sent(string channel) => _sent.Add(1, new KeyValuePair<string, object?>("channel", channel));

    internal void Discarded(string channel) => _discarded.Add(1, new KeyValuePair<string, object?>("channel", channel));
}
