using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>Agrega las respuestas de error deducibles de cada acción, como ProblemDetails.</summary>
internal sealed class ProblemResponsesConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        foreach (var controller in application.Controllers)
        {
            if (controller.ControllerType.IsDefined(typeof(OwnProtocolAttribute), inherit: true)) continue;
            foreach (var action in controller.Actions)
            {
                var declared = controller.Filters.Concat(action.Filters)
                    .OfType<IApiResponseMetadataProvider>()
                    .Select(response => response.StatusCode)
                    .ToHashSet();

                foreach (var statusCode in ErrorStatusCodes(controller, action)
                    .Where(statusCode => !declared.Contains(statusCode)))
                {
                    action.Filters.Add(new ProducesProblemAttribute(statusCode));
                }
            }
        }
    }

    private static IEnumerable<int> ErrorStatusCodes(ControllerModel controller, ActionModel action)
    {
        var sources = action.Parameters.Select(parameter => parameter.BindingInfo?.BindingSource).ToArray();
        var attributes = controller.Attributes.Concat(action.Attributes).ToArray();
        var authorization = attributes.OfType<IAuthorizeData>().ToArray();

        if (sources.Any(source => source == BindingSource.Body || source == BindingSource.Query))
        {
            yield return StatusCodes.Status400BadRequest;
        }

        if (authorization.Length > 0 && !attributes.OfType<IAllowAnonymous>().Any())
        {
            yield return StatusCodes.Status401Unauthorized;

            if (authorization.Any(data => !string.IsNullOrEmpty(data.Policy) || !string.IsNullOrEmpty(data.Roles)))
            {
                yield return StatusCodes.Status403Forbidden;
            }
        }

        if (sources.Any(source => source == BindingSource.Path))
        {
            yield return StatusCodes.Status404NotFound;
        }

        if (attributes.LastOrDefault(attribute => attribute is EnableRateLimitingAttribute or DisableRateLimitingAttribute)
            is EnableRateLimitingAttribute)
        {
            yield return StatusCodes.Status429TooManyRequests;
        }

        yield return StatusCodes.Status500InternalServerError;
    }
}
