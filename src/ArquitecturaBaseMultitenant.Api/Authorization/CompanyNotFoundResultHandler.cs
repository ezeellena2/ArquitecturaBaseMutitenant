using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>
/// Responde 404 Tenancy.Company.NotFound cuando la autorización falló porque la empresa de la ruta no es de la
/// organización activa (E9, CC2): un identificador ajeno no se distingue de uno inventado. El resto sigue con el
/// resultado del framework.
/// </summary>
internal sealed class CompanyNotFoundResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _framework = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);
        if (!authorizeResult.Forbidden || authorizeResult.AuthorizationFailure?.FailureReasons.OfType<CompanyNotFoundReason>().Any() != true)
        {
            await _framework.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var problem = ProblemDetailsMapper.FromError(CompanyErrors.NotFound,
            context.RequestServices.GetRequiredService<ProblemDetailsFactory>(), context);
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });
    }
}
