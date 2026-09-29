using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/auth/methods")]
[Tags("Auth")]
public sealed class LoginMethodsController(ILoginMethodsService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<LoginMethodsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetLoginMethodsAsync(cancellationToken)).ToActionResult(this);
}
