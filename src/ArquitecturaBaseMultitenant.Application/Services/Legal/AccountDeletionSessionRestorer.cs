using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Después del commit de una baja o cancelación, invalida el estado de acceso en caché y restablece la sesión cuando corresponde.</summary>
internal sealed class AccountDeletionSessionRestorer(IAccessStatusCache statuses, ISignInService signIn)
{
    internal ValueTask InvalidateAsync(Guid userId, CancellationToken ct) => statuses.InvalidateUserAsync(userId, ct);

    internal async Task RestoreAsync(Guid userId, string returnUrl, CancellationToken ct)
    {
        await statuses.InvalidateUserAsync(userId, ct);
        if (!ReturnUrls.IsLoginCodeDeletionReturn(returnUrl)) throw new InvalidOperationException("Invalid cancellation return.");
        var access = returnUrl == ReturnUrls.LoginLinkPath ? Access.Consumer
            : ReturnUrls.TryReadFixedLoginCodeSelection(returnUrl, out var fixedSelection) ? fixedSelection.Access
            : ReturnUrls.TryReadAccessSelection(returnUrl, out var selection) ? selection.Access : Access.Business;
        await signIn.SignInAsync(userId, access, ct);
    }
}
