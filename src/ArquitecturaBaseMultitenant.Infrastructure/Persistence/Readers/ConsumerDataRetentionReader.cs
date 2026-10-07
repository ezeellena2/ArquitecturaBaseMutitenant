using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Proyecta un lote ordenado de retenciones vencidas; no lee datos de la cuenta ni filas compartidas.</summary>
internal sealed class ConsumerDataRetentionReader(ApplicationDbContext database) : IConsumerDataRetentionReader
{
    public async Task<IReadOnlyList<ConsumerDataRetentionWork>> ListExpiredAsync(DateTime nowUtc, int maximumCount,
        CancellationToken cancellationToken)
    {
        if (maximumCount is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumCount));
        return await database.ConsumerDataRetentions.AsNoTracking()
            .Where(retention => retention.CopyReplacedAtUtc == null && retention.RetainUntilUtc <= nowUtc)
            .OrderBy(retention => retention.RetainUntilUtc).ThenBy(retention => retention.Id).Take(maximumCount)
            .Select(retention => new ConsumerDataRetentionWork(retention.Id, retention.ConsumerTenantId))
            .ToArrayAsync(cancellationToken);
    }
}
