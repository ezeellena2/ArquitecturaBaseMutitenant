using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

/// <summary>Cancela avisos pendientes de una cuenta al pedir o ejecutar su baja, para que no se envíen después. Actúa sobre el tramo global de la transacción del worker.</summary>
internal sealed class OutboxDeletionParticipant(ApplicationDbContext database) : AccountDeletionParticipant
{
    public override Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => ExecuteAsync(context, ct);
    public override async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not null) return;
        database.RequireTransaction();
        var messages = await database.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM platform."OutboxMessages"
            WHERE "UserId" = {context.UserId} AND "Status" NOT IN ('Sent', 'Cancelled')
            ORDER BY "Id" FOR UPDATE
            """).ToArrayAsync(ct);
        foreach (var message in messages)
        {
            // El DbContext puede tener Pending y un lease anteriores al bloqueo SQL.
            await database.Entry(message).ReloadAsync(ct);
            message.Cancel();
        }
    }
}
