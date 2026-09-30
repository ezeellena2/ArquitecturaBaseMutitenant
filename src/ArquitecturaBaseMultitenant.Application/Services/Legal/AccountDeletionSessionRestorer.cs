using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Solo se invoca después del commit de la cuenta.</summary>
internal sealed class AccountDeletionSessionRestorer(IAccessStatusCache statuses, ISignInService signIn)
{
    internal ValueTask InvalidateAsync(Guid userId, CancellationToken ct) => statuses.InvalidateUserAsync(userId, ct);

    internal async Task RestoreAsync(Guid userId, CancellationToken ct)
    {
        await statuses.InvalidateUserAsync(userId, ct);
        await signIn.SignInAsync(userId, ct);
    }
}
