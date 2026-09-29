using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.Tenancy;

/// <summary>Rechaza un acceso distinto al declarado, sin elegir tenant por host o request.</summary>
public sealed class AccessFilter(IReadOnlyList<Access> allowed, ProblemDetailsFactory problemDetailsFactory)
    : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var principal = context.HttpContext.User;
        Error? error = null;
        if (principal.Identity?.IsAuthenticated != true)
        {
            error = Error.Unauthorized(ApiErrorCodes.Unauthorized, "Authentication is required.");
        }
        else
        {
            var rawAccess = principal.FindFirst(TenantClaimTypes.Access)?.Value;
            if (!Enum.TryParse<Access>(rawAccess, ignoreCase: true, out var access)
                || !Enum.IsDefined(access) || !allowed.Contains(access))
            {
                error = AccessErrors.Wrong;
            }
        }

        if (error is not null)
        {
            var problem = ProblemDetailsMapper.FromError(error, problemDetailsFactory, context.HttpContext);
            context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
        }

        return Task.CompletedTask;
    }
}
