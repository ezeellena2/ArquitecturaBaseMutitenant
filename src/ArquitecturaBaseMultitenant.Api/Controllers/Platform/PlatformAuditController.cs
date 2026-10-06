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
[Route("api/platform/audit")]
[Tags("Auditoría de Plataforma")]
[Access(Access.Platform)]
[HasPlatformPermission(PlatformPermissions.Audit.Read)]
public sealed class PlatformAuditController(IPlatformAuditService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CursorResult<PlatformAuditEventResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] PlatformAuditQuery query, CancellationToken ct) =>
        (await service.ListAsync(query.ToModel(), ct)).ToActionResult(this);

    [HttpGet("organizations")]
    [ProducesResponseType<IReadOnlyList<PlatformAuditOrganizationResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListOrganizations([FromQuery] PlatformAuditQuery query, CancellationToken ct) =>
        (await service.ListOrganizationsAsync(query.ToModel(), ct)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlatformAuditEventResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Find(Guid id, CancellationToken ct) =>
        (await service.FindAsync(id, ct)).ToActionResult(this);
}
