using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

/// <summary>
/// A quién se puede sumar a una empresa: las personas activas de la organización que todavía no están en ella, con el
/// correo tapado. Un Administrador de empresa no tiene <c>users.read</c> de la organización (decisión del 2026-10-05).
/// </summary>
[ApiController]
[Route("api/companies/{companyId:guid}/candidates")]
[Tags("Miembros de empresa")]
[Access(Access.Business)]
public sealed class CompanyCandidatesController(ICompanyMembershipService service) : ControllerBase
{
    [HttpGet("paged")]
    [HasCompanyPermission(Permissions.Company.Members.Manage, organizationPermission: Permissions.Users.Read)]
    [ProducesResponseType<PagedResult<CompanyCandidateResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List([FromRoute] Guid companyId, [FromQuery] CompanyCandidateListHttpQuery query,
        CancellationToken cancellationToken) =>
        (await service.ListCandidatesAsync(companyId, query.ToModel(), cancellationToken)).ToActionResult(this);
}
