using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.ErrorHandling;

/// <summary>El 400 de MVC por un body o parámetro que no se puede enlazar.</summary>
internal static class MvcInvalidModelStateResponseFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var problemDetailsFactory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = problemDetailsFactory.CreateProblemDetails(
            context.HttpContext,
            statusCode: StatusCodes.Status400BadRequest,
            title: ErrorTexts.Title(ErrorType.Validation),
            detail: ErrorTexts.Get(ApiErrorCodes.InvalidRequest));

        problem.Extensions[ProblemDetailsMapper.CodeExtension] = ApiErrorCodes.InvalidRequest;
        ProblemDetailsMapper.AddTraceId(problem, context.HttpContext);

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    }
}
