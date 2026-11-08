using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Notifications;

/// <summary>
/// La campana (base de la 12), en los tres accesos. Arma los avisos al leer: junta lo que aporta cada
/// <see cref="INotificationSource"/> con el alcance de quien mira, calculado una sola vez por pedido, y guarda solo cuáles
/// abrió cada cuenta. Una fuente que falla, o que entrega un aviso fuera del contrato, queda afuera sin tumbar a las demás.
/// </summary>
internal sealed partial class NotificationService(IEnumerable<INotificationSource> sources, INotificationReadReader reads,
    INotificationReadRepository readWrites, IPermissionService permissions, IPlatformPermissionService platformPermissions,
    ICurrentUser currentUser, IUnitOfWork unitOfWork, TimeProvider timeProvider, ILogger<NotificationService> logger)
    : INotificationService
{
    public Task<Result<NotificationListResponse>> ListAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<NotificationListResponse>(logger, timeProvider, "ListNotifications", async () =>
        {
            if (await ContextAsync(cancellationToken) is not { } context) return AccessErrors.Wrong;
            var visible = await VisibleAsync(context, cancellationToken);
            var read = visible.Count == 0 ? [] : (await reads.ListReadIdsAsync(context.UserId,
                [.. visible.Select(item => item.Id)], cancellationToken)).ToHashSet(StringComparer.Ordinal);
            NotificationItem[] items = [.. visible.Select(item => item with { Read = read.Contains(item.Id) })];
            return new NotificationListResponse(items, items.Count(item => !item.Read));
        });

    public Task<Result> MarkReadAsync(string id, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "MarkNotificationRead", async () =>
        {
            if (await ContextAsync(cancellationToken) is not { } context) return AccessErrors.Wrong;
            // Un id que no es de un aviso visible de quien mira se ignora: no se guarda ni se distingue en la respuesta.
            if (string.IsNullOrWhiteSpace(id) || id.Length > TextLimits.NotificationId
                || !(await VisibleAsync(context, cancellationToken)).Any(item => item.Id == id)) return Result.Success();
            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    if (!await reads.ExistsAsync(context.UserId, id, ct))
                        readWrites.Add(NotificationRead.Mark(context.UserId, id, timeProvider.GetUtcNow().UtcDateTime));
                    return Result.Success();
                }, CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueKeys.NotificationRead)
            {
                // Otro pedido de la misma cuenta lo marcó a la vez: queda leído una sola vez.
                return Result.Success();
            }
        });

    /// <summary>Quién mira y los permisos de su acceso, leídos una sola vez: cada fuente decide con ellos.</summary>
    private async Task<NotificationContext?> ContextAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || userId == Guid.Empty) return null;
        return currentUser.Access switch
        {
            Access.Consumer => new NotificationContext(userId, Access.Consumer, []),
            Access.Business => new NotificationContext(userId, Access.Business,
                await permissions.GetOrganizationPermissionsAsync(userId, cancellationToken),
                await permissions.GetPermissionsByCompanyAsync(userId, cancellationToken)),
            Access.Platform => new NotificationContext(userId, Access.Platform,
                await platformPermissions.GetPermissionsAsync(userId, cancellationToken)),
            _ => null,
        };
    }

    /// <summary>Los avisos de todas las fuentes, hasta 50 y del más nuevo al más viejo, todavía sin marcar leídos.</summary>
    private async Task<IReadOnlyList<NotificationItem>> VisibleAsync(NotificationContext context,
        CancellationToken cancellationToken)
    {
        var items = new List<NotificationItem>();
        // Cada fuente lee con el mismo contexto de datos: van de a una, nunca en paralelo, como «Para resolver».
        foreach (var source in sources)
        {
            IReadOnlyList<NotificationItem> listed;
            try
            {
                listed = await source.ListAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                ExceptionLog.Write(logger, source.GetType().Name, exception);
                continue;
            }
            if (listed.All(Fits)) items.AddRange(listed);
            else LogSourceOutsideContract(logger, source.GetType().Name);
        }
        return
        [
            .. items.DistinctBy(item => item.Id, StringComparer.Ordinal)
                .OrderByDescending(item => item.OccurredAtUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .Take(NotificationListResponse.Limit),
        ];
    }

    /// <summary>
    /// Lo que publica el contrato: un id de hasta 160, sin barras porque viaja en la ruta de marcar leído, una clave, un
    /// instante UTC y el destino en el front.
    /// </summary>
    private static bool Fits(NotificationItem item) =>
        !string.IsNullOrWhiteSpace(item.Id) && item.Id.Length <= TextLimits.NotificationId && !item.Id.Contains('/')
        && !string.IsNullOrWhiteSpace(item.Kind) && item.OccurredAtUtc.Kind == DateTimeKind.Utc
        && item.Link.StartsWith('/') && item.Params is not null;

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification source {Source} returned an item outside the contract")]
    private static partial void LogSourceOutsideContract(ILogger logger, string source);
}
