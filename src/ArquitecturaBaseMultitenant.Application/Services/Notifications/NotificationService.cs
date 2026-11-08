using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Notifications;

/// <summary>
/// La campana publicada en el contrato de la base de la 12 (K1), todavía sin fuentes: la lista sale vacía y, como no hay
/// avisos visibles, marcar leído ignora el id. La B1 junta las fuentes y guarda qué abrió cada persona. No responde 501
/// porque la matriz de aislamiento no admite un 5xx en un GET con un acceso declarado.
/// </summary>
internal sealed class NotificationService(TimeProvider timeProvider, ILogger<NotificationService> logger) : INotificationService
{
    public Task<Result<NotificationListResponse>> ListAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "ListNotifications",
            () => Task.FromResult(Result.Success(new NotificationListResponse([], 0))));

    public Task<Result> MarkReadAsync(string id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "MarkNotificationRead", () => Task.FromResult(Result.Success()));
}
