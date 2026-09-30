using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Busca y guarda tickets de reautenticación por hash para confirmar cambios sensibles sin persistir el token claro.</summary>
public interface IReauthTicketRepository
{
    Task<Guid?> GetUserIdByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<ReauthTicket?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);
    void Add(ReauthTicket ticket);
}
