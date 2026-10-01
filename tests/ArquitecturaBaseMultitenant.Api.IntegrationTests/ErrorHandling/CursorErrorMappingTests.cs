using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.ErrorHandling;

/// <summary>
/// Comprueba que un cursor inválido se informe como error de validación del campo after. Mantiene el
/// rechazo ligado a la entrada que el cliente debe corregir.
/// </summary>
public sealed class CursorErrorMappingTests
{
    [Fact]
    public async Task Invalid_cursor_returns_validation_problem_for_after_field()
    {
        var writer = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(writer, NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();

        Assert.True(await handler.TryHandleAsync(context, new InvalidCursorException(),
            TestContext.Current.CancellationToken));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.NotNull(writer.Problem);
        Assert.Equal("Validation.Failed", writer.Problem.Extensions[ProblemDetailsMapper.CodeExtension]);
        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
            writer.Problem.Extensions[ProblemDetailsMapper.ErrorsExtension]);
        Assert.Equal([ValidationTexts.CursorInvalid], errors["after"]);
    }

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Problem { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Problem = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Problem = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }
    }
}
