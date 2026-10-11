using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

internal sealed class UserDisplayNameReader(ApplicationDbContext context) : IUserDisplayNameReader
{
    public async Task<IReadOnlyDictionary<Guid, string>> FindAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        await context.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.DeletedAtUtc == null && user.DeletionLeaseId == null
                && user.DisplayName != null && user.DisplayName != string.Empty)
            .Select(user => new { user.Id, user.DisplayName })
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName!, cancellationToken);
}
