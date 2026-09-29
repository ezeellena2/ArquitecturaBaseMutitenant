using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

internal sealed class LoginCodeRepository(ApplicationDbContext context) : ILoginCodeRepository
{
    public Task LockDestinationAsync(LoginCodeDestination destination, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.LoginCode(destination)], cancellationToken);

    public Task<LoginCode?> GetLatestAsync(LoginCodeDestination destination, LoginCodePurpose purpose,
        Guid? requestedByUserId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.LoginCodes
            .Where(code => code.Destination == destination.Value && code.Purpose == purpose
                && code.RequestedByUserId == requestedByUserId && code.InvalidatedAtUtc == null)
            .OrderByDescending(code => code.CreatedAtUtc)
            .ThenBy(code => code.ConsumedAtUtc != null)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoginCode>> ListActiveAsync(LoginCodeDestination destination,
        LoginCodePurpose purpose, Guid? requestedByUserId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        var candidates = await context.LoginCodes
            .Where(code => code.Destination == destination.Value && code.Purpose == purpose
                && code.RequestedByUserId == requestedByUserId && code.ConsumedAtUtc == null
                && code.InvalidatedAtUtc == null)
            .ToListAsync(cancellationToken);
        return candidates.Where(code => code.IsActive(nowUtc)).ToArray();
    }

    public async Task<IReadOnlyList<DateTime>> ListRequestTimesSinceAsync(LoginCodeDestination destination,
        DateTime sinceUtc, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return await context.LoginCodes
            .Where(code => code.Destination == destination.Value && code.CreatedAtUtc > sinceUtc)
            .OrderBy(code => code.CreatedAtUtc)
            .Select(code => code.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);
    }

    public void Add(LoginCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        context.RequireTransaction();
        context.LoginCodes.Add(code);
    }
}
