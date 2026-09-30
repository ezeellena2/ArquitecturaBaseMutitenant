using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class ReauthTicketRepository(ApplicationDbContext context) : IReauthTicketRepository
{
    public Task<Guid?> GetUserIdByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.ReauthTickets.AsNoTracking().Where(ticket => ticket.TokenHash == tokenHash)
            .Select(ticket => (Guid?)ticket.UserId).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<ReauthTicket?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.ReauthTickets.SingleOrDefaultAsync(ticket => ticket.TokenHash == tokenHash, cancellationToken);
    }

    public void Add(ReauthTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        context.RequireTransaction();
        context.ReauthTickets.Add(ticket);
    }
}
