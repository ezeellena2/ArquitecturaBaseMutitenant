using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Organization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using OrganizationUserContractResponse = ArquitecturaBaseMultitenant.Api.Contracts.Organization.OrganizationUserResponse;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

[ApiController]
[Route("api/users")]
[Tags("Usuarios de la organización")]
[Access(Access.Business)]
public sealed class OrganizationUsersController(IOrganizationUserService service) : ControllerBase
{
    [HttpGet("paged")]
    [HasPermission(Permissions.Users.Read)]
    [ProducesResponseType<PagedResult<OrganizationUserContractResponse>>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] OrganizationUserListHttpQuery query,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(query.ToModel(), cancellationToken);
        if (result.IsFailure) return result.ToActionResult(this);
        var page = result.Value;
        return Ok(new PagedResult<OrganizationUserContractResponse>([.. page.Items.Select(item => item.ToHttpResponse())],
            page.Page, page.PageSize, page.TotalCount));
    }

    [HttpGet("filter-counts")]
    [HasPermission(Permissions.Users.Read)]
    [ProducesResponseType<ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers.OrganizationUserFilterCountsResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFilterCounts([FromQuery] OrganizationUserListHttpQuery query,
        CancellationToken cancellationToken) =>
        (await service.GetFilterCountsAsync(query.ToModel(), cancellationToken)).ToActionResult(this);

    [HttpGet("{memberId:guid}")]
    [HasPermission(Permissions.Users.Read)]
    [ProducesResponseType<OrganizationUserContractResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid memberId, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(memberId, cancellationToken);
        return result.IsFailure ? result.ToActionResult(this) : Ok(result.Value.ToHttpResponse());
    }

    [HttpPost("invitations")]
    [HasPermission(Permissions.Users.Invite)]
    [Idempotent]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Invite([FromBody] InviteOrganizationUserHttpRequest request,
        CancellationToken cancellationToken)
    {
        var model = request.ToModel();
        if (model.IsFailure) return model.ToActionResult(this);
        var result = await service.InviteAsync(model.Value, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToActionResult(this);
    }

    [HttpPost("invitations/{invitationId:guid}/resend")]
    [HasPermission(Permissions.Users.Invite)]
    [Idempotent]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resend([FromRoute] Guid invitationId, CancellationToken cancellationToken) =>
        (await service.ResendInvitationAsync(invitationId, cancellationToken)).ToActionResult(this);

    [HttpDelete("invitations/{invitationId:guid}")]
    [HasPermission(Permissions.Users.Invite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke([FromRoute] Guid invitationId, CancellationToken cancellationToken) =>
        (await service.RevokeInvitationAsync(invitationId, cancellationToken)).ToActionResult(this);

    [HttpPut("{memberId:guid}")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromRoute] Guid memberId,
        [FromBody] UpdateOrganizationUserHttpRequest request, CancellationToken cancellationToken) =>
        (await service.UpdateAsync(request.ToModel(memberId), cancellationToken)).ToActionResult(this);

    [HttpPut("{memberId:guid}/status")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus([FromRoute] Guid memberId,
        [FromBody] ChangeOrganizationUserStatusHttpRequest request, CancellationToken cancellationToken) =>
        (await service.ChangeStatusAsync(new ChangeOrganizationUserStatusRequest(memberId, request.IsActive,
            request.Version), cancellationToken)).ToActionResult(this);

    [HttpDelete("{memberId:guid}")]
    [HasPermission(Permissions.Users.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove([FromRoute] Guid memberId, [FromQuery] uint? version,
        CancellationToken cancellationToken) =>
        (await service.RemoveAsync(memberId, version, cancellationToken)).ToActionResult(this);
}
