using ArquitecturaBaseMultitenant.Api.Contracts.Auth;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace ArquitecturaBaseMultitenant.Api.Controllers.Auth;

/// <summary>Verifica por correo la identidad empresarial antes de crear su organización.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth/business-signup")]
[Tags("Auth")]
public sealed class BusinessSignupController(IBusinessSignupService service, IAntiforgery antiforgery) : ControllerBase
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
        var email = AuthContactMapping.ParseEmail(request.Email, request.Phone, request.Channel);
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
        var email = AuthContactMapping.ParseEmail(request.Email, request.Phone, request.Channel);
        if (email.IsFailure) return Result.Failure(email.Error).ToActionResult(this);
        return (await service.VerifyEmailCodeAsync(new VerifySignupRequest(email.Value, request.Code,
            request.AcceptedTerms, request.Culture, request.TimeZoneId), cancellationToken)).ToActionResult(this);
    }

    [HttpPost]
    [Consumes("application/json")]
    [Idempotent]
    [ProducesResponseType<BusinessSignupResponse>(StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status401Unauthorized)]
    [ProducesProblem(StatusCodes.Status403Forbidden)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    [ProducesProblem(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] BusinessSignupHttpRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!await antiforgery.IsRequestValidAsync(HttpContext)) return InvalidAntiforgery();
        var applicationSession = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!applicationSession.Succeeded
            || !Guid.TryParse(applicationSession.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || userId == Guid.Empty)
            return Result.Failure(Error.Unauthorized("Auth.BusinessSignup.IdentityRequired",
                "Sign in before creating an organization.")).ToActionResult(this);
        if (!Guid.TryParse(Request.Headers["Idempotency-Key"], out var operationId) || operationId == Guid.Empty)
            return Result.Failure(BusinessSignupErrors.IdentityRequired).ToActionResult(this);

        var businessSignup = new BusinessSignupRequest(request.OrganizationName, request.Slug,
            request.FirstCompanyName, request.TaxId is null ? null
                : new BusinessSignupTaxId(request.TaxId.Type, request.TaxId.Number), request.AcceptedTerms);
        return (await service.CreateOrganizationAsync(userId, operationId, businessSignup, cancellationToken))
            .ToActionResult(this);
    }

    private IActionResult InvalidAntiforgery() => Result.Failure(new ValidationError(
        new Dictionary<string, string[]> { ["requestVerificationToken"] = [ValidationTexts.Required] }))
        .ToActionResult(this);
}
