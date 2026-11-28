using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>
/// Los vínculos de conversaciones con cuentas (dato compartido): la organización los crea y les copia mensajes; la persona
/// los desvincula. Cada parte ve solo los suyos. Exige la transacción del caso de uso.
/// </summary>
public interface IWhatsAppConversationLinkRepository
{
    /// <summary>El vínculo vigente de una conversación de la organización, si hay.</summary>
    Task<WhatsAppConversationLink?> GetByChatAsync(Guid chatId, CancellationToken ct);
    /// <summary>Un vínculo visible para la parte activa.</summary>
    Task<WhatsAppConversationLink?> GetAsync(Guid id, CancellationToken ct);
    void Add(WhatsAppConversationLink link);
    void Add(WhatsAppLinkedMessage message);
    /// <summary>El número del chat en la organización activa, para comprobar si la cuenta vinculada sigue siendo su dueña.</summary>
    Task<string?> FindChatWaIdAsync(Guid chatId, CancellationToken ct);
    /// <summary>Los vínculos de esa persona, desde su espacio personal (para la baja de su cuenta).</summary>
    Task<IReadOnlyList<WhatsAppConversationLink>> ListForConsumerAsync(Guid consumerTenantId, CancellationToken ct);
    /// <summary>Borra los vínculos de esa cuenta para ese teléfono en su espacio personal, con sus copias.</summary>
    Task RemoveByAccountAndPhoneAsync(Guid consumerTenantId, Guid accountId, string waId, CancellationToken ct);
    /// <summary>Desvincular borra el vínculo y sus copias.</summary>
    void Remove(WhatsAppConversationLink link);
}
