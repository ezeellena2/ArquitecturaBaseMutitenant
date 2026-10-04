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
    // Con usable en falso, el código de una cuenta falla como uno mal escrito: su destino ya es de otra cuenta.
    public async Task<Result> VerifyAsync(LoginCodeDestination destination, LoginCodePurpose purpose,
        Guid? requestedByUserId, string code, CancellationToken cancellationToken, bool usable = true)
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
            ? row.VerifyFor(userId, purpose, hash, nowUtc, usable)
            : row.Verify(purpose, hash, nowUtc);
    }

    /// <summary>Verifica el código de «Recuperar mi cuenta» con el nonce con el que se pidió.</summary>
    public async Task<Result> VerifyRecoveryAsync(LoginCodeDestination destination, string nonceHash, string code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        await loginCodes.LockDestinationAsync(destination, cancellationToken);
        var row = await loginCodes.FindRecoveryByNonceAsync(destination, nonceHash, cancellationToken);
        return row is null
            ? LoginCodeErrors.Invalid(attemptsLeft: null)
            : row.VerifyRecovery(nonceHash, codeHash: codeHasher.Hash(destination, LoginCodePurpose.AccountRecovery, code),
                timeProvider.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// El código de recuperación ya verificado con este nonce y vigente, bajo el lock del destino; null si no hay. Se
    /// busca por el nonce y no por el último código: otro pedido para el mismo correo no corta este.
    /// </summary>
    public async Task<LoginCode?> FindVerifiedRecoveryAsync(LoginCodeDestination destination, string nonceHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        await loginCodes.LockDestinationAsync(destination, cancellationToken);
        var row = await loginCodes.FindRecoveryByNonceAsync(destination, nonceHash, cancellationToken);
        return row is not null && row.IsVerifiedRecovery(nonceHash, timeProvider.GetUtcNow().UtcDateTime) ? row : null;
    }
}
