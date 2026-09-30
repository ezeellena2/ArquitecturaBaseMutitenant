using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Application.Services.Identity;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Resuelve el subject global bajo lock, vincula correo verificado o registra desde Registro.</summary>
internal sealed class GoogleAccountResolver(
    IUserLookup lookup,
    IUserRepository users,
    ILoginMethodRepository methods,
    IExternalLoginLock loginLock,
    GoogleAccountRegistrar registrar,
    LoginMethodNotifier notifier,
    ISignInService signIn,
    TimeProvider timeProvider)
{
    internal Task<PersonalSpaceDraft> PrepareAsync(string? culture, string? timeZoneId,
        CancellationToken cancellationToken) => registrar.PrepareAsync(culture, timeZoneId, cancellationToken);

    internal async Task<Result<Guid>> ResolveAsync(ExternalSignInRequest request, ExternalLogin? login,
        PersonalSpaceDraft? draft, CancellationToken cancellationToken)
    {
        if (login is null || !string.Equals(login.Provider, "Google", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(login.ProviderKey))
        {
            return ExternalLoginErrors.Failed;
        }

        var email = login.EmailVerified ? login.Email : null;
        await loginLock.AcquireAsync(login.ProviderKey, email, cancellationToken);
        var googleMethod = await lookup.FindMethodAsync(LoginMethodType.Google,
            login.ProviderKey, cancellationToken);

        Guid userId;
        if (googleMethod is not null)
        {
            if (googleMethod.VerifiedAtUtc is null) return ExternalLoginErrors.Failed;
            userId = googleMethod.UserId;
        }
        else
        {
            if (email is null) return ExternalLoginErrors.EmailNotVerified;
            var linkedId = await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email,
                email.Value, cancellationToken);
            if (linkedId is { } existingId)
            {
                userId = existingId;
            }
            else
            {
                if (!request.IsSignup) return ExternalLoginErrors.AccountNotFound;
                if (draft is null) throw new InvalidOperationException("The personal space was not prepared.");
                var registered = await registrar.RegisterAsync(login, draft, cancellationToken);
                if (registered.IsFailure) return registered.Error;
                userId = registered.Value;
            }
        }

        await methods.LockUserAsync(userId, cancellationToken);
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return AccountErrors.Suspended;
        if (account.Status == UserStatus.PendingDeletion)
            return AccountErrors.PendingDeletion;
        if (await signIn.IsLockedOutAsync(userId, cancellationToken)) return AccountErrors.LockedOut;

        if (googleMethod is null)
        {
            var all = await methods.ListByUserIdAsync(userId, cancellationToken);
            var method = LoginMethod.CreateGoogle(userId, login.ProviderKey, email);
            method.Verify(timeProvider.GetUtcNow().UtcDateTime);
            if (!all.Any(value => value.IsPrimary))
            {
                method.MakePrimary();
                await users.SetPrimaryContactAsync(userId, email, null, cancellationToken);
            }
            methods.Add(method);
            await notifier.ChangedAsync(account, [.. all, method], method, "Added", cancellationToken);
        }
        else if (email is not null)
        {
            var method = await methods.GetByIdForUserAsync(userId, googleMethod.MethodId, cancellationToken)
                ?? throw new InvalidOperationException("The Google method disappeared under its lock.");
            if (method.ContactEmail is null)
            {
                method.UpdateGoogleContact(email);
                if (method.IsPrimary) await users.SetPrimaryContactAsync(userId, email, null, cancellationToken);
            }
        }

        return userId;
    }
}
