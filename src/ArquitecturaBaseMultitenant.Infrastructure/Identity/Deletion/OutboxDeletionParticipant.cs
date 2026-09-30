using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

internal sealed class OutboxDeletionParticipant(ApplicationDbContext database) : AccountDeletionParticipant
{
    public override Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => ExecuteAsync(context, ct);
    public override async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not null) return;
        database.RequireTransaction();
        foreach (var message in await database.OutboxMessages.Where(row => row.UserId == context.UserId
            && row.Status != OutboxStatus.Sent && row.Status != OutboxStatus.Cancelled).ToArrayAsync(ct)) message.Cancel();
    }
}
