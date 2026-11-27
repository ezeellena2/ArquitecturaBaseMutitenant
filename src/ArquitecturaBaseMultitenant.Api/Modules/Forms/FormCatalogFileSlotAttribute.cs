using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.Modules.Forms;

/// <summary>
/// La barrera de las dos rutas multipart de los catálogos, la vista previa y aplicar (C11). Corre después de la sesión, de la
/// funcionalidad y del permiso, y antes de lo que lee el cuerpo: la idempotencia, que arma su huella con el formulario, y el enlace
/// de <c>[FromForm]</c>. Sin lugar no se lee el archivo: la persona con otra operación de archivo en curso recibe 409 Busy, y la
/// organización o la instalación sin lugar, 429 TooManyAtOnce con <c>Retry-After</c>. La barrera se suelta al terminar el pedido,
/// como sea que termine. Exportar no lee un cuerpo y la toma en su servicio.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class FormCatalogFileSlotAttribute(FormWorkLeaseKind kind) : Attribute, IAsyncResourceFilter, IOrderedFilter
{
    public FormWorkLeaseKind Kind { get; } = kind;

    // Antes de la idempotencia (orden 0), que lee el formulario para su huella.
    public int Order => -2000;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        if (!http.Request.HasFormContentType)
        {
            // Sin formulario no hay archivo que leer: el enlace responde como siempre.
            await next();
            return;
        }
        var services = http.RequestServices;
        try
        {
            var personId = services.GetRequiredService<ICurrentUser>().UserId
                ?? throw new InvalidOperationException("A catalog file slot needs the person of the request.");
            var taken = await services.GetRequiredService<IFormWorkLease>().TryTakeAsync(personId, Kind, http.RequestAborted);
            if (taken.IsFailure)
            {
                context.Result = Problem(http, taken.Error);
                return;
            }
            await using var lease = taken.Value;
            await next();
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            // Quien subía cortó: no hay a quién responder, y la barrera ya se soltó.
            context.Result = new EmptyResult();
        }
    }

    private static ObjectResult Problem(HttpContext http, Error error)
    {
        var problem = ProblemDetailsMapper.FromError(error, http.RequestServices.GetRequiredService<ProblemDetailsFactory>(), http);
        if (problem.Status == StatusCodes.Status429TooManyRequests
            && error.Metadata?.TryGetValue("retryAfter", out var delay) == true && delay is int seconds)
            http.Response.Headers.RetryAfter = RetryAfterHeaderFormatter.Format(seconds);
        var result = new ObjectResult(problem) { StatusCode = problem.Status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
