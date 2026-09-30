using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Registra eventos de seguridad y encola avisos de solicitud, cancelación o finalización de la baja.</summary>
internal sealed class AccountDeletionNotifier(AccountNoticeIssuer notices, ISecurityEventRepository events, IPublicOrigin origin)
{
    internal Task DeletedAsync(UserAccountRow account, IReadOnlyList<LoginMethod> methods, DateTime nowUtc, CancellationToken ct)
    {
        events.Add(SecurityEvent.ForAccount(SecurityEventType.AccountDeleted, account.Id, nowUtc));
        return notices.EnqueueAsync(account.Id, methods, new AccountNotice.AccountDeleted
            { RecipientName = account.DisplayName }, account.Culture, true, ct);
    }

    internal Task CancelledAsync(UserAccountRow account, IReadOnlyList<LoginMethod> methods, DateTime nowUtc, CancellationToken ct)
    {
        events.Add(SecurityEvent.ForAccount(SecurityEventType.AccountDeletionCancelled, account.Id, nowUtc));
        var url = origin.Value is { } value ? new Uri(value, "/cuenta").AbsoluteUri : "/cuenta";
        return notices.EnqueueAsync(account.Id, methods, new AccountNotice.DeletionCancelled(nowUtc, account.TimeZoneId, url)
            { RecipientName = account.DisplayName }, account.Culture, false, ct);
    }

    internal Task RequestedAsync(UserAccountRow account, IReadOnlyList<LoginMethod> methods,
        DateTime nowUtc, DateTime scheduledForUtc, CancellationToken ct)
    {
        events.Add(SecurityEvent.ForAccount(SecurityEventType.AccountDeletionRequested, account.Id, nowUtc));
        var url = origin.Value is { } value ? new Uri(value, "/login").AbsoluteUri : "/login";
        return notices.EnqueueAsync(account.Id, methods,
            new AccountNotice.DeletionRequested(nowUtc, scheduledForUtc, account.TimeZoneId, url)
                { RecipientName = account.DisplayName }, account.Culture, false, ct);
    }
}
