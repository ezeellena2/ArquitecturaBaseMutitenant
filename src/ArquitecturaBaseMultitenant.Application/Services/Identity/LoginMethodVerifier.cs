using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Verifica el código de un correo nuevo y, si la cuenta no tiene principal, lo promueve antes de avisar el cambio.</summary>
internal sealed class LoginMethodVerifier(LoginMethodGuard guard, ILoginMethodRepository methods,
    ILoginCodeRepository codeRepository, LoginCodeVerifier codes, LoginMethodNotifier notifier,
    IUserRepository users, TimeProvider timeProvider)
{
    public async Task<Result> VerifyAsync(Guid userId, VerifyLoginMethodRequest request, CancellationToken ct)
    {
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        var method = await methods.GetByIdForUserAsync(userId, request.MethodId, ct);
        if (method is null || method.Type != LoginMethodType.Email) return LoginMethodErrors.NotFound;
        if (method.VerifiedAtUtc is not null) return LoginMethodErrors.AlreadyVerified;
        var destination = LoginCodeDestination.ForEmail(Email.Create(method.Value).Value);
        await codeRepository.LockDestinationAsync(destination, ct);
        var currentCode = await codeRepository.GetLatestAsync(destination,
            LoginCodePurpose.VerifyDestination, userId, ct);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (currentCode is null || !currentCode.IsActive(nowUtc))
        {
            methods.Remove(method);
            return currentCode is { FailedAttempts: var attempts, MaxAttempts: var maxAttempts }
                && attempts >= maxAttempts
                ? LoginCodeErrors.TooManyAttempts
                : currentCode is { ConsumedAtUtc: not null }
                    ? LoginCodeErrors.AlreadyUsed
                    : LoginCodeErrors.Expired;
        }
        var verified = await codes.VerifyAsync(destination,
            LoginCodePurpose.VerifyDestination, userId, request.Code!, ct);
        if (verified.IsFailure) return verified.Error;
        method.Verify(timeProvider.GetUtcNow().UtcDateTime);
        var all = await methods.ListByUserIdAsync(userId, ct);
        if (!all.Any(value => value.IsPrimary))
        {
            method.MakePrimary();
            await users.SetPrimaryEmailAsync(userId, Email.Create(method.Value).Value, ct);
        }
        await notifier.ChangedAsync(account.Value, all, method, "Added", ct);
        return Result.Success();
    }
}
