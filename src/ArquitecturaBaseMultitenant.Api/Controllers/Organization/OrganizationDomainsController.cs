using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

[ApiController]
[Route("api/organization/settings/domains")]
[Tags("Dominios de correo de la organización")]
[Access(Access.Business)]
public sealed class OrganizationDomainsController(IOrganizationDomainService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Settings.Read)]
    [ProducesResponseType<IReadOnlyList<OrganizationDomainResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        (await service.ListAsync(cancellationToken)).ToActionResult(this);

    [HttpPost]
    [HasPermission(Permissions.Settings.Manage)]
    [Idempotent]
    [ProducesResponseType<OrganizationDomainResponse>(StatusCodes.Status201Created)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterOrganizationDomainHttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RegisterAsync(request.ToModel(), cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(List), null, result.Value) : result.ToActionResult(this);
    }

    [HttpPost("{domainId:guid}/verify")]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    [ProducesProblem(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Verify([FromRoute] Guid domainId, CancellationToken cancellationToken) =>
        (await service.VerifyAsync(domainId, cancellationToken)).ToActionResult(this);

    [HttpDelete("{domainId:guid}")]
    [HasPermission(Permissions.Settings.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove([FromRoute] Guid domainId, [FromQuery] uint? version,
        CancellationToken cancellationToken) =>
        (await service.RemoveAsync(new RemoveOrganizationDomainRequest(domainId, version), cancellationToken))
        .ToActionResult(this);
}
