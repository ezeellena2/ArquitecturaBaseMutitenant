using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Los equipos de la organización del claim. Exige la transacción del caso de uso.</summary>
public interface IWhatsAppTeamRepository
{
    /// <summary>Los equipos con esos identificadores; los ajenos o inexistentes no vuelven.</summary>
    Task<IReadOnlyList<WhatsAppTeam>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    void Add(WhatsAppTeam team);
}
