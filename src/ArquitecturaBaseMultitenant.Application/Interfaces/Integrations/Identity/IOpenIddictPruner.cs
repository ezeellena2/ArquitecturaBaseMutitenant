using ArquitecturaBaseMultitenant.Application.Models.Identity;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>
/// ADR 0062: poda las autorizaciones y los tokens de OpenIddict que vencieron o se revocaron antes de
/// <c>expiredBeforeUtc</c>, con la poda que trae OpenIddict, de a tramos. Cada tramo corre en la transacción del llamador.
/// </summary>
public interface IOpenIddictPruner
{
    /// <summary>
    /// Un tramo: hasta un día después del token más viejo creado desde <paramref name="fromUtc"/> (o desde el principio,
    /// si es null), sin pasar el umbral final. El próximo tramo sigue desde <see cref="OpenIddictPruneTranche.NextFromUtc"/>.
    /// </summary>
    Task<OpenIddictPruneTranche> PruneTrancheAsync(DateTime expiredBeforeUtc, DateTime? fromUtc,
        CancellationToken cancellationToken);
}
