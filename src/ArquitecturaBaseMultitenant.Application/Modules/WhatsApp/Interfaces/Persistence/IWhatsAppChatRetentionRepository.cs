using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>
/// El contenido vencido de los chats (decisión 272), en lotes con <c>FOR UPDATE SKIP LOCKED</c>: dos copias que purgan a la vez
/// no toman la misma fila. Lo de la organización es solo el de la organización del alcance (RLS y su columna), también su copia
/// vinculada; lo de Plataforma, el de su propio número. Exige la transacción del caso de uso. Lo que devuelve se guarda con su
/// contenido escrito, aunque se haya leído vacío porque el anillo no lo abre (ADR 0065): así vaciarlo lo vacía en la base.
/// </summary>
public interface IWhatsAppChatRetentionRepository
{
    /// <summary>Los mensajes con texto o autor creados hasta <paramref name="beforeUtc"/>.</summary>
    Task<IReadOnlyList<WhatsAppChatMessage>> ListExpiredMessagesAsync(DateTime beforeUtc, int limit, CancellationToken ct);
    /// <summary>Las conversaciones con vista previa cuyo último mensaje es de hasta <paramref name="beforeUtc"/>.</summary>
    Task<IReadOnlyList<WhatsAppChat>> ListExpiredPreviewsAsync(DateTime beforeUtc, int limit, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppConversationLink>> ListExpiredLinkPreviewsAsync(DateTime beforeUtc, int limit, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppLinkedMessage>> ListExpiredLinkedMessagesAsync(DateTime beforeUtc, int limit, CancellationToken ct);
    Task<IReadOnlyList<PlatformWhatsAppChatMessage>> ListExpiredPlatformMessagesAsync(DateTime beforeUtc, int limit, CancellationToken ct);
    Task<IReadOnlyList<PlatformWhatsAppChat>> ListExpiredPlatformPreviewsAsync(DateTime beforeUtc, int limit, CancellationToken ct);
}
