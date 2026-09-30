using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

internal sealed class GoogleMethodLinker(LoginMethodGuard guard, IExternalLoginLock loginLock,
    IUserLookup lookup, ILoginMethodRepository methods, IUserRepository users,
    LoginMethodNotifier notifier, ISignInService signIn, TimeProvider timeProvider)
{
    public async Task<Result> LinkAsync(Guid userId, ExternalLogin login, CancellationToken ct)
    {
        await loginLock.AcquireAsync(login.ProviderKey, login.Email, ct);
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        if (await signIn.IsLockedOutAsync(userId, ct)) return AccountErrors.LockedOut;
        var existing = await lookup.FindMethodAsync(LoginMethodType.Google, login.ProviderKey, ct);
        if (existing is not null)
            return existing.UserId == userId ? Result.Success() : GoogleMethodErrors.AlreadyUsed;
        var emailOwner = await lookup.FindMethodAsync(LoginMethodType.Email, login.Email!.Value, ct);
        if (emailOwner is not null && emailOwner.UserId != userId) return GoogleMethodErrors.AlreadyUsed;
        var all = await methods.ListByUserIdAsync(userId, ct);
        var method = LoginMethod.CreateGoogle(userId, login.ProviderKey, login.Email);
        method.Verify(timeProvider.GetUtcNow().UtcDateTime);
        if (!all.Any(value => value.IsPrimary))
        {
            method.MakePrimary();
            await users.SetPrimaryContactAsync(userId, login.Email, null, ct);
        }
        methods.Add(method);
        await notifier.ChangedAsync(account.Value, [.. all, method], method, "Added", ct);
        return Result.Success();
    }
}
