using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

[ApiController, AllowAnonymous, Route("api/auth/login-link"), Tags("Auth")]
public sealed class LoginLinkController(ILoginLinkService service) : ControllerBase
{
    [HttpPost("preview"), EnableRateLimiting(RateLimitPolicies.LoginVerify)]
    [ProducesResponseType<LoginLinkPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Preview([FromBody] PreviewLoginLinkHttpRequest request, CancellationToken ct) =>
        (await service.PreviewAsync(new PreviewLoginLinkRequest(request.Token), ct)).ToActionResult(this);

    [HttpPost("redeem"), Idempotent, EnableRateLimiting(RateLimitPolicies.LoginVerify)]
    [ProducesResponseType<LoginLinkRedeemResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Redeem([FromBody] RedeemLoginLinkHttpRequest request, CancellationToken ct) =>
        (await service.RedeemAsync(new RedeemLoginLinkRequest(request.Token, request.Access), ct)).ToActionResult(this);
}
