using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

/// <summary>
/// Segundo factor del ingreso de un operador (E9). No usa la sesión: cada acción exige la cookie protegida del desafío
/// pendiente, que nace al probar el primer factor y no autentica otras rutas.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth/platform-factor")]
[Tags("Auth")]
public sealed class PlatformFactorController(IPlatformFactorService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PlatformFactorChallengeResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetAsync(cancellationToken)).ToActionResult(this);

    [HttpPost("verify")]
    [Idempotent]
    [ProducesResponseType<VerifyPlatformFactorResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    [ProducesProblem(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Verify([FromBody] VerifyPlatformFactorHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.VerifyAsync(request.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpPost("complete")]
    [Idempotent]
    [ProducesResponseType<CompletePlatformFactorResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete([FromBody] CompletePlatformFactorHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.CompleteAsync(request.ToModel(), cancellationToken)).ToActionResult(this);
}
