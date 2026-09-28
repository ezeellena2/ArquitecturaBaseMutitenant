using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures;

/// <summary>Rutas de prueba sin persistencia que ejercitan los contratos HTTP reales.</summary>
[ApiController]
[Route("test")]
public sealed class TestController : ControllerBase
{
    [HttpGet("result/{type}")]
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

    [HttpGet("status/{status:int}")]
    public IActionResult EmptyStatus(int status) => StatusCode(status);

    [HttpPost("body")]
    public IActionResult Body([FromBody] TestBodyHttpRequest request) => NoContent();

    [HttpPost("dates")]
    public ActionResult<TestDatesHttpRequest> Dates([FromBody] TestDatesHttpRequest request) => Ok(request);

    [HttpPost("civil-time")]
    public ActionResult<TestCivilTimeHttpRequest> CivilTime([FromBody] TestCivilTimeHttpRequest request) => Ok(request);

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
}

public sealed record TestBodyHttpRequest(string? Value);

public sealed record TestDatesHttpRequest(DateTime OccurredAtUtc, DateTime? ExpiresAtUtc);

public sealed record TestCivilTimeHttpRequest(DateOnly Date, TimeOnly Time);

public sealed record TestNormalizedInputHttpRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? Empty { get; init; }

    [RawText]
    public string? Raw { get; init; }
}
