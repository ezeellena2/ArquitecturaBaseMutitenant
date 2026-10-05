using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants/{id:guid}/features")]
[Tags("Módulos de Plataforma")]
[Access(Access.Platform)]
public sealed class PlatformFeaturesController(IPlatformFeatureService service) : ControllerBase
{
    [HttpGet]
    [HasPlatformPermission(PlatformPermissions.Tenants.Read)]
    [ProducesResponseType<IReadOnlyList<PlatformFeatureResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid id, CancellationToken cancellationToken) =>
        (await service.ListAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPut("{featureKey}")]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, string featureKey, [FromBody] UpdatePlatformFeatureHttpRequest request,
        CancellationToken cancellationToken) => (await service.UpdateAsync(id, featureKey, request.ToModel(), cancellationToken)).ToActionResult(this);
}
