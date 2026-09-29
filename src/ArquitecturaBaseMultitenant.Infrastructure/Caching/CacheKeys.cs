using System.Globalization;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>Claves de HybridCache separadas por alcance; ninguna lectura arma el prefijo por su cuenta.</summary>
internal static class CacheKeys
{
    public static string Tenant(Guid tenantId, string resource) => Scoped("t", tenantId, resource);

    public static string PublicSite(Guid businessTenantId, string resource) => Scoped("s", businessTenantId, resource);

    public static string User(Guid userId, string resource) => Scoped("u", userId, resource);

    public static string Platform(string resource)
    {
        ValidateResource(resource);
        return "p:" + resource;
    }

    private static string Scoped(string prefix, Guid scopeId, string resource)
    {
        if (scopeId == Guid.Empty)
        {
            throw new ArgumentException("A cache scope requires a non-empty ID.", nameof(scopeId));
        }

        ValidateResource(resource);
        return prefix + ":" + scopeId.ToString("N", CultureInfo.InvariantCulture) + ":" + resource;
    }

    private static void ValidateResource(string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new ArgumentException("A cache key requires a resource.", nameof(resource));
        }
    }
}
