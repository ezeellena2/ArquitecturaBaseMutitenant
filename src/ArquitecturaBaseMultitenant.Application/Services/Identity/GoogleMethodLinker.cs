using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>
/// Aplica las reglas para sumar un ingreso Google a una cuenta existente dentro de la transacción del servicio.
/// Bloquea los identificadores compartidos, impide que pertenezcan a otra cuenta y avisa del cambio.
/// </summary>
internal sealed class GoogleMethodLinker(LoginMethodGuard guard, IExternalLoginLock loginLock,
    IUserLookup lookup, ILoginMethodRepository methods, LoginCodeIssuer codes,
    TimeProvider timeProvider, LoginMethodDomainAssigner emailDomains, GoogleMethodLinkFinalizer finalizer)
{
    public async Task<Result> LinkAsync(Guid userId, ExternalLogin login, CancellationToken ct)
    {
        await loginLock.AcquireAsync(login.ProviderKey, login.Email, ct);
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        if (await guard.IsLockedOutAsync(userId, ct)) return AccountErrors.LockedOut;
        if (login.Email is { } email)
            await codes.LockDestinationAsync(LoginCodeDestination.ForEmail(email), ct);
        var existing = await lookup.FindMethodAsync(LoginMethodType.Google, login.ProviderKey, ct);
        if (existing is not null)
        {
            if (existing.UserId == userId) return Result.Success();
            if (existing.VerifiedAtUtc is not null) return GoogleMethodErrors.AlreadyUsed;
            var pending = await methods.GetByIdAsync(existing.MethodId, ct);
            if (pending is not null && pending.VerifiedAtUtc is null) methods.Remove(pending);
        }
        var emailOwner = await lookup.FindMethodAsync(LoginMethodType.Email, login.Email!.Value, ct);
        if (emailOwner is not null && emailOwner.UserId != userId)
        {
            if (emailOwner.VerifiedAtUtc is not null) return GoogleMethodErrors.AlreadyUsed;
            var pending = await methods.GetByIdAsync(emailOwner.MethodId, ct);
            if (pending is not null && pending.VerifiedAtUtc is null) methods.Remove(pending);
        }
        var all = await methods.ListByUserIdAsync(userId, ct);
        var method = LoginMethod.CreateGoogle(userId, login.ProviderKey, login.Email);
        method.Verify(timeProvider.GetUtcNow().UtcDateTime);
        await emailDomains.AssignIfVerifiedAsync(method, login.Email, ct);
        await finalizer.SetPrimaryIfNeededAsync(userId, login, all, method, ct);
        methods.Add(method);
        await finalizer.NotifyAddedAsync(account.Value, all, method, ct);
        return Result.Success();
    }
}
