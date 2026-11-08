using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Notifications;

/// <summary>La campana publicada en el contrato de la base de la 12 (K1): responde 501 hasta que la B1 la programe.</summary>
internal sealed class NotificationService(TimeProvider timeProvider, ILogger<NotificationService> logger) : INotificationService
{
    public Task<Result<NotificationListResponse>> ListAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "ListNotifications",
            () => Task.FromResult(Result.Failure<NotificationListResponse>(ContractErrors.NotImplemented)));

    public Task<Result> MarkReadAsync(string id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "MarkNotificationRead",
            () => Task.FromResult(Result.Failure(ContractErrors.NotImplemented)));
}
