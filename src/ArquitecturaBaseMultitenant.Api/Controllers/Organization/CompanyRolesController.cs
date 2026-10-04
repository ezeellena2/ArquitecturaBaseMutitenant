using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

/// <summary>
/// Los roles que se pueden asignar en una empresa, para quien ve sus miembros: un Administrador de empresa no tiene
/// <c>roles.read</c> de la organización (B4).
/// </summary>
[ApiController]
[Route("api/companies/{companyId:guid}/roles")]
[Tags("Miembros de empresa")]
[Access(Access.Business)]
public sealed class CompanyRolesController(IRoleService service) : ControllerBase
{
    [HttpGet("paged")]
    [HasCompanyPermission(Permissions.Company.Members.Read)]
    [ProducesResponseType<PagedResult<RoleResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List([FromRoute] Guid companyId, [FromQuery] CompanyRoleListHttpQuery query,
        CancellationToken cancellationToken) =>
        (await service.ListForCompanyAsync(companyId, new ListRolesRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Sort = query.Sort,
            Search = query.Search,
        }, cancellationToken)).ToActionResult(this);
}
