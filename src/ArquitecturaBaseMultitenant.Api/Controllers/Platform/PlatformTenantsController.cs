using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants")]
[Tags("Organizaciones de Plataforma")]
[Access(Access.Platform)]
public sealed class PlatformTenantsController(ITenantAdministrationService service) : ControllerBase
{
    [HttpGet("paged")]
    [HasPlatformPermission(PlatformPermissions.Tenants.Read)]
    [ProducesResponseType<PagedResult<PlatformTenantResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] PlatformTenantsQuery query, CancellationToken ct) =>
        (await service.ListAsync(query.ToModel(), ct)).ToActionResult(this);

    [HttpGet("filter-counts")]
    [HasPlatformPermission(PlatformPermissions.Tenants.Read)]
    [ProducesResponseType<PlatformTenantFilterCounts>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Counts([FromQuery] PlatformTenantsQuery query, CancellationToken ct) =>
        (await service.CountAsync(query.ToModel(), ct)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [HasPlatformPermission(PlatformPermissions.Tenants.Read)]
    [ProducesResponseType<PlatformTenantResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Find(Guid id, CancellationToken ct) =>
        (await service.FindAsync(id, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/reject")]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken ct) =>
        (await service.ChangeStateAsync(id, PlatformTenantAction.Reject, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/suspend")]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken ct) =>
        (await service.ChangeStateAsync(id, PlatformTenantAction.Suspend, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/reactivate")]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivate(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken ct) =>
        (await service.ChangeStateAsync(id, PlatformTenantAction.Reactivate, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/close")]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken ct) =>
        (await service.ChangeStateAsync(id, PlatformTenantAction.Close, request.ToModel(), ct)).ToActionResult(this);
}
