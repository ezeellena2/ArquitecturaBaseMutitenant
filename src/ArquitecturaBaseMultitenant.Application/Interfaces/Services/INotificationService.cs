using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>La campana de quien mira, en los tres accesos: junta las fuentes y guarda solo qué abrió cada persona.</summary>
public interface INotificationService
{
    Task<Result<NotificationListResponse>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Marca leído un aviso visible de quien mira; un id que no corresponde a uno se ignora, sin error.</summary>
    Task<Result> MarkReadAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Borra una tanda de los avisos abiertos hace más de 90 días. La llama la limpieza en segundo plano y devuelve cuántos
    /// borró; 0 cuando no queda ninguno.
    /// </summary>
    Task<Result<int>> CleanupExpiredReadsAsync(CancellationToken cancellationToken);
}
