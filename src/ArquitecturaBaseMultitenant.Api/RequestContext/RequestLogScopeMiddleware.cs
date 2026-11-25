using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

namespace ArquitecturaBaseMultitenant.Api.RequestContext;

/// <summary>
/// Los logs de un pedido con sesión llevan en un scope el id de quien pide y el de la organización del acceso ya
/// validado (N10): con el código que ve la persona en un error (el traceId, que la consola escribe con los scopes del
/// pedido) se encuentran el error, la ruta, la cuenta y la organización. Solo los ids, nunca el correo ni el número.
/// Va después de <c>TenantResolutionMiddleware</c>: antes no se sabe quién pide ni en qué organización.
/// </summary>
public sealed class RequestLogScopeMiddleware(RequestDelegate next, ILogger<RequestLogScopeMiddleware> logger)
{
    private static readonly Func<ILogger, Guid, IDisposable?> BeginUserScope =
        LoggerMessage.DefineScope<Guid>("UserId:{UserId}");

    private static readonly Func<ILogger, Guid, Guid, IDisposable?> BeginUserAndTenantScope =
        LoggerMessage.DefineScope<Guid, Guid>("UserId:{UserId} TenantId:{TenantId}");

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        using (Begin(logger, context))
        {
            await next(context);
        }
    }

    /// <summary>
    /// El scope con los ids del pedido; sin sesión, ninguno. Una excepción sale del scope del pedido antes de llegar al
    /// manejador de errores, que abre el suyo con este método para que el log del 500 los lleve.
    /// </summary>
    public static IDisposable? Begin(ILogger logger, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.RequestServices.GetService<ICurrentUser>()?.UserId is not { } userId) return null;
        return context.RequestServices.GetService<ITenantContext>()?.TenantId is { } tenantId
            ? BeginUserAndTenantScope(logger, userId, tenantId)
            : BeginUserScope(logger, userId);
    }
}
