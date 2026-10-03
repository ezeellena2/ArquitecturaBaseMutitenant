using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Completa el alta Google: selecciona el primer contacto y encola el aviso del cambio.</summary>
internal sealed class GoogleMethodLinkFinalizer(IUserRepository users, LoginMethodNotifier notifier)
{
    internal async Task SetPrimaryIfNeededAsync(Guid userId, ExternalLogin login,
        IReadOnlyList<LoginMethod> existingMethods, LoginMethod method, CancellationToken cancellationToken)
    {
        if (!existingMethods.Any(value => value.IsPrimary))
        {
            method.MakePrimary();
            await users.SetPrimaryContactAsync(userId, login.Email, null, cancellationToken);
        }

    }

    internal Task NotifyAddedAsync(UserAccountRow account, IReadOnlyList<LoginMethod> existingMethods,
        LoginMethod method, CancellationToken cancellationToken) => notifier.ChangedAsync(account,
        [.. existingMethods, method], method, "Added", cancellationToken);
}
