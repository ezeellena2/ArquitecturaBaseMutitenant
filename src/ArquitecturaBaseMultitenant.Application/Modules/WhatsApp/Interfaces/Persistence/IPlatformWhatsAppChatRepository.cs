using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Las consultas al número de Plataforma y sus mensajes. Exige la transacción del caso de uso.</summary>
public interface IPlatformWhatsAppChatRepository
{
    Task LockAsync(Guid channelId, string waId, CancellationToken ct);
    Task<PlatformWhatsAppChat?> GetAsync(Guid channelId, string waId, CancellationToken ct);
    Task<PlatformWhatsAppChat?> GetForUpdateAsync(Guid id, uint? expectedVersion, CancellationToken ct);
    Task<bool> HasInboundAsync(string wamid, CancellationToken ct);
    /// <summary>Si en la consulta salió una plantilla de un operador después de esa hora (B11): la respuesta sigue con él.</summary>
    Task<bool> HasOperatorTemplateSinceAsync(Guid chatId, DateTime sinceUtc, CancellationToken ct);
    Task<PlatformWhatsAppChatMessage?> GetOutboundAsync(Guid outboxMessageId, CancellationToken ct);
    Task<IReadOnlyList<PlatformWhatsAppChat>> ListByTeamAsync(Guid teamId, CancellationToken ct);
    Task<IReadOnlyList<PlatformWhatsAppChat>> ListTakenByAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<PlatformWhatsAppChatMessage>> ListAuthoredByAsync(Guid userId, CancellationToken ct);
    void Add(PlatformWhatsAppChat chat);
    void Add(PlatformWhatsAppChatMessage message);
    /// <summary>Un mensaje de esa consulta, para cambiarlo; null si es de otra.</summary>
    Task<PlatformWhatsAppChatMessage?> GetMessageAsync(Guid chatId, Guid messageId, CancellationToken ct);
}
