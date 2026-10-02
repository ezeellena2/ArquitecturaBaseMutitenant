using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>Lee sólo el estado de una identidad global.</summary>
internal sealed class UserStatusReader(ApplicationDbContext context) : IUserStatusReader
{
    public Task<UserStatus?> GetStatusAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (UserStatus?)user.Status)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> ListActiveAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0) return [];
        return await context.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.Status == UserStatus.Active)
            .Select(user => user.Id).Distinct().ToArrayAsync(cancellationToken);
    }
}
