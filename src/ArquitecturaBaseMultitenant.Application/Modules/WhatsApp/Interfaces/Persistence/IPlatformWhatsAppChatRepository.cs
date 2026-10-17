using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Las consultas al número de Plataforma y sus mensajes. Exige la transacción del caso de uso.</summary>
public interface IPlatformWhatsAppChatRepository
{
    Task LockAsync(Guid channelId, string waId, CancellationToken ct);
    Task<PlatformWhatsAppChat?> GetAsync(Guid channelId, string waId, CancellationToken ct);
    Task<PlatformWhatsAppChat?> GetForUpdateAsync(Guid id, uint? expectedVersion, CancellationToken ct);
    Task<bool> HasInboundAsync(string wamid, CancellationToken ct);
    Task<PlatformWhatsAppChatMessage?> GetOutboundAsync(Guid outboxMessageId, CancellationToken ct);
    Task<IReadOnlyList<PlatformWhatsAppChat>> ListByTeamAsync(Guid teamId, CancellationToken ct);
    Task<IReadOnlyList<PlatformWhatsAppChat>> ListTakenByAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<PlatformWhatsAppChatMessage>> ListAuthoredByAsync(Guid userId, CancellationToken ct);
    void Add(PlatformWhatsAppChat chat);
    void Add(PlatformWhatsAppChatMessage message);
}
