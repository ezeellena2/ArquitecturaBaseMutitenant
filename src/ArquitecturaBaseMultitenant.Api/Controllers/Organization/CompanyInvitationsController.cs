using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Organization;

/// <summary>
/// Reenviar y revocar desde la ficha de una empresa las invitaciones que la incluyen, como en Usuarios (decisión del
/// 2026-10-05). Un Administrador de empresa sin <c>users.invite</c> resuelve solo las que son únicamente de su empresa,
/// sin roles de la organización ni otras empresas (<c>CompanyInvitationPolicy</c>). Cualquier otra es ajena: 404.
/// </summary>
[ApiController]
[Route("api/companies/{companyId:guid}/invitations")]
[Tags("Miembros de empresa")]
[Access(Access.Business)]
public sealed class CompanyInvitationsController(IOrganizationUserService service) : ControllerBase
{
    [HttpPost("{invitationId:guid}/resend")]
    [HasCompanyPermission(Permissions.Company.Members.Manage, organizationPermission: Permissions.Users.Invite)]
    [Idempotent]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resend([FromRoute] Guid companyId, [FromRoute] Guid invitationId,
        CancellationToken cancellationToken) =>
        (await service.ResendCompanyInvitationAsync(companyId, invitationId, cancellationToken)).ToActionResult(this);

    [HttpDelete("{invitationId:guid}")]
    [HasCompanyPermission(Permissions.Company.Members.Manage, organizationPermission: Permissions.Users.Invite)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke([FromRoute] Guid companyId, [FromRoute] Guid invitationId,
        CancellationToken cancellationToken) =>
        (await service.RevokeCompanyInvitationAsync(companyId, invitationId, cancellationToken)).ToActionResult(this);
}
