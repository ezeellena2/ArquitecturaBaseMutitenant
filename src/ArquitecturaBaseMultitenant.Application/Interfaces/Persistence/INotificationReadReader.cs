namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Qué avisos de la campana abrió una cuenta (base de la 12): global y por cuenta, sin tenant.</summary>
public interface INotificationReadReader
{
    /// <summary>De esos ids, los que la cuenta ya abrió.</summary>
    Task<IReadOnlyList<string>> ListReadIdsAsync(Guid userId, IReadOnlyCollection<string> notificationIds,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid userId, string notificationId, CancellationToken cancellationToken);
}
