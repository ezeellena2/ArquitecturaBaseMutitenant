using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Retention;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// ADR 0062: la retención de los datos globales del ingreso, en el scope global y sin una organización. La llama un
/// trabajo de fondo, un lote o un tramo por vez, cada uno en su transacción.
/// </summary>
public interface IIdentityRetentionService
{
    /// <summary>Borra un lote de códigos de ingreso vencidos de plazo y devuelve cuántos; menos que el lote, ya no quedan.</summary>
    Task<Result<int>> PurgeLoginCodesAsync(CancellationToken cancellationToken);

    /// <summary>Borra un lote de ingresos vencidos de plazo, salvo el último de cada cuenta, desde <paramref name="after"/>.</summary>
    Task<Result<RetentionBatch>> PurgeLoginAuditsAsync(RetentionCursor? after, CancellationToken cancellationToken);

    /// <summary>
    /// Poda un tramo de OpenIddict y devuelve cuántos tokens y autorizaciones podó y desde dónde sigue el próximo, o null
    /// si terminó.
    /// </summary>
    Task<Result<OpenIddictPruneTranche>> PruneSessionsAsync(DateTime? fromUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Borra un lote de contadores de los topes de mensajes (ADR 0063) que ya salieron de su ventana de 24 horas móviles y
    /// devuelve cuántos; menos que el lote, ya no quedan.
    /// </summary>
    Task<Result<int>> PurgeMessageQuotaBucketsAsync(CancellationToken cancellationToken);
}
