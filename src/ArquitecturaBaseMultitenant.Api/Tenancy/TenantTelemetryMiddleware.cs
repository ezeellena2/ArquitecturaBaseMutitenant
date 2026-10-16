using System.Diagnostics;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

namespace ArquitecturaBaseMultitenant.Api.Tenancy;

/// <summary>
/// Etiqueta el span del pedido con la organización del acceso ya validado (E9): <c>tenant.id</c> y <c>tenant.kind</c>
/// salen del contexto que fijó <see cref="TenantResolutionMiddleware"/>, nunca del Host. Plataforma, un pedido anónimo y
/// el sitio público quedan sin organización. Las métricas no llevan el id: sería una dimensión sin tope.
/// </summary>
public sealed class TenantTelemetryMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (tenant.TenantId is { } tenantId && Activity.Current is { } activity)
        {
            activity.SetTag("tenant.id", tenantId.ToString("D"));
            activity.SetTag("tenant.kind", tenant.TenantKind?.ToString());
        }

        return next(context);
    }
}
