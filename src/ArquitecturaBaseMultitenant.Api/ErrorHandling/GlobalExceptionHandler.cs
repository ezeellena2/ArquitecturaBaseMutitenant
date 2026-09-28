using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.ErrorHandling;

/// <summary>Convierte errores de lectura HTTP en 400 y demás excepciones en un 500 sin detalles internos.</summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        if (exception is BadHttpRequestException badRequest)
        {
            problem = ProblemDetailsMapper.Create(
                ErrorType.Validation, ApiErrorCodes.InvalidRequest, ErrorTexts.Get(ApiErrorCodes.InvalidRequest), badRequest.StatusCode);
        }
        else
        {
            LogUnhandledException(logger, exception);
            problem = ProblemDetailsMapper.Create(
                ErrorType.Failure, ApiErrorCodes.Unexpected, ErrorTexts.Get(ApiErrorCodes.Unexpected));
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing the request")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);
}
