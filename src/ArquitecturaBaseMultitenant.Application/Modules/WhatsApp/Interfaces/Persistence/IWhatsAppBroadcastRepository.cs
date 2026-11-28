using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Las difusiones de la organización del claim y sus destinatarios. Exige la transacción del caso de uso.</summary>
public interface IWhatsAppBroadcastRepository
{
    /// <summary>La difusión con el lock de su fila (la cancelación y el envío no se cruzan); con versión, la exige.</summary>
    Task<WhatsAppBroadcast?> GetForUpdateAsync(Guid id, uint? expectedVersion, CancellationToken ct);
    /// <summary>Los primeros destinatarios que todavía no salieron.</summary>
    Task<IReadOnlyList<WhatsAppBroadcastRecipient>> ListPendingAsync(Guid broadcastId, int limit, CancellationToken ct);
    /// <summary>
    /// Los primeros destinatarios cuyo mensaje sigue en la cola sin reservar: los que una difusión en pausa le saca. Quien los
    /// saca de verdad es la cola, bajo su lock.
    /// </summary>
    Task<IReadOnlyList<WhatsAppBroadcastRecipient>> ListQueuedAsync(Guid broadcastId, int limit, CancellationToken ct);
    /// <summary>El último destinatario de ese contacto al que le salió una difusión (o que ya respondió) desde esa fecha.</summary>
    Task<WhatsAppBroadcastRecipient?> FindLatestSentAsync(Guid customerId, DateTime sinceUtc, CancellationToken ct);
    /// <summary>El destinatario asociado a un mensaje del outbox, para anotar el motivo de un fallo informado por webhook.</summary>
    Task<WhatsAppBroadcastRecipient?> GetByOutboxMessageAsync(Guid outboxMessageId, CancellationToken ct);
    void Add(WhatsAppBroadcast broadcast);
    void AddRecipients(IEnumerable<WhatsAppBroadcastRecipient> recipients);
}
