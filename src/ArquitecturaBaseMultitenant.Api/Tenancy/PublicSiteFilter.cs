using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.Tenancy;

/// <summary>Oculta con el mismo 404 todo pedido que no resolvió un sitio público.</summary>
public sealed class PublicSiteFilter(IPublicSiteContext publicSite, ProblemDetailsFactory problemDetailsFactory)
    : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (publicSite.BusinessTenantId is null)
        {
            var problem = ProblemDetailsMapper.FromError(PublicPageErrors.NotFound,
                problemDetailsFactory, context.HttpContext);
            context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
        }

        return Task.CompletedTask;
    }
}
