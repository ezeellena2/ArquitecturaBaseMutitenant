using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Serializa la creación o recuperación del espacio Personal de una cuenta mediante un advisory lock. Se adquiere dentro de la transacción del alta.</summary>
internal sealed class PersonalSpaceLock(ApplicationDbContext context) : IPersonalSpaceLock
{
    public Task LockAsync(Guid userId, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.PersonalSpace(userId)], cancellationToken);
}
