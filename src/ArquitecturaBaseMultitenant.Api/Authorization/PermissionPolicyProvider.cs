using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.Authorization;

/// <summary>Resuelve las tres clases de políticas de permiso y delega las demás al proveedor de ASP.NET Core.</summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string OrganizationPrefix = "permission:";
    public const string CompanyPrefix = "company-permission:";
    public const string PlatformPrefix = "platform-permission:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var (prefix, kind) = policyName.StartsWith(OrganizationPrefix, StringComparison.Ordinal)
            ? (OrganizationPrefix, PermissionKind.Organization)
            : policyName.StartsWith(CompanyPrefix, StringComparison.Ordinal)
                ? (CompanyPrefix, PermissionKind.Company)
                : policyName.StartsWith(PlatformPrefix, StringComparison.Ordinal)
                    ? (PlatformPrefix, PermissionKind.Platform)
                    : (string.Empty, (PermissionKind?)null);

        if (kind is null) return await base.GetPolicyAsync(policyName);

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(kind.Value, policyName[prefix.Length..]))
            .Build();
    }
}
