using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public static class TenantSlugErrors
{
    public const string InvalidCode = "Tenancy.TenantSlug.Invalid";
    public const string ReservedCode = "Tenancy.TenantSlug.Reserved";
    public const string TakenCode = "Tenancy.TenantSlug.Taken";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The organization page address is invalid.");
    public static readonly Error Reserved = Error.Conflict(ReservedCode, "The organization page address is reserved.");
    public static readonly Error Taken = Error.Conflict(TakenCode, "The organization page address is already in use.");
}
