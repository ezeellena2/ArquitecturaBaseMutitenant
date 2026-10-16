using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>
/// Después del commit de una cancelación, restablece la sesión cuando corresponde. El estado de acceso cacheado ya cambió
/// con la generación que avanzó el commit (ADR 0052).
/// </summary>
internal sealed class AccountDeletionSessionRestorer(ISignInService signIn)
{
    internal async Task RestoreAsync(Guid userId, string returnUrl, CancellationToken ct)
    {
        if (!ReturnUrls.IsLoginCodeDeletionReturn(returnUrl)) throw new InvalidOperationException("Invalid cancellation return.");
        var access = returnUrl == ReturnUrls.LoginLinkPath ? Access.Consumer
            : ReturnUrls.TryReadFixedLoginCodeSelection(returnUrl, out var fixedSelection) ? fixedSelection.Access
            : ReturnUrls.TryReadAccessSelection(returnUrl, out var selection) ? selection.Access : Access.Business;
        await signIn.SignInAsync(userId, access, ct);
    }
}
