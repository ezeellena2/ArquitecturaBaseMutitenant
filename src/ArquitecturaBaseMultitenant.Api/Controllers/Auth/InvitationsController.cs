using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

/// <summary>Expone solo la vista previa y aceptación anónimas; la emisión queda dentro de InvitationIssuer.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/invitations")]
[Tags("Invitations")]
public sealed class InvitationsController(IInvitationService service, IAntiforgery antiforgery) : ControllerBase
{
    [HttpPost("preview")]
    [Consumes("application/json")]
    [EnableRateLimiting(RateLimitPolicies.InvitationPreview)]
    [ProducesResponseType<InvitationPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Preview([FromBody] PreviewInvitationHttpRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!await antiforgery.IsRequestValidAsync(HttpContext)) return InvalidAntiforgery();
        return (await service.PreviewAsync(new PreviewInvitationRequest(request.Token), cancellationToken))
            .ToActionResult(this);
    }

    [HttpPost("accept")]
    [Consumes("application/json")]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.InvitationAccept)]
    [ProducesResponseType<AcceptInvitationResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    [ProducesProblem(StatusCodes.Status429TooManyRequests)]
    [ProducesProblem(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationHttpRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!await antiforgery.IsRequestValidAsync(HttpContext)) return InvalidAntiforgery();
        return (await service.AcceptAsync(new AcceptInvitationRequest(request.Token, request.AcceptedTerms), cancellationToken))
            .ToActionResult(this);
    }

    private IActionResult InvalidAntiforgery() => Result.Failure(new ValidationError(
        new Dictionary<string, string[]> { ["requestVerificationToken"] = [ValidationTexts.Required] }))
        .ToActionResult(this);
}
