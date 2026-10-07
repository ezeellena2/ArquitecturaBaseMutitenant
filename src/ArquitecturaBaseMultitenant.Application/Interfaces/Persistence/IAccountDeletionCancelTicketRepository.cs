using ArquitecturaBaseMultitenant.Domain.Legal;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
/// <summary>Busca y guarda pruebas de cancelación de baja por hash, sin persistir el secreto claro.</summary>
public interface IAccountDeletionCancelTicketRepository
{
    Task<Guid?> GetUserIdByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<AccountDeletionCancelTicket?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);
    void Add(AccountDeletionCancelTicket ticket);
}
