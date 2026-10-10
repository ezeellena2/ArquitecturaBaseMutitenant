using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
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

    public void AddAccountNotice(AccountRecoveryNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        context.RequireTransaction();
        context.AccountRecoveryNotices.Add(notice);
    }

    public Task<AccountRecoveryIdentityHint?> GetIdentityHintAsync(Guid id, CancellationToken cancellationToken) =>
        context.AccountRecoveryRequests.AsNoTracking().Where(request => request.Id == id)
            .Select(request => new AccountRecoveryIdentityHint(request.UserId, request.NewMethodType, request.NewMethodValue))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<AccountRecoveryRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.AccountRecoveryRequests.FromSqlInterpolated($"""
            SELECT *, xmin FROM platform."AccountRecoveryRequests" WHERE "Id" = {id} FOR NO KEY UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountRecoveryRequest>> ListAwaitingAccountNoticeAsync(int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        context.RequireTransaction();
        var candidates = await context.AccountRecoveryRequests.FromSqlInterpolated($"""
            SELECT request.*, request.xmin FROM platform."AccountRecoveryRequests" AS request
            WHERE NOT EXISTS (SELECT 1 FROM platform."AccountRecoveryNotices" AS notice WHERE notice."Id" = request."Id")
            ORDER BY request."CreatedAtUtc", request."Id"
            LIMIT {limit}
            FOR UPDATE OF request SKIP LOCKED
            """).ToListAsync(cancellationToken);
        // READ COMMITTED: otra tanda puede haber confirmado una marca después del snapshot del SELECT anterior,
        // sin editar el pedido. Releer las marcas con los locks ya tomados evita preparar otra vez ese aviso.
        var ids = candidates.Select(request => request.Id).ToArray();
        var completed = await context.AccountRecoveryNotices.Where(notice => ids.Contains(notice.Id))
            .Select(notice => notice.Id).ToArrayAsync(cancellationToken);
        return candidates.Where(request => !completed.Contains(request.Id)).ToArray();
    }
}
