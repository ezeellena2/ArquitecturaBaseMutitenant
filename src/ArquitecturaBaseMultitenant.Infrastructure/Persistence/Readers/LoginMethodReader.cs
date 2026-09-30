using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyecta los métodos de ingreso de la cuenta solicitada para mostrarlos y administrarlos. No entrega entidades seguidas por EF ni autoriza por sí mismo el acceso.</summary>
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
