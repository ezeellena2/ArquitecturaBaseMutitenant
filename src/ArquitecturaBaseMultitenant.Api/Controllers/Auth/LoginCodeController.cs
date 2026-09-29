using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/auth/login-code")]
[Tags("Auth")]
public sealed class LoginCodeController(ILoginCodeService service) : ControllerBase
{
    [HttpPost]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.LoginCode)]
    [ProducesResponseType<RequestLoginCodeResponse>(StatusCodes.Status202Accepted)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestLoginCode([FromBody] RequestLoginCodeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var email = AuthEmailMapping.Parse(request.Email);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        return (await service.RequestLoginCodeAsync(new RequestLoginCodeRequest(email.Value), cancellationToken))
            .ToAcceptedResult(this);
    }

    [HttpPost("verify")]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.LoginVerify)]
    [ProducesResponseType<VerifyLoginCodeResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> VerifyLoginCode([FromBody] VerifyLoginCodeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var email = AuthEmailMapping.Parse(request.Email);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        return (await service.VerifyLoginCodeAsync(
            new VerifyLoginCodeRequest(email.Value, request.Code, request.ReturnUrl), cancellationToken))
            .ToActionResult(this);
    }
}
