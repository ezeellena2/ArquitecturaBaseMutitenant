using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

[ApiController]
[Route("api/organization/audit")]
[Tags("Auditoría de la organización")]
[Access(Access.Business)]
public sealed class OrganizationAuditController(IOrganizationAuditService service) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Audit.Read)]
    [ProducesResponseType<CursorResult<OrganizationAuditEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] OrganizationAuditHttpQuery query,
        CancellationToken cancellationToken) =>
        (await service.ListAsync(query.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpGet("actors")]
    [BoundedLegacyCollection("/api/organization/audit/actors/paged")]
    [HasPermission(Permissions.Audit.Read)]
    [ProducesResponseType<IReadOnlyList<OrganizationAuditActorResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListActors([FromQuery] OrganizationAuditHttpQuery query,
        CancellationToken cancellationToken) =>
        (await service.ListActorsAsync(query.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpGet("actors/paged")]
    [HasPermission(Permissions.Audit.Read)]
    [ProducesResponseType<PagedResult<OrganizationAuditActorResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListActorsPaged([FromQuery] OrganizationAuditActorsHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListActorsPagedAsync(query.ToModel(), cancellationToken)).ToActionResult(this);
}
