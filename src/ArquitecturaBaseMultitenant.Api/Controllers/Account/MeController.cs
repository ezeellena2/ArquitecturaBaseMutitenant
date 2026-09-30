using ArquitecturaBaseMultitenant.Api.Contracts.Account;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Profile;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Account;

/// <summary>Entrega perfiles incluso si el acceso empresa está suspendido y delega la edición de preferencias globales.</summary>
[ApiController]
[Route("api/me")]
[Tags("Account")]
public sealed class MeController(IProfileService service) : ControllerBase
{
    [HttpGet]
    [Access(Access.Consumer, Access.Business, Access.Platform)]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetAsync(cancellationToken)).ToActionResult(this);

    [HttpPut]
    [Access(Access.Consumer, Access.Business, Access.Platform)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] UpdateMeHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.UpdateAsync(new UpdateMeRequest(request.DisplayName, request.Culture,
            request.TimeZoneId, request.Version), cancellationToken)).ToActionResult(this);
}
