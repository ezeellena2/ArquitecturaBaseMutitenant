using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Pedidos globales de recuperación; el índice único por código impide registrar dos con el mismo.</summary>
internal sealed class AccountRecoveryRepository(ApplicationDbContext context) : IAccountRecoveryRepository
{
    public Task<bool> ExistsForLoginCodeAsync(Guid loginCodeId, CancellationToken cancellationToken) =>
        context.AccountRecoveryRequests.AnyAsync(request => request.LoginCodeId == loginCodeId, cancellationToken);

    public void Add(AccountRecoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        context.RequireTransaction();
        context.AccountRecoveryRequests.Add(request);
    }
}
