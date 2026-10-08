using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Verifica el propósito del código y conserva intentos/lockout solo ante un intento activo fallido.</summary>
internal sealed class LoginCodeAttemptVerifier(LoginCodeVerifier verifier, ILoginCodeRepository codes,
    ISignInService signIn, TimeProvider clock)
{
    internal async Task<Result> VerifyAsync(LoginCodeDestination destination, LoginCodePurpose purpose,
        Guid? requestedByUserId, string code, Guid? accountId, CancellationToken ct, bool usable = true)
    {
        await codes.LockDestinationAsync(destination, ct);
        var active = (await codes.GetLatestAsync(destination, purpose, requestedByUserId, ct))
            ?.IsActive(clock.GetUtcNow().UtcDateTime) == true;
        var verified = await verifier.VerifyAsync(destination, purpose, requestedByUserId, code, ct, usable);
        if (verified.IsFailure && active && accountId is { } userId && IsFailedAttempt(verified.Error))
        {
            await signIn.RegisterFailedAttemptAsync(userId, ct);
            if (await signIn.IsLockedOutAsync(userId, ct)) return AccountErrors.LockedOut;
        }
        return verified;
    }

    private static bool IsFailedAttempt(Error error) => error.Code == LoginCodeErrors.TooManyAttemptsCode
        || (error.Code == LoginCodeErrors.InvalidCode && error.Metadata?.ContainsKey(LoginCodeErrors.AttemptsLeftKey) == true);
}
