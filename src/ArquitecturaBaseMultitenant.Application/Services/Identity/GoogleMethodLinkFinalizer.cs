using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Completa el alta Google: selecciona el primer contacto y encola el aviso del cambio.</summary>
internal sealed class GoogleMethodLinkFinalizer(LoginMethodNotifier notifier, GoogleProfileWriter profile)
{
    internal Task NotifyAddedAsync(UserAccountRow account, IReadOnlyList<LoginMethod> existingMethods,
        LoginMethod method, CancellationToken cancellationToken) => notifier.ChangedAsync(account,
        [.. existingMethods, method], method, LoginMethodChange.Added, cancellationToken);

    internal Task FillMissingNamesAsync(Guid userId, ExternalLogin login, CancellationToken ct) =>
        profile.FillMissingNamesAsync(userId, login, ct);
}
