using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Features;

/// <summary>Oculta módulos antes de evaluar permisos. Access y el tenant firmado conservan su prioridad.</summary>
internal sealed class FeatureGateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, IFeatureService features,
        ProblemDetailsFactory factory)
    {
        var endpoint = context.GetEndpoint();
        var access = endpoint?.Metadata.GetMetadata<AccessAttribute>();
        var gates = endpoint?.Metadata.GetOrderedMetadata<FeatureGateAttribute>();
        if (context.User.Identity?.IsAuthenticated != true || currentUser.Access is not { } activeAccess
            || access is null || !access.Allowed.Contains(activeAccess) || gates is null)
        {
            await next(context);
            return;
        }
        foreach (var gate in gates)
        {
            var enabledCount = 0;
            foreach (var key in gate.Features)
                if (await features.IsEnabledAsync(key, context.RequestAborted)) enabledCount++;
            var enabled = gate.RequirementType == RequirementType.All
                ? enabledCount == gate.Features.Count() : enabledCount > 0;
            if (gate.Negate) enabled = !enabled;
            if (enabled) continue;
            var problem = ProblemDetailsMapper.FromError(Error.NotFound(ApiErrorCodes.NotFound,
                "The resource was not found."), factory, context);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem,
            });
            return;
        }
        await next(context);
    }
}
