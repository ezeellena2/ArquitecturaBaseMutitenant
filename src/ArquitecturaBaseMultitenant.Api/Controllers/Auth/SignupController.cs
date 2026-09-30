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

/// <summary>Expone el alta personal por correo en dos pasos; la creación de cuenta y espacio queda en Application.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth/signup")]
[Tags("Auth")]
public sealed class SignupController(IAccountService service) : ControllerBase
{
    [HttpPost]
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
        return (await service.RequestSignupCodeAsync(new SignupRequest(email.Value, request.AcceptedTerms,
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
        return (await service.VerifySignupAsync(new VerifySignupRequest(email.Value, request.Code,
            request.AcceptedTerms, request.Culture, request.TimeZoneId), cancellationToken)).ToActionResult(this);
    }
}
