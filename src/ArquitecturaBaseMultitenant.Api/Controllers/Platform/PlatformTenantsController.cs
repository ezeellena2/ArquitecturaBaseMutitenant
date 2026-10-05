using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants")]
[Tags("Organizaciones de Plataforma")]
[Access(Access.Platform)]
public sealed class PlatformTenantsController(ITenantAdministrationService service) : ControllerBase
{
    [HttpPost]
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType<CreatedPlatformTenantResponse>(StatusCodes.Status201Created)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreatePlatformTenantHttpRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(Request.Headers["Idempotency-Key"], out var id) || id == Guid.Empty)
            return Result.Failure(TenantErrors.ProvisioningConflict).ToActionResult(this);
        var parsed = request.ToModel();
        if (parsed.IsFailure) return Result.Failure(parsed.Error).ToActionResult(this);
        return (await service.CreateAsync(id, parsed.Value, ct)).ToCreatedResult(this, nameof(Find), value => new { id = value.Id });
    }

    [HttpPost("{id:guid}/approve")]
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken ct) =>
        (await service.ProvisionAsync(id, false, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/retry-provisioning")]
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(Guid id, [FromBody] PlatformTenantRetryHttpRequest request, CancellationToken ct) =>
        (await service.ProvisionAsync(id, true, request.ToModel(), ct)).ToActionResult(this);

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
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken ct) =>
        (await service.ChangeStateAsync(id, PlatformTenantAction.Reject, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/suspend")]
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken ct) =>
        (await service.ChangeStateAsync(id, PlatformTenantAction.Suspend, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/reactivate")]
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivate(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken ct) =>
        (await service.ChangeStateAsync(id, PlatformTenantAction.Reactivate, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/close")]
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Tenants.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid id, [FromBody] PlatformTenantStateHttpRequest request, CancellationToken ct) =>
        (await service.ChangeStateAsync(id, PlatformTenantAction.Close, request.ToModel(), ct)).ToActionResult(this);
}
