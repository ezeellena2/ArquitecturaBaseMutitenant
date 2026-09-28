using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArquitecturaBaseMultitenant.Api.ErrorHandling;

internal sealed class EmptyJsonBodyContentTypeFilter : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var request = context.HttpContext.Request;
        if (request.ContentType is not null
            || !context.ActionDescriptor.Parameters.Any(parameter => parameter.BindingInfo?.BindingSource == BindingSource.Body))
        {
            return;
        }

        var canHaveBody = context.HttpContext.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody;
        if (canHaveBody is false || request.ContentLength is 0)
        {
            request.ContentType = "application/json";
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
