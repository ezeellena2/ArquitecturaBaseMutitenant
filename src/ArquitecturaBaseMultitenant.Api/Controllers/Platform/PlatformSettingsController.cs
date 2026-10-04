using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/settings")]
[Tags("Configuración de Plataforma")]
[Access(Access.Platform)]
[HasPlatformPermission(PlatformPermissions.Settings.Manage)]
public sealed class PlatformSettingsController(IPlatformSettingsService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PlatformSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetAsync(cancellationToken)).ToActionResult(this);

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] UpdatePlatformSettingsHttpRequest request, CancellationToken cancellationToken)
    {
        var model = request.ToModel();
        if (model.IsFailure) return Result.Failure(model.Error).ToActionResult(this);
        return (await service.UpdateAsync(model.Value, cancellationToken)).ToActionResult(this);
    }
}
