using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Los equipos de operadores de Plataforma. Exige la transacción del caso de uso.</summary>
public interface IPlatformWhatsAppTeamRepository
{
    Task<IReadOnlyList<PlatformWhatsAppTeam>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<PlatformWhatsAppTeam?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct);
    void Add(PlatformWhatsAppTeam team);
    void Remove(PlatformWhatsAppTeam team);
}
