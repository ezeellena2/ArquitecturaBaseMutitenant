using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Features;

/// <summary>Un módulo apagado responde como una ruta inexistente, sin exponer su clave.</summary>
internal sealed class DisabledFeatureHandler(ProblemDetailsFactory factory) : IDisabledFeaturesHandler
{
    public Task HandleDisabledFeatures(IEnumerable<string> features, ActionExecutingContext context)
    {
        var problem = ProblemDetailsMapper.FromError(Error.NotFound(ApiErrorCodes.NotFound,
            "The resource was not found."), factory, context.HttpContext);
        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
        return Task.CompletedTask;
    }
}
