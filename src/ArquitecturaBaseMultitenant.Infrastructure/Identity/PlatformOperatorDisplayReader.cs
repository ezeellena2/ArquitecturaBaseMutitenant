using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>
/// El nombre y la copia del correo principal de las cuentas operadoras pedidas; nada de las personas ni de las cuentas cuya
/// baja empezó.
/// </summary>
internal sealed class PlatformOperatorDisplayReader(ApplicationDbContext context) : IPlatformOperatorDisplayReader
{
    public async Task<IReadOnlyList<PlatformOperatorDisplayRow>> ListAsync(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0) return [];
        return await context.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.IsPlatformOperator && user.DeletedAtUtc == null
                && user.DeletionLeaseId == null)
            .Select(user => new PlatformOperatorDisplayRow(user.Id, user.DisplayName,
                user.Email == null ? null : Email.Create(user.Email).Value))
            .ToArrayAsync(cancellationToken);
    }
}
