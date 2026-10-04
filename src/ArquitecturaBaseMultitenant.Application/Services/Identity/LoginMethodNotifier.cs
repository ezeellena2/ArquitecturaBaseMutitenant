using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Registra un evento de seguridad y encola el aviso de un cambio de método sin incluir el contacto completo en el mensaje.</summary>
internal sealed class LoginMethodNotifier(AccountNoticeIssuer notices, ISecurityEventRepository events,
    IPublicOrigin origin, TimeProvider timeProvider)
{
    public Task ChangedAsync(UserAccountRow account, IReadOnlyList<LoginMethod> destinations,
        LoginMethod changed, string change, CancellationToken ct)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        events.Add(SecurityEvent.ForAccount(SecurityEventType.LoginMethodChanged, account.Id, nowUtc));
        var url = origin.Value is { } value ? new Uri(value, "/cuenta").AbsoluteUri : "/cuenta";
        var notice = new AccountNotice.LoginMethodChanged(change, changed.Type,
            PersonalDataMask.LoginMethod(changed.Type, changed.Value, changed.ContactEmail?.Value), nowUtc,
            account.TimeZoneId, url) { RecipientName = account.DisplayName };
        return notices.EnqueueAsync(account.Id, destinations, notice, account.Culture, false, ct);
    }

    internal static string? Contact(LoginMethod method) =>
        method.Type == LoginMethodType.Google ? method.ContactEmail?.Value : method.Value;
}
