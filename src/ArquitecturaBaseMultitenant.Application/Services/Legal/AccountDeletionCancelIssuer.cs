using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>La prueba ya validada y el lock de cuenta preceden a este comprobante sin sesión.</summary>
internal sealed class AccountDeletionCancelIssuer(IReauthTicketRepository tickets, ISecureTokenGenerator secrets,
    TimeProvider timeProvider)
{
    internal Error Issue(UserAccountRow account, Guid? sourceMethodId, string returnUrl)
    {
        if (account.Status == UserStatus.Suspended) return AccountErrors.Suspended;
        if (account.Status != UserStatus.PendingDeletion || account.DeletionScheduledForUtc is not { } date)
            return UserErrors.InvalidTransition;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (nowUtc >= date) return AccountDeletionErrors.GraceExpired;
        if (!ReturnUrls.TryReadAccessSelection(returnUrl, out _))
            throw new InvalidOperationException("The cancellation return URL must be validated.");
        var secret = secrets.Generate();
        var ticket = ReauthTicket.Issue(account.Id, ReauthAction.CancelDeletion, sourceMethodId,
            null, secrets.Hash(secret), nowUtc, returnUrl).Value;
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
