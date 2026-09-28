using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Domain.Results;
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

    [HttpGet("throw")]
    public IActionResult Throw()
    {
        ArgumentNullException.ThrowIfNull(HttpContext);
        throw new InvalidOperationException("PRIVATE_TEST_EXCEPTION");
    }
}

public sealed record TestBodyHttpRequest(string? Value);
