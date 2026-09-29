using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Resuelve el subject global bajo lock, vincula correo verificado o registra desde Registro.</summary>
internal sealed class GoogleAccountResolver(
    IUserLookup lookup,
    IUserRepository users,
    ILoginMethodRepository methods,
    IExternalLoginLock loginLock,
    GoogleAccountRegistrar registrar,
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

        var parsedEmail = Email.Create(login.Email);
        var email = login.EmailVerified && parsedEmail.IsSuccess ? parsedEmail.Value : null;
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

        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null || account.Status is UserStatus.Suspended or UserStatus.Deleted)
            return AccountErrors.Suspended;
        if (account.Status == UserStatus.PendingDeletion)
            return AccountErrors.PendingDeletion;

        if (googleMethod is null)
        {
            var method = LoginMethod.CreateGoogle(userId, login.ProviderKey);
            method.Verify(timeProvider.GetUtcNow().UtcDateTime);
            if (account.PrimaryEmail is null && method.MakePrimary().IsFailure)
                throw new InvalidOperationException("A verified Google method could not become primary.");
            methods.Add(method);
        }

        return userId;
    }
}
