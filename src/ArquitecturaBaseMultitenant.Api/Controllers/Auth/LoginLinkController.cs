using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

[ApiController, AllowAnonymous, Route("api/auth/login-link"), Tags("Auth")]
public sealed class LoginLinkController(ILoginLinkService service) : ControllerBase
{
    [HttpPost("preview"), RateLimit(RateLimitProfile.LoginVerify)]
    [ProducesResponseType<LoginLinkPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Preview([FromBody] PreviewLoginLinkHttpRequest request, CancellationToken ct) =>
        (await service.PreviewAsync(new PreviewLoginLinkRequest(request.Token), ct)).ToActionResult(this);

    [HttpPost("redeem"), Idempotent, RateLimit(RateLimitProfile.LoginVerify)]
    [IdempotencyReplay(typeof(LoginLinkReplayVerifier))]
    [ProducesResponseType<LoginLinkRedeemResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Redeem([FromBody] RedeemLoginLinkHttpRequest request, CancellationToken ct) =>
        (await service.RedeemAsync(new RedeemLoginLinkRequest(request.Token, request.Access), ct)).ToActionResult(this);
}
