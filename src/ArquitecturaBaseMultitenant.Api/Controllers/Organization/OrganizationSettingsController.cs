using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

[ApiController]
[Route("api/organization/settings")]
[Tags("Configuración de la organización")]
[Access(Access.Business)]
public sealed class OrganizationSettingsController(IOrganizationSettingsService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Settings.Read)]
    [ProducesResponseType<OrganizationSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        (await service.GetAsync(cancellationToken)).ToActionResult(this);

    [HttpPut]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] UpdateOrganizationSettingsHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.UpdateAsync(request.ToModel(), cancellationToken)).ToActionResult(this);
}
