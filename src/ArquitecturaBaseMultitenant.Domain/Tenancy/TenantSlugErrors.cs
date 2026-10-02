using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public static class TenantSlugErrors
{
    public const string InvalidCode = "Tenancy.TenantSlug.Invalid";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The organization page address is invalid.");
}
