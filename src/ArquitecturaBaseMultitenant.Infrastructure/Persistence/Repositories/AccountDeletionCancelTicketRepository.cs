using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;
namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;
/// <summary>Busca y guarda las pruebas de cancelación dentro de la transacción del caso de uso.</summary>
internal sealed class AccountDeletionCancelTicketRepository(ApplicationDbContext context) : IAccountDeletionCancelTicketRepository
{
    public Task<Guid?> GetUserIdByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.AccountDeletionCancelTickets.AsNoTracking().Where(ticket => ticket.TokenHash == tokenHash)
            .Select(ticket => (Guid?)ticket.UserId).SingleOrDefaultAsync(cancellationToken);
    }
    public Task<AccountDeletionCancelTicket?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.AccountDeletionCancelTickets.SingleOrDefaultAsync(ticket => ticket.TokenHash == tokenHash, cancellationToken);
    }
    public void Add(AccountDeletionCancelTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        context.RequireTransaction();
        context.AccountDeletionCancelTickets.Add(ticket);
    }
}
