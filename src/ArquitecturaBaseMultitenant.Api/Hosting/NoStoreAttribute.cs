using Microsoft.AspNetCore.Mvc.Filters;

namespace ArquitecturaBaseMultitenant.Api.Hosting;

/// <summary>
/// Respuestas que llevan secretos o pruebas (E9): Cache-Control: no-store en todos los resultados de la acción, también
/// en un replay o un rechazo de la idempotencia, porque corre antes que ese filtro.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class NoStoreAttribute : Attribute, IResourceFilter, IOrderedFilter
{
    public int Order => -1;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HttpContext.Response.Headers.CacheControl = "no-store";
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
