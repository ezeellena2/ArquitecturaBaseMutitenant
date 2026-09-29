using System.Globalization;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>Claves estables de locks tenant y del seed global entre réplicas de la Api.</summary>
internal static class AdvisoryLockKeys
{
    public const string ReferenceDataSeed = "p:ref:seed";

    public static string For(Guid tenantId, string resource, Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A lock requires a non-empty resource ID.", nameof(id));
        }

        return For(tenantId, resource, id.ToString("N", CultureInfo.InvariantCulture));
    }

    public static string For(Guid tenantId, string resource, string id)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A lock requires a non-empty tenant ID.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new ArgumentException("A lock requires a resource name.", nameof(resource));
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A lock requires a resource ID.", nameof(id));
        }

        return "t:" + tenantId.ToString("N", CultureInfo.InvariantCulture) + ":lock:" + resource + ":" + id;
    }
}
