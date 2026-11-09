using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Guarda qué avisos de la campana abrió cada cuenta, dentro de la transacción del caso de uso.</summary>
public interface INotificationReadRepository
{
    void Add(NotificationRead read);

    /// <summary>Los de una cuenta que se borra, en el tramo global de su baja.</summary>
    Task RemoveForAccountAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Una tanda de los abiertos antes de ese instante, los más viejos primero; devuelve cuántos borró.</summary>
    Task<int> DeleteExpiredAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken);
}
