using System.Globalization;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>Claves estables de locks tenant y del seed global entre réplicas de la Api.</summary>
internal static class AdvisoryLockKeys
{
    public const string ReferenceDataSeed = "p:ref:seed";
    public const string Seed = "seed:database";

    public static string Account(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user ID is required.", nameof(userId));
        return "u:" + userId.ToString("N", CultureInfo.InvariantCulture) + ":lock:account";
    }

    public static string ExternalGoogleSubject(string subject) => "google:subject:" + subject;

    public static string ExternalGoogleEmail(string email) => "google:email:" + email;

    public static string PersonalSpace(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A personal space lock requires a user ID.", nameof(userId));
        }

        return "u:" + userId.ToString("N", CultureInfo.InvariantCulture) + ":lock:personal-space";
    }

    /// <summary>Un lock global por destino normalizado, compartido entre propósitos de ingreso.</summary>
    public static string LoginCode(LoginCodeDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return "login-code:" + destination.Value;
    }

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
