using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

[ApiController]
[Route("api/companies")]
[Tags("Empresas")]
[Access(Access.Business)]
public sealed class CompaniesController(ICompanyService service) : ControllerBase
{
    [HttpGet("paged")]
    [HasPermission(Permissions.Companies.Read)]
    [ProducesResponseType<PagedResult<CompanyResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] CompanyListHttpQuery query,
        CancellationToken cancellationToken) =>
        (await service.ListAsync(query.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpGet("filter-counts")]
    [HasPermission(Permissions.Companies.Read)]
    [ProducesResponseType<CompanyFilterCountsResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFilterCounts([FromQuery] CompanyListHttpQuery query,
        CancellationToken cancellationToken) =>
        (await service.GetFilterCountsAsync(query.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpGet("{companyId:guid}")]
    [HasCompanyPermission(Permissions.Company.Members.Read, organizationPermission: Permissions.Companies.Read)]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid companyId, CancellationToken cancellationToken) =>
        (await service.GetAsync(companyId, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [HasPermission(Permissions.Companies.Manage)]
    [Idempotent]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.CreateAsync(new CreateCompanyRequest(request.Name, request.TaxIdNumber,
            request.TimeZoneId), cancellationToken)).ToCreatedResult(this, nameof(Get),
            id => new { companyId = id });

    [HttpPut("{companyId:guid}")]
    [HasPermission(Permissions.Companies.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromRoute] Guid companyId, [FromBody] UpdateCompanyHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.UpdateAsync(new UpdateCompanyRequest(companyId, request.Name,
            request.TaxIdNumber, request.TimeZoneId, request.Version), cancellationToken)).ToActionResult(this);

    [HttpPut("{companyId:guid}/status")]
    [HasPermission(Permissions.Companies.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus([FromRoute] Guid companyId,
        [FromBody] ChangeCompanyStatusHttpRequest request, CancellationToken cancellationToken) =>
        (await service.ChangeStatusAsync(new ChangeCompanyStatusRequest(companyId, request.Status, request.Version),
            cancellationToken)).ToActionResult(this);
}
