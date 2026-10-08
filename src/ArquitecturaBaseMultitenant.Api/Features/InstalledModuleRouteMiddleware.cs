namespace ArquitecturaBaseMultitenant.Api.Features;

/// <summary>Sin contribuidor disponible, devuelve el 404 común antes de ejecutar una ruta técnica.</summary>
internal sealed class InstalledModuleRouteMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IEnumerable<IInstalledModuleRouteAvailability> routes)
    {
        var route = context.GetEndpoint()?.Metadata.GetMetadata<InstalledModuleRouteAttribute>();
        if (route is not null && !routes.Any(value => value.Key == route.Key && value.IsAvailable))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next(context);
    }
}
