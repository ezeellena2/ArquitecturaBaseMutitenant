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
[Route("api/platform/accounts")]
[Tags("Cuentas de Plataforma")]
[Access(Access.Platform)]
public sealed class PlatformAccountsController(IPlatformAccountService service,
    IPlatformAccountStateService states) : ControllerBase
{
    [HttpGet("paged")]
    [HasPlatformPermission(PlatformPermissions.Accounts.Read)]
    [ProducesResponseType<PagedResult<PlatformAccountResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] PlatformAccountsQuery query, CancellationToken ct) =>
        (await service.ListAsync(query.ToModel(), ct)).ToActionResult(this);

    [HttpGet("filter-counts")]
    [HasPlatformPermission(PlatformPermissions.Accounts.Read)]
    [ProducesResponseType<PlatformAccountFilterCounts>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Count([FromQuery] PlatformAccountsQuery query, CancellationToken ct) =>
        (await service.CountAsync(query.ToModel(), ct)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [HasPlatformPermission(PlatformPermissions.Accounts.Read)]
    [ProducesResponseType<PlatformAccountDetailResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Find(Guid id, CancellationToken ct) =>
        (await service.FindAsync(id, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/suspend")]
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Accounts.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] PlatformAccountActionHttpRequest request, CancellationToken ct) =>
        (await states.SuspendAsync(id, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/reactivate")]
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Accounts.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reactivate(Guid id, [FromBody] PlatformAccountActionHttpRequest request, CancellationToken ct) =>
        (await states.ReactivateAsync(id, request.ToModel(), ct)).ToActionResult(this);

    [HttpPost("{id:guid}/revoke-sessions")]
    [Idempotent]
    [HasPlatformPermission(PlatformPermissions.Accounts.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeSessions(Guid id, [FromBody] PlatformAccountActionHttpRequest request, CancellationToken ct) =>
        (await states.RevokeSessionsAsync(id, request.ToModel(), ct)).ToActionResult(this);
}
