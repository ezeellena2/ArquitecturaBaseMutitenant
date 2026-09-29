using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Verifica y consume el código bajo el lock del destino dentro de la UoW del servicio llamador.
/// Identity, auditoría y sesión pertenecen al caso de uso que llama.
/// </summary>
internal sealed class LoginCodeVerifier(
    ILoginCodeRepository loginCodes,
    ILoginCodeHasher codeHasher,
    TimeProvider timeProvider)
{
    public async Task<Result> VerifyAsync(LoginCodeDestination destination, LoginCodePurpose purpose,
        Guid? requestedByUserId, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        await loginCodes.LockDestinationAsync(destination, cancellationToken);

        var row = await loginCodes.GetLatestAsync(destination, purpose, requestedByUserId, cancellationToken);
        if (row is null)
        {
            return LoginCodeErrors.Invalid(attemptsLeft: null);
        }

        var hash = codeHasher.Hash(destination, purpose, code);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        return requestedByUserId is { } userId
            ? row.VerifyFor(userId, purpose, hash, nowUtc)
            : row.Verify(purpose, hash, nowUtc);
    }
}
