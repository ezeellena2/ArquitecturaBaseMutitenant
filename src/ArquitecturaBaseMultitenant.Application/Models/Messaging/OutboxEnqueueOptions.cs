using ArquitecturaBaseMultitenant.Domain.Messaging;
namespace ArquitecturaBaseMultitenant.Application.Models.Messaging;

/// <summary>
/// Cómo se encola un mensaje (ADR 0066): el destino que lo ordena, su prioridad y el instante desde el que ya no sirve.
/// </summary>
/// <param name="Destination">
/// El destino en claro que da el canal (por ejemplo, el número que manda y el que recibe): lo anterior del mismo destino sale
/// primero. La cola guarda solo su HMAC y nunca lo registra. Sin destino, no espera a nadie.
/// </param>
/// <param name="Priority">Lo que contesta sale antes que los avisos; por defecto, un aviso.</param>
/// <param name="DiscardAfterUtc">Desde este instante (UTC) no se manda: se descarta, como un código que ya venció.</param>
/// <param name="TenantId">Organización que origina el envío para repartir la cola con equidad; si falta, se toma del contexto activo.</param>
public sealed record OutboxEnqueueOptions(
    string? Destination = null,
    OutboxPriority Priority = OutboxPriority.Notice,
    DateTime? DiscardAfterUtc = null,
    Guid? TenantId = null)
{
    public override string ToString() => $"OutboxEnqueueOptions ({Priority})";
}
