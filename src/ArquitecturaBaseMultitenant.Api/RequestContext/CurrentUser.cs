using System.Globalization;
using System.Security.Claims;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Api.RequestContext;

/// <summary>Identidad y acceso del token o de la cookie de la petición.</summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var value = user?.FindFirstValue("sub") ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, CultureInfo.InvariantCulture, out var userId) ? userId : null;
        }
    }

    public Access? Access => httpContextAccessor.HttpContext?.User.FindFirstValue("access") switch
    {
        "consumer" => Domain.Users.Access.Consumer,
        "business" => Domain.Users.Access.Business,
        "platform" => Domain.Users.Access.Platform,
        _ => null,
    };
}
