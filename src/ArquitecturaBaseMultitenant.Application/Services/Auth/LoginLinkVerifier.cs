using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
namespace ArquitecturaBaseMultitenant.Application.Services.Auth;
internal sealed class LoginLinkVerifier(LoginLinkGuard guard, ISignInService signIn,
    ILoginAuditRepository audits, AccountDeletionCancelIssuer cancellation, TimeProvider clock)
{
    internal async Task<Result<Guid>> RedeemAsync(string hash, CancellationToken ct)
    {
        var context = await guard.LockAsync(hash, ct);
        if (context is null) return LoginLinkErrors.Invalid;
        var (link, snapshot) = context.Value;
        var account = snapshot.Account;
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var consumed = link.Redeem(nowUtc);
        if (consumed.IsFailure) return Fail(account.Id, consumed.Error, nowUtc);
        if (snapshot.LockoutEnabled && snapshot.LockoutEndUtc > nowUtc)
            return Fail(account.Id, AccountErrors.LockedOut, nowUtc);
        if (account.Status == UserStatus.Suspended) return Fail(account.Id, AccountErrors.Suspended, nowUtc);
        if (account.Status == UserStatus.PendingDeletion)
            return Fail(account.Id, cancellation.Issue(account, Application.Models.Auth.ReturnUrls.LoginLinkPath), nowUtc);
        await signIn.ResetFailedAttemptsAsync(account.Id, ct);
        audits.Add(LoginAudit.Success(account.Id, LoginAuditMethod.WhatsAppLink, nowUtc));
        return account.Id;
    }
    private Error Fail(Guid id, Error error, DateTime nowUtc)
    { audits.Add(LoginAudit.Failure(id, LoginAuditMethod.WhatsAppLink, error, nowUtc)); return error; }
}
