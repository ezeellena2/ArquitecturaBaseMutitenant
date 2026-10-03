using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Persiste el ingreso Google y sincroniza el contacto primario con su aviso de seguridad.</summary>
internal sealed class GoogleAccountMethodFinalizer(ILoginMethodRepository methods, IUserRepository users,
    LoginMethodNotifier notifier, TimeProvider timeProvider, LoginMethodDomainAssigner emailDomains)
{
    internal async Task AddGoogleMethodAsync(UserAccountRow account, Guid userId, string providerKey,
        Email? email, IReadOnlyList<LoginMethod> existingMethods, CancellationToken cancellationToken)
    {
        var method = LoginMethod.CreateGoogle(userId, providerKey, email);
        method.Verify(timeProvider.GetUtcNow().UtcDateTime);
        await emailDomains.AssignIfVerifiedAsync(method, email, cancellationToken);
        if (!existingMethods.Any(value => value.IsPrimary))
        {
            method.MakePrimary();
            await users.SetPrimaryContactAsync(userId, email, null, cancellationToken);
        }

        methods.Add(method);
        await notifier.ChangedAsync(account, [.. existingMethods, method], method, "Added", cancellationToken);
    }

    internal Task UpdatePrimaryContactIfNeededAsync(Guid userId, LoginMethod method, Email email,
        CancellationToken cancellationToken) => method.IsPrimary
        ? users.SetPrimaryContactAsync(userId, email, null, cancellationToken)
        : Task.CompletedTask;
}
