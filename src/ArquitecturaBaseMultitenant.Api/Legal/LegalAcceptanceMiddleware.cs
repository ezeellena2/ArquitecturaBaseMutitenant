using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.Legal;

/// <summary>Bloquea rutas autenticadas mientras falte aceptar documentos vigentes; exceptúa perfil, lectura y aceptación legal.</summary>
public sealed class LegalAcceptanceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILegalAcceptanceService service)
    {
        if (!context.Request.Path.StartsWithSegments("/api") || context.User.Identity?.IsAuthenticated != true
            || context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || (HttpMethods.IsGet(context.Request.Method) && (context.Request.Path == "/api/me"
                || context.Request.Path.StartsWithSegments("/api/legal")))
            || (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/api/legal/accept"))
        {
            await next(context);
            return;
        }
        var pending = await service.GetPendingAsync(context.RequestAborted);
        if (pending.IsSuccess && pending.Value.Count == 0)
        {
            await next(context);
            return;
        }
        var error = pending.IsFailure ? pending.Error : LegalErrors.AcceptanceRequired;
        var problem = ProblemDetailsMapper.FromError(error,
            context.RequestServices.GetRequiredService<ProblemDetailsFactory>(), context);
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status403Forbidden;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
            new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
