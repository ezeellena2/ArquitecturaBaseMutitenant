using ArquitecturaBaseMultitenant.Application.Models.Messaging;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>
/// Encola el payload de un canal registrado dentro de la transacción del caso de uso. Devuelve el identificador del mensaje,
/// con el que un módulo une después lo que informa el proveedor. Las opciones dicen su destino, su prioridad y
/// hasta cuándo sirve (ADR 0066).
/// </summary>
public interface IOutbox
{
    Guid Enqueue(string channel, string payload, Guid? userId = null, OutboxEnqueueOptions? options = null);

    /// <summary>
    /// Saca de la cola, dentro de la transacción del caso de uso, los mensajes que todavía no salieron ni están reservados, y
    /// devuelve cuáles sacó. Lo que otra copia está reservando se saltea sin esperar. Lo usa quien los encoló para retomarlos
    /// después, como una difusión que queda en pausa.
    /// </summary>
    Task<IReadOnlySet<Guid>> WithdrawAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct);
}
