using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.ErrorHandling;

/// <summary>Convierte errores HTTP y conflictos de versión; reserva 500 sin detalles para fallos inesperados.</summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
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
            ExceptionLog.Write(logger, nameof(GlobalExceptionHandler), exception);
            problem = ProblemDetailsMapper.Create(
                ErrorType.Failure, ApiErrorCodes.Unexpected, ErrorTexts.Get(ApiErrorCodes.Unexpected));
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        }))
        {
            ProblemDetailsMapper.AddTraceId(problem, httpContext);
            await httpContext.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        }
        // .NET 10 suppresses framework exception diagnostics only when a handler returns true.
        return true;
    }

}
