using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;

using ArquitecturaBaseMultitenant.Application.Services.Identity;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Valida la cuenta existente y confirma su método sin duplicar identidad.</summary>
internal sealed class SignupExistingMethodVerifier(IUserRepository users,
    ILoginMethodRepository methods, IUserLookup userLookup,
    LoginMethodLinker linker)
{
    internal async Task<LoginMethodLookup?> LockAsync(LoginCodeDestination destination, CancellationToken ct)
    {
        await linker.LockDestinationAsync(destination, ct);
        var method = await FindAsync(destination, ct);
        if (method is not null) await methods.LockUserAsync(method.UserId, ct);
        return method;
    }

    internal Task<LoginMethodLookup?> FindAsync(LoginCodeDestination destination,
        CancellationToken cancellationToken) =>
        userLookup.FindMethodAsync(LoginDestinationResolver.Type(destination), destination.Value, cancellationToken);

    internal async Task RemovePendingAsync(LoginMethodLookup method, CancellationToken cancellationToken)
    {
        if (method.VerifiedAtUtc is not null) return;
        var pending = await methods.GetByIdAsync(method.MethodId, cancellationToken);
        if (pending is not null && pending.VerifiedAtUtc is null) methods.Remove(pending);
    }

    internal async Task<Error?> CheckAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return AccountErrors.Suspended;
        // ADR 0042: una cuenta de operador no entra por el registro de persona ni de empresa.
        if (account.IsPlatformOperator) return AccessErrors.PlatformOnly;
        return account.Status == UserStatus.PendingDeletion ? AccountErrors.PendingDeletion : null;
    }

    internal async Task<Guid> ConfirmAsync(LoginMethodLookup method, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var userId = method.UserId;
        if (method.VerifiedAtUtc is null)
        {
            var tracked = await methods.GetByIdAsync(method.MethodId, cancellationToken)
                ?? throw new InvalidOperationException("The login method disappeared during signup.");
            var linked = await linker.VerifyAsync(tracked, nowUtc, cancellationToken);
            if (linked.IsFailure) throw new InvalidOperationException("Signup ownership changed under the identity lock.");
        }

        return userId;
    }
}
