using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Un lock por retención; el reemplazo de la copia y la marca durable se confirman en la misma UoW.</summary>
internal sealed class ConsumerDataRetentionRepository(ApplicationDbContext database, ITenantContext tenant)
    : IConsumerDataRetentionRepository
{
    public async Task<ConsumerDataRetention?> GetForProcessingAsync(ConsumerDataRetentionWork work, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        database.RequireTransaction();
        if (tenant.RequiredTenantId != work.ConsumerTenantId)
            throw new InvalidOperationException("Retention processing needs the consumer tenant scope.");
        var rows = await database.ConsumerDataRetentions.FromSqlInterpolated($"""
            SELECT * FROM identity."ConsumerDataRetentions"
            WHERE "Id" = {work.RetentionId} AND "ConsumerTenantId" = {work.ConsumerTenantId}
              AND "CopyReplacedAtUtc" IS NULL AND "RetainUntilUtc" <= {nowUtc}
            FOR UPDATE SKIP LOCKED
            """).ToArrayAsync(cancellationToken);
        return rows.SingleOrDefault();
    }
}
