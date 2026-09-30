using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

internal sealed class LoginMethodReader(ApplicationDbContext context) : ILoginMethodReader
{
    public async Task<IReadOnlyList<LoginMethodRow>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.LoginMethods.AsNoTracking()
            .Where(method => method.UserId == userId)
            .OrderByDescending(method => method.IsPrimary)
            .ThenBy(method => method.Type)
            .ThenBy(method => method.Id)
            .Select(method => new LoginMethodRow(method.Id, method.Type, method.Value,
                method.IsPrimary, method.VerifiedAtUtc, method.ManagedByTenantId, method.ContactEmail))
            .ToArrayAsync(cancellationToken);
}
