using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Emite un comprobante de cancelación para una cuenta pendiente de baja tras validar su estado; permite volver al acceso elegido sin sesión activa.</summary>
internal sealed class AccountDeletionCancelIssuer(IAccountDeletionCancelTicketRepository tickets, ISecureTokenGenerator secrets,
    TimeProvider timeProvider)
{
    internal Error Issue(UserAccountRow account, string returnUrl)
    {
        if (!ReturnUrls.IsDeletionReturn(returnUrl))
            throw new InvalidOperationException("The cancellation return URL must be validated.");
        return IssueValidated(account, returnUrl);
    }

    internal Error IssueForLoginCode(UserAccountRow account, string returnUrl)
    {
        if (!ReturnUrls.IsLoginCodeDeletionReturn(returnUrl))
            throw new InvalidOperationException("The login code cancellation return URL must be validated.");
        return IssueValidated(account, returnUrl);
    }

    private Error IssueValidated(UserAccountRow account, string returnUrl)
    {
        if (account.Status == UserStatus.Suspended) return AccountErrors.Suspended;
        if (account.Status != UserStatus.PendingDeletion || account.DeletionScheduledForUtc is not { } date)
            return UserErrors.InvalidTransition;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (nowUtc >= date) return AccountDeletionErrors.GraceExpired;
        var secret = secrets.Generate();
        var ticket = AccountDeletionCancelTicket.Issue(account.Id, secrets.Hash(secret), nowUtc, returnUrl);
        tickets.Add(ticket);
        return AccountErrors.PendingDeletion with
        {
            Metadata = new Dictionary<string, object?>
            {
                ["scheduledForUtc"] = date, ["cancelTicket"] = secret, ["timeZoneId"] = account.TimeZoneId,
                ["returnUrl"] = returnUrl, ["cancelTicketExpiresAtUtc"] = ticket.ExpiresAtUtc,
            },
        };
    }
}
