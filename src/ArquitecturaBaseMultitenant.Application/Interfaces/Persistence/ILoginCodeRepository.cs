using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Los límites y el lock se comparten por destino entre propósitos y cuentas.
/// El caso de uso toma el lock antes de leer o cambiar códigos, dentro de su transacción.
/// </summary>
public interface ILoginCodeRepository
{
    Task LockDestinationAsync(LoginCodeDestination destination, CancellationToken cancellationToken);

    Task<LoginCode?> GetLatestAsync(LoginCodeDestination destination, LoginCodePurpose purpose,
        Guid? requestedByUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LoginCode>> ListActiveAsync(LoginCodeDestination destination, LoginCodePurpose purpose,
        Guid? requestedByUserId, DateTime nowUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<DateTime>> ListRequestTimesSinceAsync(LoginCodeDestination destination, DateTime sinceUtc,
        CancellationToken cancellationToken);

    void Add(LoginCode code);
}
