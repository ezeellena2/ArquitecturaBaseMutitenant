using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>
/// Segundo factor del ingreso de un operador (E9, ADR 0043 y 0044): consulta el desafío pendiente del navegador, prueba
/// el autenticador o un código de recuperación y completa el alta. La sesión de Plataforma nace recién al terminar.
/// </summary>
public interface IPlatformFactorService
{
    Task<Result<PlatformFactorChallengeResponse>> GetAsync(CancellationToken cancellationToken);

    Task<Result<VerifyPlatformFactorResponse>> VerifyAsync(VerifyPlatformFactorRequest request,
        CancellationToken cancellationToken);

    Task<Result<CompletePlatformFactorResponse>> CompleteAsync(CompletePlatformFactorRequest request,
        CancellationToken cancellationToken);
}
