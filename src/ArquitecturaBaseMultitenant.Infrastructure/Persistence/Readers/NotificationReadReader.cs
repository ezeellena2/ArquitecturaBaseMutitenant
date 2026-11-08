using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class NotificationReadReader(ApplicationDbContext context) : INotificationReadReader
{
    public async Task<IReadOnlyList<string>> ListReadIdsAsync(Guid userId, IReadOnlyCollection<string> notificationIds,
        CancellationToken cancellationToken)
    {
        if (notificationIds.Count == 0) return [];
        string[] ids = [.. notificationIds];
        return await context.NotificationReads.AsNoTracking()
            .Where(read => read.UserId == userId && ids.Contains(read.NotificationId))
            .Select(read => read.NotificationId)
            .ToArrayAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid userId, string notificationId, CancellationToken cancellationToken) =>
        context.NotificationReads.AsNoTracking()
            .AnyAsync(read => read.UserId == userId && read.NotificationId == notificationId, cancellationToken);
}
