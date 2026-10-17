using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>
/// Los avisos «Chats esperando» ya enviados: de la organización del claim o de Plataforma. Exige la transacción del caso de
/// uso; el lock serializa una vuelta del aviso por organización (o por Plataforma).
/// </summary>
public interface IWhatsAppTeamNoticeRepository
{
    Task LockAsync(bool platform, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppTeamNoticeBase>> ListAsync(bool platform, CancellationToken ct);
    void Add(WhatsAppTeamNoticeBase notice);
    void Remove(WhatsAppTeamNoticeBase notice);
}
