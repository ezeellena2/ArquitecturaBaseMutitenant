using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>
/// Las difusiones de la organización del claim con sus resultados. El estado de cada destinatario que salió se lee del
/// envío (outbox y la fila «accepted» de Meta): enviado, entregado, leído o fallido; respondió y se dio de baja, de la fila.
/// </summary>
public interface IWhatsAppBroadcastReader
{
    Task<PagedResult<WhatsAppBroadcastResponse>> ListAsync(WhatsAppBroadcastListRequest request, CancellationToken ct);
    Task<int> CountScheduledAsync(CancellationToken ct);
    Task<WhatsAppBroadcastResponse?> FindAsync(Guid id, CancellationToken ct);
    Task<PagedResult<WhatsAppRecipientResponse>> ListRecipientsAsync(Guid broadcastId, WhatsAppBroadcastDetailRequest request, CancellationToken ct);
    /// <summary>Las difusiones que le salieron a un contacto, la más nueva primero.</summary>
    Task<PagedResult<WhatsAppContactBroadcastResponse>> ListForContactAsync(Guid contactId, PagedRequest request, CancellationToken ct);
    /// <summary>La difusión que más tiempo lleva esperando para salir, o null.</summary>
    Task<WhatsAppDueBroadcast?> FindNextDueAsync(DateTime nowUtc, CancellationToken ct);
    /// <summary>A cuántos contactos distintos les salió una difusión desde esa fecha (el tope de Meta es por 24 horas).</summary>
    Task<int> CountContactsSentSinceAsync(DateTime sinceUtc, CancellationToken ct);
}

public sealed record WhatsAppDueBroadcast(Guid Id, Guid TemplateId, IReadOnlyList<string> Variables);
