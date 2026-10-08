using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Persiste el ingreso Google y sincroniza el contacto primario con su aviso de seguridad.</summary>
internal sealed class GoogleAccountMethodFinalizer(ILoginMethodRepository methods,
    LoginMethodNotifier notifier, TimeProvider timeProvider, LoginMethodLinker linker)
{
    internal async Task AddGoogleMethodAsync(UserAccountRow account, Guid userId, string providerKey,
        Email? email, IReadOnlyList<LoginMethod> existingMethods, CancellationToken cancellationToken)
    {
        var method = LoginMethod.CreateGoogle(userId, providerKey, email);
        var linked = await linker.VerifyAsync(method, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        if (linked.IsFailure) throw new InvalidOperationException("Google ownership changed under its identity lock.");
        methods.Add(method);
        await notifier.ChangedAsync(account, [.. existingMethods, method], method, LoginMethodChange.Added, cancellationToken);
    }

    internal Task UpdatePrimaryContactIfNeededAsync(Guid userId, LoginMethod method, Email email,
        CancellationToken cancellationToken) => method.IsPrimary
        ? linker.SynchronizePrimaryAsync(method, cancellationToken)
        : Task.CompletedTask;
}
