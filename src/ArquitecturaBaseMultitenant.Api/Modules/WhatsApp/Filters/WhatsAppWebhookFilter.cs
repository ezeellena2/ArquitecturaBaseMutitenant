using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Controllers;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.Filters;

/// <summary>Autentica bytes antes del binder; solo modifica el protocolo del POST del módulo.</summary>
internal sealed class WhatsAppWebhookFilter(IWhatsAppSignatureValidator signatures, ProblemDetailsFactory problems)
    : IAsyncResourceFilter, IActionFilter, IOrderedFilter
{
    internal static readonly object OriginalBodyKey = new();
    internal const int MaximumBytes = 5 * 1024 * 1024;
    public int Order => -3000;
    private static bool Applies(ActionContext context) => context.ActionDescriptor is ControllerActionDescriptor action
        && action.ControllerTypeInfo.AsType() == typeof(WhatsAppWebhookController) && HttpMethods.IsPost(context.HttpContext.Request.Method);
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (!Applies(context)) { await next(); return; }
        var request = context.HttpContext.Request;
        if (request.ContentLength > MaximumBytes) { context.Result = TooLarge(context.HttpContext); return; }
        using var copy = new MemoryStream();
        var buffer = new byte[16 * 1024];
        try
        {
            while (true)
            {
                var read = await request.Body.ReadAsync(buffer, context.HttpContext.RequestAborted);
                if (read == 0) break;
                if (copy.Length + read > MaximumBytes) { context.Result = TooLarge(context.HttpContext); return; }
                await copy.WriteAsync(buffer.AsMemory(0, read), context.HttpContext.RequestAborted);
            }
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        { context.Result = TooLarge(context.HttpContext); return; }
        var bytes = copy.ToArray();
        if (!signatures.IsValidSignature(bytes, request.Headers["X-Hub-Signature-256"]))
        { context.Result = ErrorResult(WhatsAppErrors.InvalidSignature, context.HttpContext); return; }
        var original = request.Body;
        await using var body = new MemoryStream(bytes, writable: false);
        request.Body = body;
        context.HttpContext.Items[OriginalBodyKey] = bytes;
        try { await next(); }
        finally { request.Body = original; context.HttpContext.Items.Remove(OriginalBodyKey); }
    }
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (Applies(context) && !context.ModelState.IsValid) context.Result = ErrorResult(WhatsAppErrors.InvalidPayload, context.HttpContext);
    }
    public void OnActionExecuted(ActionExecutedContext context) { }
    private ObjectResult TooLarge(HttpContext context) => ErrorResult(Error.Validation("WhatsApp.Webhook.PayloadTooLarge", "Webhook payload is too large."), context, 413);
    private ObjectResult ErrorResult(Error error, HttpContext context, int? status = null)
    {
        var problem = ProblemDetailsMapper.FromError(error, problems, context);
        problem.Status = status ?? problem.Status;
        return new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { "application/problem+json" } };
    }
}
