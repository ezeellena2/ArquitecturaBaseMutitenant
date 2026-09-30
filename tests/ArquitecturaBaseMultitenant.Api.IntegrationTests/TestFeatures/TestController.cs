using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures;

/// <summary>Rutas de prueba sin persistencia que ejercitan los contratos HTTP reales.</summary>
[ApiController]
[Route("test")]
public sealed class TestController : ControllerBase
{
    [HttpGet("result/{type}")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status409Conflict)]
    [ProducesProblem(StatusCodes.Status429TooManyRequests)]
    public IActionResult ResultByType(ErrorType type)
    {
        Error error = type switch
        {
            ErrorType.Validation => Error.Validation("Request.Invalid", "Invalid request."),
            ErrorType.Unauthorized => Error.Unauthorized("Http.Unauthorized", "Unauthorized."),
            ErrorType.Forbidden => Error.Forbidden("Http.Forbidden", "Forbidden."),
            ErrorType.NotFound => Error.NotFound("Http.NotFound", "Not found."),
            ErrorType.Conflict => Error.Conflict("Http.Conflict", "Conflict."),
            ErrorType.TooManyRequests => Error.TooManyRequests("Http.TooManyRequests", "Too many requests.",
                new Dictionary<string, object?> { ["retryAfter"] = 12 }),
            _ => Error.Failure("General.Unexpected", "Unexpected error."),
        };

        return Result.Failure(error).ToActionResult(this);
    }

    [HttpGet("validation")]
    public IActionResult Validation() => Result.Failure(new ValidationError(
        new Dictionary<string, string[]> { ["email"] = ["Ingresá un correo válido."] })).ToActionResult(this);

    [HttpGet("rate-limit")]
    [EnableRateLimiting("test-rejection")]
    public IActionResult RateLimit() => NoContent();

    [HttpGet("login-code-rate-limit")]
    [EnableRateLimiting("login-code")]
    public IActionResult LoginCodeRateLimit() => NoContent();

    [HttpGet("login-verify-rate-limit")]
    [EnableRateLimiting("login-verify")]
    public IActionResult LoginVerifyRateLimit() => NoContent();

    [HttpGet("status/{status:int}")]
    public IActionResult EmptyStatus(int status) => StatusCode(status);

    [HttpGet("paged")]
    public IActionResult Paged([FromQuery] int pageSize) => Ok(pageSize);

    [HttpPost("body")]
    public IActionResult Body([FromBody] TestBodyHttpRequest request) => NoContent();

    [HttpPost("dates")]
    public ActionResult<TestDatesHttpRequest> Dates([FromBody] TestDatesHttpRequest request) => Ok(request);

    [HttpPost("civil-time")]
    public ActionResult<TestCivilTimeHttpRequest> CivilTime([FromBody] TestCivilTimeHttpRequest request) => Ok(request);

    [HttpPost("enum")]
    public ActionResult<TestEnumHttpRequest> Enum([FromBody] TestEnumHttpRequest request) => Ok(request);

    [HttpPost("money")]
    public ActionResult<Money> Money([FromBody] Money money) => Ok(money);

    [HttpPost("normalized-input")]
    public ActionResult<TestNormalizedInputHttpRequest> NormalizedInput(
        [FromBody] TestNormalizedInputHttpRequest request) => Ok(request);

    [HttpGet("throw")]
    public IActionResult Throw()
    {
        ArgumentNullException.ThrowIfNull(HttpContext);
        throw new InvalidOperationException("PRIVATE_TEST_EXCEPTION");
    }

    [HttpPost("auth/request-code")]
    public async Task<IActionResult> RequestCode([FromBody] TestRequestCode body,
        [FromServices] ILoginCodeService loginCodes, CancellationToken cancellationToken) =>
        (await loginCodes.RequestLoginCodeAsync(new RequestLoginCodeRequest(Email.Create(body.Email).Value),
            cancellationToken)).ToActionResult(this);

    [HttpPost("auth/verify-code")]
    public async Task<IActionResult> VerifyCode([FromBody] TestVerifyCode body,
        [FromServices] ILoginCodeService loginCodes, CancellationToken cancellationToken) =>
        (await loginCodes.VerifyLoginCodeAsync(new VerifyLoginCodeRequest(Email.Create(body.Email).Value,
            body.Code, body.ReturnUrl), cancellationToken)).ToActionResult(this);

    [HttpPost("auth/signup")]
    public async Task<IActionResult> Signup([FromBody] TestSignup body,
        [FromServices] IAccountService accounts, CancellationToken cancellationToken) =>
        (await accounts.RequestSignupCodeAsync(new SignupRequest(Email.Create(body.Email).Value,
            body.AcceptedTerms, body.Culture, body.TimeZoneId), cancellationToken)).ToAcceptedResult(this);

    [HttpPost("auth/signup/verify")]
    public async Task<IActionResult> VerifySignup([FromBody] TestVerifySignup body,
        [FromServices] IAccountService accounts, CancellationToken cancellationToken) =>
        (await accounts.VerifySignupAsync(new VerifySignupRequest(Email.Create(body.Email).Value,
            body.Code, body.AcceptedTerms, body.Culture, body.TimeZoneId), cancellationToken)).ToActionResult(this);

    [HttpPost("auth/external-ticket")]
    public async Task<IActionResult> ExternalTicket([FromBody] TestExternalTicket body,
        [FromServices] SignInManager<ApplicationUser> signIn)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, "google-subject"),
            new(ClaimTypes.Email, "verified@example.test"),
        ];
        if (body.EmailVerified is { } verified)
            claims.Add(new Claim("email_verified", verified ? "True" : "False"));
        var properties = signIn.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, "/test/auth/external-info");
        await HttpContext.SignInAsync(IdentityConstants.ExternalScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, GoogleDefaults.AuthenticationScheme)), properties);
        return NoContent();
    }

    [HttpGet("auth/external-info")]
    public async Task<IActionResult> ExternalInfo([FromServices] ISignInService signIn,
        CancellationToken cancellationToken)
    {
        var login = await signIn.GetExternalLoginAsync(cancellationToken);
        return login is null ? NotFound() : Ok(new { login.Provider, login.EmailVerified });
    }
}

public sealed record TestRequestCode(string Email);
public sealed record TestVerifyCode(string Email, string Code, string ReturnUrl);
public sealed record TestSignup(string Email, bool AcceptedTerms, string? Culture, string? TimeZoneId);
public sealed record TestVerifySignup(string Email, string Code, bool AcceptedTerms,
    string? Culture, string? TimeZoneId);
public sealed record TestExternalTicket(bool? EmailVerified);

public sealed record TestBodyHttpRequest(string? Value);

public sealed record TestDatesHttpRequest(DateTime OccurredAtUtc, DateTime? ExpiresAtUtc);

public sealed record TestCivilTimeHttpRequest(DateOnly Date, TimeOnly Time);

public sealed record TestEnumHttpRequest(TestStatus Status);

public enum TestStatus
{
    Pending,
    Ready,
}

public sealed record TestNormalizedInputHttpRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? Empty { get; init; }

    [RawText]
    public string? Raw { get; init; }
}
