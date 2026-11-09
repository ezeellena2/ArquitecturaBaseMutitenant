using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class NotificationReadRepository(ApplicationDbContext context) : INotificationReadRepository
{
    public void Add(NotificationRead read)
    {
        context.RequireTransaction();
        context.NotificationReads.Add(read);
    }

    public async Task RemoveForAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        context.NotificationReads.RemoveRange(await context.NotificationReads
            .Where(read => read.UserId == userId).ToArrayAsync(cancellationToken));
    }

    /// <summary>
    /// Sin <c>FOR UPDATE</c>: mt_app no edita estas filas, solo las lee, suma y borra. Dos limpiezas a la vez se esperan
    /// en el borrado y la segunda saltea lo que ya no está.
    /// </summary>
    public Task<int> DeleteExpiredAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.Database.ExecuteSqlAsync($"""
            DELETE FROM identity."NotificationReads" WHERE "Id" IN (
                SELECT "Id" FROM identity."NotificationReads" WHERE "ReadAtUtc" < {beforeUtc}
                ORDER BY "ReadAtUtc" LIMIT {batchSize})
            """, cancellationToken);
    }
}
