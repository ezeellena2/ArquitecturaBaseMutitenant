using System.Security.Claims;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.BusinessAccess;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que el acceso declarado por una ruta rechace modos incompatibles con 403. Una coincidencia
/// debe continuar sin cambiar el espacio activo.
/// </summary>
public sealed class AccessTests
{
    [Fact]
    public async Task Wrong_access_returns_403_with_stable_problem_code()
    {
        var context = NewContext(Access.Consumer);
        var filter = new AccessFilter([Access.Business],
            context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>());

        await filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(AccessErrors.WrongCode, problem.Extensions["code"]);
        Assert.NotNull(problem.Extensions["traceId"]);
    }

    [Fact]
    public async Task Matching_access_continues_without_changing_tenant()
    {
        var context = NewContext(Access.Business);
        var filter = new AccessFilter([Access.Business],
            context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>());

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void Access_attribute_declares_all_allowed_accesses()
    {
        var attribute = new AccessAttribute(Access.Consumer, Access.Business, Access.Platform);

        Assert.Equal([Access.Consumer, Access.Business, Access.Platform], attribute.Allowed);
    }

    [Fact]
    public void Business_signup_probe_is_limited_to_business_access()
    {
        var access = Assert.IsType<AccessAttribute>(
            Attribute.GetCustomAttribute(typeof(BusinessOnlyController), typeof(AccessAttribute)));

        Assert.Equal([Access.Business], access.Allowed);
    }

    private static AuthorizationFilterContext NewContext(Access access)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        services.AddProblemDetails();
        var provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(TenantClaimTypes.Access, access.ToString().ToLowerInvariant())], "test"));
        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), []);
    }
}
