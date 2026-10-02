using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

/// <summary>Consulta una dirección pública antes de completar el alta de una organización.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public-site/slug-availability")]
[Tags("Public site")]
public sealed class TenantSlugAvailabilityController(ITenantSlugAvailabilityService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Check([FromQuery] string? slug, CancellationToken cancellationToken) =>
        (await service.CheckAsync(slug, cancellationToken)).ToActionResult(this);
}
