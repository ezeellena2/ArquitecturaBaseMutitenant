using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
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
[Route("api/platform/recoveries")]
[Tags("Recuperaciones de Plataforma")]
[Access(Access.Platform)]
[HasPlatformPermission(PlatformPermissions.Recoveries.Manage)]
public sealed class PlatformRecoveriesController(IPlatformRecoveryService service,
    IPlatformRecoveryDecisionService decisions) : ControllerBase
{
    [HttpGet("paged")]
    [ProducesResponseType<PagedResult<PlatformRecoveryResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] PlatformRecoveriesQuery query, CancellationToken ct) =>
        (await service.ListAsync(query.ToModel(), ct)).ToActionResult(this);

    [HttpGet("filter-counts")]
    [ProducesResponseType<PlatformRecoveryFilterCounts>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Count([FromQuery] PlatformRecoveriesQuery query, CancellationToken ct) =>
        (await service.CountAsync(query.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/approve")]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] PlatformAccountActionHttpRequest request, CancellationToken ct) =>
        (await decisions.ApproveAsync(id, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/reject")]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] PlatformAccountActionHttpRequest request, CancellationToken ct) =>
        (await decisions.RejectAsync(id, request.ToModel(), ct)).ToActionResult(this);
}
