using System.Globalization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.ErrorHandling;

/// <summary>
/// Comprueba títulos, detalles y metadata de errores traducidos. Impide reemplazar campos reservados y
/// conserva el tiempo de espera admitido.
/// </summary>
public sealed class ValidationProblemTests
{
    [Theory]
    [InlineData(ErrorType.Validation, "es-AR", 400, "Datos inválidos", "Revisá los campos marcados.")]
    [InlineData(ErrorType.Validation, "en-US", 400, "Invalid data", "Check the highlighted fields.")]
    [InlineData(ErrorType.Unauthorized, "es-AR", 401, "No autenticado", "Tenés que iniciar sesión para continuar.")]
    [InlineData(ErrorType.Unauthorized, "en-US", 401, "Unauthorized", "You need to sign in to continue.")]
    [InlineData(ErrorType.Forbidden, "es-AR", 403, "Acceso denegado", "No tenés permiso para realizar esta acción.")]
    [InlineData(ErrorType.Forbidden, "en-US", 403, "Forbidden", "You don't have permission to perform this action.")]
    [InlineData(ErrorType.NotFound, "es-AR", 404, "No encontrado", "No encontramos lo que buscás.")]
    [InlineData(ErrorType.NotFound, "en-US", 404, "Not found", "We couldn't find what you're looking for.")]
    [InlineData(ErrorType.Conflict, "es-AR", 409, "Conflicto", "La solicitud entra en conflicto con el estado actual del recurso.")]
    [InlineData(ErrorType.Conflict, "en-US", 409, "Conflict", "The request conflicts with the current state of the resource.")]
    [InlineData(ErrorType.TooManyRequests, "es-AR", 429, "Demasiadas solicitudes", "Hiciste demasiadas solicitudes. Esperá un momento y volvé a intentar.")]
    [InlineData(ErrorType.TooManyRequests, "en-US", 429, "Too many requests", "Too many requests. Wait a moment and try again.")]
    [InlineData(ErrorType.Failure, "es-AR", 500, "Error del servidor", "Ocurrió un error inesperado. Si el problema continúa, informá el código de seguimiento.")]
    [InlineData(ErrorType.Failure, "en-US", 500, "Server error", "An unexpected error occurred. If the problem persists, report the trace id.")]
    public void Every_error_type_has_translated_title_and_detail(
        ErrorType type, string culture, int status, string title, string detail)
    {
        using var scope = new CultureScope(culture);
        var code = type switch
        {
            ErrorType.Validation => "Validation.Failed",
            ErrorType.Unauthorized => "Http.Unauthorized",
            ErrorType.Forbidden => "Http.Forbidden",
            ErrorType.NotFound => "Http.NotFound",
            ErrorType.Conflict => "Http.Conflict",
            ErrorType.TooManyRequests => "Http.TooManyRequests",
            _ => "General.Unexpected",
        };
        Error error = type == ErrorType.Validation
            ? new ValidationError(new Dictionary<string, string[]> { ["email"] = ["Invalid email."] })
            : new Error(code, "Fallback.", type);

        var problem = Map(error);

        Assert.Equal(status, problem.Status);
        Assert.Equal(title, problem.Title);
        Assert.Equal(detail, problem.Detail);
        Assert.Equal(code, problem.Extensions["code"]);
        Assert.NotNull(problem.Extensions["traceId"]);
        Assert.Equal(type == ErrorType.Validation, problem.Extensions.ContainsKey("errors"));
    }

    [Fact]
    public void Metadata_cannot_override_reserved_extensions_but_exposes_retry_after()
    {
        var error = Error.TooManyRequests("Http.TooManyRequests", "Fallback.",
            new Dictionary<string, object?>
            {
                ["code"] = "Fake.Code",
                ["traceId"] = "fake-trace",
                ["errors"] = "fake-fields",
                ["retryAfter"] = 12,
            });

        var problem = Map(error);

        Assert.Equal("Http.TooManyRequests", problem.Extensions["code"]);
        Assert.NotEqual("fake-trace", problem.Extensions["traceId"]);
        Assert.False(problem.Extensions.ContainsKey("errors"));
        Assert.Equal(12, problem.Extensions["retryAfter"]);
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Map(Error error)
    {
        using var services = new ServiceCollection().AddControllers().Services.BuildServiceProvider();
        var context = new DefaultHttpContext { TraceIdentifier = "test-request" };
        return ProblemDetailsMapper.FromError(error, services.GetRequiredService<ProblemDetailsFactory>(), context);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _previousUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string culture)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }
}
