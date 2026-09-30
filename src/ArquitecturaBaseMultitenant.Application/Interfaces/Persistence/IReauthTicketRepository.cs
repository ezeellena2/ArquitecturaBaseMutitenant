using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

public interface IReauthTicketRepository
{
    Task<Guid?> GetUserIdByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<ReauthTicket?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);
    void Add(ReauthTicket ticket);
}
