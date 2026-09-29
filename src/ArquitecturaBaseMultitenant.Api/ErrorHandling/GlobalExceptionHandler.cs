using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.ErrorHandling;

/// <summary>Convierte errores HTTP y conflictos de versión; reserva 500 sin detalles para fallos inesperados.</summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        if (exception is InvalidCursorException)
        {
            problem = ProblemDetailsMapper.Create(
                ErrorType.Validation, ValidationError.ErrorCode, ErrorTexts.Get(ValidationError.ErrorCode));
            problem.Extensions[ProblemDetailsMapper.ErrorsExtension] =
                new Dictionary<string, string[]> { ["after"] = [ValidationTexts.CursorInvalid] };
        }
        else if (exception is BadHttpRequestException badRequest)
        {
            problem = ProblemDetailsMapper.Create(
                ErrorType.Validation, ApiErrorCodes.InvalidRequest, ErrorTexts.Get(ApiErrorCodes.InvalidRequest), badRequest.StatusCode);
        }
        else if (exception is ConcurrencyConflictException)
        {
            problem = ProblemDetailsMapper.Create(
                ErrorType.Conflict,
                ApiErrorCodes.ConcurrencyConflict,
                ErrorTexts.Get(ApiErrorCodes.ConcurrencyConflict));
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
