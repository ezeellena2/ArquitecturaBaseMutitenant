using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

/// <summary>
/// «Confirmá que sos vos» (E9): el operador activo prueba su segundo factor y deja una prueba reciente de 10 minutos
/// para las acciones sensibles de esta sesión. No pide otro permiso.
/// </summary>
[ApiController]
[Route("api/platform/reauthentication")]
[Tags("Seguridad de Plataforma")]
[Access(Access.Platform)]
public sealed class PlatformReauthenticationController(IPlatformReauthenticationService service) : ControllerBase
{
    [HttpPost]
    [Idempotent]
    [ProducesResponseType<PlatformReauthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Reauthenticate([FromBody] PlatformReauthenticationHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.ReauthenticateAsync(request.ToModel(), cancellationToken)).ToActionResult(this);
}
