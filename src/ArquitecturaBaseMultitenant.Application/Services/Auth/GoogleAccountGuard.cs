using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Application.Models.Auth;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

// El resolver llama bajo el lock de cuenta; la gracia entrega prueba sin emitir sesión. El bloqueo por códigos fallidos
// no frena a Google (B4): cualquiera que conozca el correo lo provoca, y Google es la salida del dueño.
internal sealed class GoogleAccountGuard(IUserRepository users, AccountDeletionCancelIssuer cancellation)
{
    internal async Task<Result<UserAccountRow>> CheckAsync(Guid userId, string returnUrl,
        CancellationToken cancellationToken)
    {
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return AccountErrors.Suspended;
        // ADR 0042: una invitación de operador se acepta solo con código; con Google no entra ni se vincula.
        if (account.HasPendingOperatorInvitation) return ExternalLoginErrors.Failed;
        if (account.IsPlatformOperator && (!ReturnUrls.TryReadAccessSelection(returnUrl, out var selection)
            || selection.Access != Access.Platform)) return AccessErrors.PlatformOnly;
        if (account.Status == UserStatus.PendingDeletion) return cancellation.Issue(account, returnUrl);
        return account;
    }
}
