using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>Comprueba permisos actuales del acceso, sin incluirlos en un token de vida larga.</summary>
internal sealed class PermissionAuthorizationHandler(ICurrentUser currentUser, ITenantContext tenantContext,
    IPermissionService permissions, IPlatformPermissionService platformPermissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (currentUser.UserId is not { } userId) return;
        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        var granted = requirement.Kind switch
        {
            PermissionKind.Organization => currentUser.Access == Access.Business
                && tenantContext.TenantKind == TenantKind.Business
                && tenantContext.TenantId is not null
                && await permissions.HasPermissionAsync(userId, requirement.Permission, cancellationToken),
            PermissionKind.Company => currentUser.Access == Access.Business
                && tenantContext.TenantKind == TenantKind.Business
                && tenantContext.TenantId is not null
                && context.Resource is HttpContext companyContext
                && TryCompanyId(companyContext, out var companyId)
                && await permissions.HasCompanyPermissionAsync(userId, companyId, requirement.Permission, cancellationToken),
            PermissionKind.Platform => currentUser.Access == Access.Platform
                && await platformPermissions.HasPermissionAsync(userId, requirement.Permission, cancellationToken),
            _ => false,
        };

        if (granted) context.Succeed(requirement);
    }

    private static bool TryCompanyId(HttpContext context, out Guid companyId)
    {
        var raw = context.Request.RouteValues["companyId"]?.ToString();
        return Guid.TryParse(raw, CultureInfo.InvariantCulture, out companyId) && companyId != Guid.Empty;
    }
}
