using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

internal sealed class LegalAcceptanceDeletionParticipant(ApplicationDbContext database) : AccountDeletionParticipant
{
    public override async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not null) return;
        database.RequireTransaction();
        foreach (var acceptance in await database.LegalAcceptances.Where(row => row.UserId == context.UserId).ToArrayAsync(ct))
            acceptance.ClearPersonalMetadata();
    }
}
