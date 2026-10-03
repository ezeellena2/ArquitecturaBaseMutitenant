using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using CompanyMemberContractResponse = ArquitecturaBaseMultitenant.Api.Contracts.Organization.CompanyMemberResponse;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

[ApiController]
[Route("api/companies/{companyId:guid}/members")]
[Tags("Miembros de empresa")]
[Access(Access.Business)]
public sealed class CompanyMembersController(ICompanyMembershipService service) : ControllerBase
{
    [HttpGet("paged")]
    [HasCompanyPermission(Permissions.Company.Members.Read)]
    [ProducesResponseType<PagedResult<CompanyMemberContractResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List([FromRoute] Guid companyId, [FromQuery] CompanyMemberListHttpQuery query,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(companyId, query.ToModel(), cancellationToken);
        if (result.IsFailure) return result.ToActionResult(this);
        var page = result.Value;
        return Ok(new PagedResult<CompanyMemberContractResponse>([.. page.Items.Select(item => item.ToHttpResponse())],
            page.Page, page.PageSize, page.TotalCount));
    }

    [HttpGet("filter-counts")]
    [HasCompanyPermission(Permissions.Company.Members.Read)]
    [ProducesResponseType<CompanyMemberFilterCountsResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFilterCounts([FromRoute] Guid companyId,
        [FromQuery] CompanyMemberListHttpQuery query, CancellationToken cancellationToken) =>
        (await service.GetFilterCountsAsync(companyId, query.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpPost]
    [HasCompanyPermission(Permissions.Company.Members.Manage)]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add([FromRoute] Guid companyId, [FromBody] AddCompanyMemberHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.AddAsync(new AddCompanyMemberRequest(companyId, request.MemberId, request.RoleIds),
            cancellationToken)).ToActionResult(this);

    [HttpPut("{memberId:guid}/roles")]
    [HasCompanyPermission(Permissions.Company.Members.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRoles([FromRoute] Guid companyId, [FromRoute] Guid memberId,
        [FromBody] UpdateCompanyMemberRolesHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateRolesAsync(new UpdateCompanyMemberRolesRequest(companyId, memberId,
            request.RoleIds, request.Version), cancellationToken)).ToActionResult(this);

    [HttpPut("{memberId:guid}/status")]
    [HasCompanyPermission(Permissions.Company.Members.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus([FromRoute] Guid companyId, [FromRoute] Guid memberId,
        [FromBody] ChangeCompanyMemberStatusHttpRequest request, CancellationToken cancellationToken) =>
        (await service.ChangeStatusAsync(new ChangeCompanyMemberStatusRequest(companyId, memberId,
            request.Status, request.Version), cancellationToken)).ToActionResult(this);
}
