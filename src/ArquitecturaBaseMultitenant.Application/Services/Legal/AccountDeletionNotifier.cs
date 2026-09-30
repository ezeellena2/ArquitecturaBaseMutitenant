using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

internal sealed class AccountDeletionNotifier(AccountNoticeIssuer notices, ISecurityEventRepository events, IPublicOrigin origin)
{
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
