using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

/// <summary>
/// El lugar de una subida del Inicio (ADR 0064 §2 y §4). Corre después de la sesión y del permiso, y antes de lo que lee el
/// cuerpo: la idempotencia, que arma su huella con el formulario, y el enlace de <c>[FromForm]</c>. En orden:
/// <list type="number">
/// <item>el cupo diario y el mensual (<see cref="IAssistantUploadAdmission"/>): sin cupo, el 429 de siempre y no se lee nada;</item>
/// <item>el lugar de la persona, uno entre todas las copias y sin cola: otra subida suya en curso, 429 UploadBusy;</item>
/// <item>el de la copia (<see cref="AssistantUploadLimiter"/>): con la cola llena o su organización con sus lugares, 429 UploadQueueFull;</item>
/// <item>el cuerpo tiene <c>Assistant:Uploads:BodyTimeout</c>: vencido, 400 UploadTimedOut.</item>
/// </list>
/// El formulario leído queda en la petición para la idempotencia y el enlace, también su falla, que responden como siempre.
/// Los lugares vuelven cuando termina la subida, como sea que termine.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class AssistantUploadSlotAttribute : Attribute, IAsyncResourceFilter, IOrderedFilter
{
    internal const string Scope = "assistant-upload";
    // Un lugar ocupado se libera al terminar la otra subida: se vuelve a probar enseguida.
    private const int RetryAfterSeconds = 1;

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
            var admitted = await services.GetRequiredService<IAssistantUploadAdmission>().AdmitAsync(http.RequestAborted);
            if (admitted.IsFailure)
            {
                context.Result = Problem(http, admitted.Error);
                return;
            }
            var personId = services.GetRequiredService<ICurrentUser>().UserId
                ?? throw new InvalidOperationException("An upload slot needs the person of the request.");
            await using var person = await services.GetRequiredService<IExclusiveSlots>().TryTakeAsync(Scope, personId, http.RequestAborted);
            if (person is null)
            {
                context.Result = Problem(http, AssistantErrors.AttachmentUploadBusy(RetryAfterSeconds));
                return;
            }
            using var copy = await services.GetRequiredService<AssistantUploadLimiter>().TryAcquireAsync(
                OrganizationOf(services.GetRequiredService<ITenantContext>()), http.RequestAborted);
            if (copy is null)
            {
                context.Result = Problem(http, AssistantErrors.AttachmentUploadQueueFull(RetryAfterSeconds));
                return;
            }
            if (!await ReadBodyAsync(http, services))
            {
                context.Result = Problem(http, AssistantErrors.AttachmentUploadTimedOut);
                return;
            }
            await next();
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            // Quien subía cortó: no hay a quién responder, y los lugares ya volvieron.
            context.Result = new EmptyResult();
        }
    }

    /// <summary>
    /// La parte de la cola que ocupa la subida: la de su organización, con el acceso de empresa. El lado personal y Plataforma
    /// van juntos, fuera de una (<see cref="Guid.Empty"/>): el espacio personal de cada persona no es una organización, y
    /// contado como una dejaría que las personas ocupen entre todas la cola que necesitan las organizaciones.
    /// </summary>
    private static Guid OrganizationOf(ITenantContext tenant) =>
        tenant.TenantKind == TenantKind.Business ? tenant.RequiredTenantId : Guid.Empty;

    /// <summary>Lee el formulario con su plazo; false si venció. Un cuerpo inválido queda para el enlace, como siempre.</summary>
    private static async Task<bool> ReadBodyAsync(HttpContext http, IServiceProvider services)
    {
        var timeout = services.GetRequiredService<IOptions<AssistantOptions>>().Value.Uploads.BodyTimeout;
        using var deadline = new CancellationTokenSource(timeout, services.GetRequiredService<TimeProvider>());
        using var body = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, http.RequestAborted);
        try
        {
            await http.Request.ReadFormAsync(body.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !http.RequestAborted.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            // El formulario guarda la falla: la idempotencia y el enlace la vuelven a ver y responden como siempre.
        }
        return true;
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
