using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Conversaciones de la organización del claim y sus mensajes. Exige la transacción del caso de uso.</summary>
public interface IWhatsAppChatRepository
{
    /// <summary>Serializa la conversación de un número con un remitente, aunque todavía no exista.</summary>
    Task LockAsync(Guid channelId, string waId, CancellationToken ct);
    Task<WhatsAppChat?> GetAsync(Guid channelId, string waId, WhatsAppInbox inbox, CancellationToken ct);
    /// <summary>Una conversación de la bandeja, bajo su lock de fila; con versión, la exige.</summary>
    Task<WhatsAppChat?> GetForUpdateAsync(Guid id, WhatsAppInbox inbox, uint? expectedVersion, CancellationToken ct);
    Task<bool> HasInboundAsync(string wamid, CancellationToken ct);
    Task<WhatsAppChatMessage?> GetOutboundAsync(Guid outboxMessageId, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppChat>> ListByTeamAsync(Guid teamId, CancellationToken ct);
    void Add(WhatsAppChat chat);
    void Add(WhatsAppChatMessage message);
}
