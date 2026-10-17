using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

[ApiController]
[Route("api/roles")]
[Tags("Roles")]
[Access(Access.Business)]
public sealed class RolesController(IRoleService service) : ControllerBase
{
    [HttpGet("filter-counts")]
    [HasPermission(Permissions.Roles.Read)]
    [ProducesResponseType<RoleFilterCountsResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFilterCounts([FromQuery] RoleFilterCountsHttpQuery query, CancellationToken cancellationToken) =>
        (await service.GetFilterCountsAsync(query.Search, cancellationToken)).ToActionResult(this);

    [HttpGet("paged")]
    [HasPermission(Permissions.Roles.Read)]
    [ProducesResponseType<PagedResult<RoleResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] RoleListHttpQuery query, CancellationToken cancellationToken) =>
        (await service.ListAsync(new ListRolesRequest
        {
            Page = query.Page ?? PagedRequest.DefaultPage,
            PageSize = query.PageSize ?? PagedRequest.DefaultPageSize,
            Sort = query.Sort,
            Search = query.Search,
            Scope = query.Scope,
            CompanyId = query.CompanyId,
            OnlyOwn = query.OnlyOwn ?? false,
        }, cancellationToken)).ToActionResult(this);

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Roles.Read)]
    [ProducesResponseType<RoleResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken cancellationToken) =>
        (await service.GetAsync(id, cancellationToken)).ToActionResult(this);

    [HttpPost]
    [HasPermission(Permissions.Roles.Manage)]
    [Idempotent]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateRoleHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.CreateAsync(new CreateRoleRequest(request.Name, request.Description, request.Scope,
            request.CompanyId, request.Permissions), cancellationToken))
        .ToCreatedResult(this, nameof(Get), id => new { id });

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Roles.Manage)]
    [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateRoleHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.UpdateAsync(new UpdateRoleRequest(id, request.Name, request.Description, request.Scope,
            request.CompanyId, request.Permissions, request.Version), cancellationToken)).ToActionResult(this);

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Roles.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, [FromBody] DeleteRoleHttpRequest request,
        CancellationToken cancellationToken) =>
        (await service.DeleteAsync(new DeleteRoleRequest(id, request.Version), cancellationToken)).ToActionResult(this);
}
