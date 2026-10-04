using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

/// <summary>Correos de ventas y de soporte que muestran Ingreso y Registro; los carga Plataforma › Configuración.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/contact")]
[Tags("Auth")]
public sealed class ContactController(IPlatformSettingsService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PlatformContactResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetContactAsync(cancellationToken)).ToActionResult(this);
}
