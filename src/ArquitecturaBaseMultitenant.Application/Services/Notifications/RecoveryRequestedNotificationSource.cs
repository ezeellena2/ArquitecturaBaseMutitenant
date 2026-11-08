using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Notifications;

/// <summary>
/// «Pablo Ortiz pidió recuperar su cuenta», en la campana de Plataforma: uno por pedido pendiente, para quien los revisa
/// (<c>platform.recoveries.manage</c>, como «Para resolver» del inicio). Los 50 más nuevos, con la fecha del pedido; llevan
/// a Recuperaciones con los pendientes. Sus datos: <c>name</c>, el nombre de la cuenta si se encontró, y
/// <c>methodType</c> y <c>method</c>, el método anterior enmascarado como en la bandeja.
/// </summary>
internal sealed class RecoveryRequestedNotificationSource(IAccountRecoveryReader recoveries) : INotificationSource
{
    public const string Kind = "recoveryRequested";

    public async Task<IReadOnlyList<NotificationItem>> ListAsync(NotificationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Access != Access.Platform || !context.Can(PlatformPermissions.Recoveries.Manage)) return [];
        var page = await recoveries.ListAsync(new ListPlatformRecoveriesRequest
        {
            Status = AccountRecoveryStatus.Pending,
            PageSize = NotificationListResponse.Limit,
            Sort = "-createdAt",
        }, cancellationToken);
        return [.. page.Items.Select(row => new NotificationItem($"{Kind}:{row.Id:N}", Kind, Params(row), row.CreatedAtUtc,
            $"{ReturnUrls.PlatformRecoveriesPath}?status={AccountRecoveryStatus.Pending}", Read: false))];
    }

    private static Dictionary<string, string> Params(PlatformRecoveryRow row)
    {
        var values = new Dictionary<string, string>
        {
            ["methodType"] = row.PreviousMethodType.ToString(),
            ["method"] = PersonalDataMask.LoginMethod(row.PreviousMethodType, row.PreviousMethodValue, null),
        };
        if (!string.IsNullOrWhiteSpace(row.AccountName)) values["name"] = row.AccountName;
        return values;
    }
}
