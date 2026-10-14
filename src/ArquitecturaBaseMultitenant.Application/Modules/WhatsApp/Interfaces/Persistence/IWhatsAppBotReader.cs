using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Lee, sin seguirlo, el bot configurado de un número de la organización del claim o el de Plataforma.</summary>
public interface IWhatsAppBotReader
{
    Task<WhatsAppBot?> FindAsync(Guid channelId, CancellationToken ct);
    Task<PlatformWhatsAppBot?> FindPlatformAsync(CancellationToken ct);
}
