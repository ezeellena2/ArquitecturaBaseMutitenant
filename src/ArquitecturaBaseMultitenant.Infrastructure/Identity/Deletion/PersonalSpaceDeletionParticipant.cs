using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

internal sealed class PersonalSpaceDeletionParticipant(ApplicationDbContext database, ITenantContext tenantContext)
    : AccountDeletionParticipant
{
    public override async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantKind != TenantKind.Personal || context.TenantId is not { } tenantId) return;
        database.RequireTransaction();
        if (tenantContext.RequiredTenantId != tenantId) throw new InvalidOperationException("The personal deletion needs its tenant scope.");
        var tenant = await database.Tenants.SingleAsync(row => row.Id == tenantId && row.Kind == TenantKind.Personal, ct);
        if (tenant.Status != TenantStatus.Closed && tenant.Close().IsFailure)
            throw new InvalidOperationException("The personal space could not be closed.");
        database.TenantSettings.RemoveRange(await database.TenantSettings.ToArrayAsync(ct));
        database.Members.RemoveRange(await database.Members.ToArrayAsync(ct));
    }
}
