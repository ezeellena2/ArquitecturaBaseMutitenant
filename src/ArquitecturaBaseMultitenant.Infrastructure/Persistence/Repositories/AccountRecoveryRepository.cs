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

    public Task<AccountRecoveryRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.AccountRecoveryRequests.SingleOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountRecoveryRequest>> ListAwaitingAccountNoticeAsync(int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        context.RequireTransaction();
        return await context.AccountRecoveryRequests.FromSqlInterpolated($"""
            SELECT *, xmin FROM platform."AccountRecoveryRequests"
            WHERE "AccountNotifiedAtUtc" IS NULL
            ORDER BY "CreatedAtUtc", "Id"
            LIMIT {limit}
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
    }
}
