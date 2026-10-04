using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.Contracts.Platform;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Platform;

/// <summary>Operadores de Plataforma (ADR 0042): invitar y cambiar o quitar sus permisos.</summary>
[ApiController]
[Tags("Operadores de Plataforma")]
[Access(Access.Platform)]
[HasPlatformPermission(PlatformPermissions.Operators.Manage)]
public sealed class PlatformOperatorsController(IPlatformOperatorInvitationService invitations,
    IPlatformOperatorService operators) : ControllerBase
{
    [HttpPost("api/platform/operators/invitations")]
    [Idempotent]
    [ProducesResponseType<PlatformOperatorInvitationResponse>(StatusCodes.Status201Created)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Invite([FromBody] InvitePlatformOperatorHttpRequest request, CancellationToken ct)
    {
        var email = AuthEmailMapping.Parse(request.Email);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        var invited = await invitations.InviteAsync(new InvitePlatformOperatorRequest(email.Value, request.Permissions), ct);
        return invited.IsFailure
            ? Result.Failure(invited.Error).ToActionResult(this)
            : Created($"/api/platform/accounts/{invited.Value.Id:D}", invited.Value);
    }

    [HttpPut("api/platform/accounts/{id:guid}/operator")]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePermissions(Guid id, [FromBody] ChangePlatformOperatorHttpRequest request,
        CancellationToken ct) =>
        (await operators.ChangePermissionsAsync(id, request.ToModel(), ct)).ToActionResult(this);

    [HttpDelete("api/platform/accounts/{id:guid}/operator")]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid id, [FromBody] PlatformAccountActionHttpRequest request,
        CancellationToken ct) =>
        (await operators.RemoveAsync(id, request.ToModel(), ct)).ToActionResult(this);
}
