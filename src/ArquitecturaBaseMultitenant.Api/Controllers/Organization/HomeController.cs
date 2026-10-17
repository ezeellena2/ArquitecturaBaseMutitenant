using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

/// <summary>
/// Inicio de la organización activa (ADR 0040). Lo ve cualquier miembro: cada tarjeta y cada fila de «Para resolver»
/// salen solo si quien mira tiene el permiso de esa área.
/// </summary>
[ApiController]
[Route("api/home")]
[Tags("Inicio")]
[Access(Access.Business)]
public sealed class HomeController(IHomeService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HomeResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetSummaryAsync(cancellationToken)).ToActionResult(this);
}
