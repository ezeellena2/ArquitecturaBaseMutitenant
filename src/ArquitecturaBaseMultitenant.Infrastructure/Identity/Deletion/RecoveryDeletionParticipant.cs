using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

/// <summary>
/// Cierra los pedidos de «Recuperar mi cuenta» de una cuenta eliminada (ADR 0035): los borra, con sus datos
/// personales, en el tramo global de la transacción del worker. Los SecurityEvent de cada decisión se conservan.
/// </summary>
internal sealed class RecoveryDeletionParticipant(ApplicationDbContext database) : AccountDeletionParticipant
{
    public override async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not null) return;
        database.RequireTransaction();
        database.AccountRecoveryRequests.RemoveRange(await database.AccountRecoveryRequests
            .Where(request => request.UserId == context.UserId).ToArrayAsync(ct));
    }
}
