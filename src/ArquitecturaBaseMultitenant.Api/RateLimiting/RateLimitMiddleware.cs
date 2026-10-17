using System.Globalization;
using System.Security.Claims;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.RateLimiting;

/// <summary>
/// Admite cada petición de la API en sus cuotas compartidas (E9), después de autenticar y de resolver la organización: la
/// IP real (la pública en el sitio de una organización), la identidad firmada, la organización del claim y el perfil que
/// declare la ruta. Quedan afuera los assets, los probes y <c>/connect</c>. Un rechazo es 429 con retryAfter y Retry-After.
/// </summary>
public sealed class RateLimitMiddleware(RequestDelegate next)
{
    private static readonly PathString Api = new("/api");

    public async Task InvokeAsync(HttpContext context, IRateLimitService rateLimits, ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(context);
        var endpoint = context.GetEndpoint();
        var route = endpoint?.Metadata.GetMetadata<RateLimitAttribute>();
        var isApi = context.Request.Path.StartsWithSegments(Api);
        if (!isApi && route is null)
        {
            await next(context);
            return;
        }

        var userId = SignedUserId(context.User);
        Guid? tenantId = null;
        var profiles = new List<RateLimitProfile>(4);
        if (isApi)
        {
            // El sitio público no inventa una identidad ni una organización por el subdominio.
            if (endpoint?.Metadata.GetMetadata<PublicSiteAttribute>() is not null)
            {
                profiles.Add(RateLimitProfile.PublicIp);
            }
            else
            {
                profiles.Add(RateLimitProfile.Ip);
                tenantId = tenant.TenantId;
                if (tenantId is not null) profiles.Add(RateLimitProfile.Tenant);
            }

            if (userId is not null) profiles.Add(RateLimitProfile.Identity);
        }

        // La cuota de persona u organización de una ruta no aplica a quien todavía no la tiene: la IP ya cuenta el pedido y
        // el acceso lo rechaza después con 401.
        if (route is not null && RateLimitProfiles.PartitionOf(route.Profile) switch
        {
            RateLimitPartitionKind.Identity => userId is not null,
            RateLimitPartitionKind.Tenant => tenantId is not null,
            _ => true,
        })
            profiles.Add(route.Profile);
        var admitted = await rateLimits.AdmitAsync(new RateLimitRequest(profiles,
            context.Connection.RemoteIpAddress?.ToString(), userId, tenantId), context.RequestAborted);
        if (admitted.IsSuccess)
        {
            await next(context);
            return;
        }

        await RejectAsync(context, admitted.Error, endpoint?.Metadata.GetMetadata<IRateLimitRejection>());
    }

    /// <summary>Escribe el 429 de una cuota: el error de la ruta, si declara uno, o el de las cuotas compartidas.</summary>
    internal static async Task RejectAsync(HttpContext context, Error error, IRateLimitRejection? rejection)
    {
        var seconds = error.Metadata?.TryGetValue(LoginCodeErrors.RetryAfterKey, out var value) == true
            && value is int retryAfter ? retryAfter : 1;
        var problem = ProblemDetailsMapper.FromError(rejection?.Rejected(seconds) ?? error,
            context.RequestServices.GetRequiredService<ProblemDetailsFactory>(), context);
        problem.Extensions[LoginCodeErrors.RetryAfterKey] = seconds;
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = RetryAfterHeaderFormatter.Format(seconds);
        await context.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }

    internal static Guid? SignedUserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && Guid.TryParse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier),
            CultureInfo.InvariantCulture, out var userId) && userId != Guid.Empty
            ? userId : null;
}
