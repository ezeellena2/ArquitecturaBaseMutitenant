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

/// <summary>Verifica por correo la identidad empresarial antes de crear su organización.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth/business-signup")]
[Tags("Auth")]
public sealed class BusinessSignupController(IBusinessSignupService service) : ControllerBase
{
    [HttpPost("code")]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.LoginCode)]
    [ProducesResponseType<RequestLoginCodeResponse>(StatusCodes.Status202Accepted)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RequestCode([FromBody] SignupHttpRequest request,
        CancellationToken cancellationToken)
    {
        var email = AuthEmailMapping.Parse(request.Email);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        return (await service.RequestEmailCodeAsync(new SignupRequest(email.Value, request.AcceptedTerms,
            request.Culture, request.TimeZoneId), cancellationToken)).ToAcceptedResult(this);
    }

    [HttpPost("verify")]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.LoginVerify)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Verify([FromBody] VerifySignupHttpRequest request,
        CancellationToken cancellationToken)
    {
        var email = AuthEmailMapping.Parse(request.Email);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        return (await service.VerifyEmailCodeAsync(new VerifySignupRequest(email.Value, request.Code,
            request.AcceptedTerms, request.Culture, request.TimeZoneId), cancellationToken)).ToActionResult(this);
    }
}
