using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// «Confirmá que sos vos» (E9): prueba el segundo factor del operador con sesión y deja una prueba reciente de 10
/// minutos ligada a esa sesión. No es un permiso ni acepta fechas del cliente. Devuelve la referencia de su vínculo
/// durable: un replay conserva el vencimiento original.
/// </summary>
public interface IPlatformReauthenticationService
{
    Task<Result<IdempotentReplayResponse<PlatformReauthenticationResponse>>> ReauthenticateAsync(
        PlatformReauthenticationRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Restablece el autenticador de otro operador (E9): revoca sus sesiones, desafío, clave y códigos, conserva sus
/// permisos y su próximo ingreso exige el alta. Usa la versión de operador que mostró la ficha.
/// </summary>
public interface IPlatformAuthenticatorService
{
    Task<Result<VersionResponse>> ResetAsync(Guid accountId, PlatformAccountActionRequest request,
        CancellationToken cancellationToken);
}
