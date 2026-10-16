using System.Reflection;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>
/// Qué acciones piden una prueba reciente del segundo factor (E9): toda escritura con acceso Plataforma que exige un
/// permiso de <see cref="PlatformPermissions.RequiringRecentProof"/>. Se deduce de la metadata de la ruta, también en
/// las que instala un módulo, sin que el núcleo lo conozca.
/// </summary>
public static class RecentPlatformProof
{
    public static bool IsRequired(ActionDescriptor action, string httpMethod)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action is not ControllerActionDescriptor controller || HttpMethods.IsGet(httpMethod) || HttpMethods.IsHead(httpMethod))
            return false;
        var access = controller.ControllerTypeInfo.GetCustomAttributes<AccessAttribute>(inherit: true)
            .Concat(controller.MethodInfo.GetCustomAttributes<AccessAttribute>(inherit: true)).ToArray();
        if (access.Length == 0 || access.Any(value => value.Allowed is not [Access.Platform])) return false;
        return controller.ControllerTypeInfo.GetCustomAttributes<HasPlatformPermissionAttribute>(inherit: true)
            .Concat(controller.MethodInfo.GetCustomAttributes<HasPlatformPermissionAttribute>(inherit: true))
            .Any(permission => PlatformPermissions.RequiringRecentProof.Contains(permission.Permission));
    }
}
