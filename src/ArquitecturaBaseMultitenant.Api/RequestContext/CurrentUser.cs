using System.Globalization;
using System.Security.Claims;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Api.RequestContext;

/// <summary>Identidad y acceso del token o de la cookie de la petición.</summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor, IExecutionActorContext? executionActors = null) : ICurrentUser
{
    public DateTime? SessionStartedAtUtc => httpContextAccessor.HttpContext is { } context
        ? BrowserSessionKeys.ReadStartedAtUtc(context.User.FindFirstValue(BrowserSessionKeys.StartedAtUtc))
        : executionActors?.Actor?.SessionStartedAtUtc;

    public Guid? UserId
    {
        get
        {
            if (httpContextAccessor.HttpContext is null) return executionActors?.Actor?.OwnerUserId;
            var user = httpContextAccessor.HttpContext?.User;
            var value = user?.FindFirstValue("sub") ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, CultureInfo.InvariantCulture, out var userId) ? userId : null;
        }
    }

    public Access? Access => httpContextAccessor.HttpContext is null ? executionActors?.Actor?.Access : httpContextAccessor.HttpContext.User.FindFirstValue("access") switch
    {
        "consumer" => Domain.Users.Access.Consumer,
        "business" => Domain.Users.Access.Business,
        "platform" => Domain.Users.Access.Platform,
        _ => null,
    };
}
