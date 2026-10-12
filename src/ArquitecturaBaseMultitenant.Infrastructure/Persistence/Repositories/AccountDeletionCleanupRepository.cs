using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara la limpieza EF de la baja sin decidir estados, alcance ni reglas de salida.</summary>
internal sealed class AccountDeletionCleanupRepository(ApplicationDbContext database) : IAccountDeletionCleanupRepository
{
    public void RequireTransaction() => database.RequireTransaction();

    public Task<Member?> GetMemberAsync(Guid userId, CancellationToken ct)
    {
        database.RequireTransaction();
        return database.Members.SingleOrDefaultAsync(row => row.UserId == userId, ct);
    }

    public Task<Tenant> GetPersonalSpaceAsync(Guid tenantId, CancellationToken ct)
    {
        database.RequireTransaction();
        return database.Tenants.SingleAsync(row => row.Id == tenantId && row.Kind == TenantKind.Personal, ct);
    }

    public async Task RemovePersonalDataAsync(CancellationToken ct)
    {
        database.RequireTransaction();
        database.TenantSettings.RemoveRange(await database.TenantSettings.ToArrayAsync(ct));
        database.Members.RemoveRange(await database.Members.ToArrayAsync(ct));
    }

    public async Task RemoveRecoveryRequestsAsync(Guid userId, CancellationToken ct)
    {
        database.RequireTransaction();
        database.AccountRecoveryRequests.RemoveRange(await database.AccountRecoveryRequests
            .Where(request => request.UserId == userId).ToArrayAsync(ct));
    }
}
