using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>
/// La guarda de la prueba reciente (E9). En una escritura sensible de Plataforma (<see cref="RecentPlatformProof"/>, la
/// misma regla que publica el contrato) exige la prueba reciente de la sesión del token, leída sin caché. Es un filtro
/// de autorización ordenado después del acceso, y los permisos ya se resolvieron antes: un 403 nunca reserva ni consume
/// la clave de idempotencia, así el reenvío con la misma clave después de confirmar ejecuta una sola vez. La unidad de
/// trabajo que escribe vuelve a exigir la misma prueba.
/// </summary>
public sealed class RecentPlatformProofFilter(IPlatformSessionService sessions, ProblemDetailsFactory problems)
    : IAsyncAuthorizationFilter
{
    /// <summary>Después del <c>AccessFilter</c> de cada controller (orden 0).</summary>
    public const int Order = 1;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!RecentPlatformProof.IsRequired(context.ActionDescriptor, context.HttpContext.Request.Method)) return;
        var proof = await sessions.RequireRecentProofAsync(context.HttpContext.RequestAborted);
        if (proof.IsSuccess) return;
        var problem = ProblemDetailsMapper.FromError(proof.Error, problems, context.HttpContext);
        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
    }
}
