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

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Companies.Read)]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);

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
            request.TimeZoneId), cancellationToken)).ToCreatedResult(this, nameof(Get), id => new { id });

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Companies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateCompanyHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.UpdateAsync(new UpdateCompanyRequest(id, request.Name,
            request.TaxIdNumber, request.TimeZoneId, request.Version), cancellationToken)).ToActionResult(this);

    [HttpPut("{id:guid}/status")]
    [HasPermission(Permissions.Companies.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus([FromRoute] Guid id,
        [FromBody] ChangeCompanyStatusHttpRequest request, CancellationToken cancellationToken) =>
        (await service.ChangeStatusAsync(new ChangeCompanyStatusRequest(id, request.Status, request.Version),
            cancellationToken)).ToActionResult(this);
}
