using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class PersonalSpaceLock(ApplicationDbContext context) : IPersonalSpaceLock
{
    public Task LockAsync(Guid userId, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.PersonalSpace(userId)], cancellationToken);
}
