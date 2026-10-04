using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants/{id:guid}/domains")]
[Tags("Dominios de organización")]
[Access(Access.Platform)]
[HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
public sealed class PlatformDomainsController(IPlatformDomainService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationDomainResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid id, CancellationToken cancellationToken) =>
        (await service.ListAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost("{domain}/verify")]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    [ProducesProblem(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Verify(Guid id, string domain, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken cancellationToken) =>
        (await service.VerifyAsync(id, domain, request.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpDelete("{domain}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid id, string domain, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken cancellationToken) =>
        (await service.RemoveAsync(id, domain, request.ToModel(), cancellationToken)).ToActionResult(this);
}
