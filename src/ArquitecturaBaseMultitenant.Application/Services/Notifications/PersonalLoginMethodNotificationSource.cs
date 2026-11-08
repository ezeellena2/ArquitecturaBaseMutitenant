using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Notifications;

/// <summary>
/// «Agregá un correo personal o tu WhatsApp para no perder tu cuenta si dejás la empresa», en la campana de la persona y
/// en la de su organización: la cuenta no tiene un método propio disponible, con la misma regla que el perfil
/// (<c>needsPersonalLoginMethod</c>). Nace con el último método que verificó; uno nuevo lo vuelve a contar. Lleva a «Mi
/// cuenta». Una cuenta de operador es solo de Plataforma y no lo recibe.
/// </summary>
internal sealed class PersonalLoginMethodNotificationSource(ILoginMethodReader methods,
    LoginMethodAvailabilityPolicy availability) : INotificationSource
{
    public const string Kind = "personalLoginMethod";

    public async Task<IReadOnlyList<NotificationItem>> ListAsync(NotificationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Access == Access.Platform) return [];
        var rows = await methods.ListByUserIdAsync(context.UserId, cancellationToken);
        if (await availability.HasPersonalBackupAsync(context.UserId, rows, cancellationToken)) return [];
        var latest = rows.Where(row => row.VerifiedAtUtc is not null)
            .OrderByDescending(row => row.VerifiedAtUtc).ThenBy(row => row.Id).FirstOrDefault();
        return latest is null ? [] :
        [
            new NotificationItem($"{Kind}:{latest.Id:N}", Kind, new Dictionary<string, string>(),
                latest.VerifiedAtUtc!.Value, ReturnUrls.AccountPath, Read: false),
        ];
    }
}
