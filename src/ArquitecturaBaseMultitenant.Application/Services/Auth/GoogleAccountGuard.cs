using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

// El resolver llama bajo el lock de cuenta; la gracia entrega prueba sin emitir sesión.
internal sealed class GoogleAccountGuard(IUserRepository users, ISignInService signIn,
    AccountDeletionCancelIssuer cancellation)
{
    internal async Task<Result<UserAccountRow>> CheckAsync(Guid userId, Guid? methodId, string returnUrl,
        CancellationToken cancellationToken)
    {
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return AccountErrors.Suspended;
        if (await signIn.IsLockedOutAsync(userId, cancellationToken)) return AccountErrors.LockedOut;
        if (account.Status == UserStatus.PendingDeletion) return cancellation.Issue(account, methodId, returnUrl);
        return account;
    }
}
