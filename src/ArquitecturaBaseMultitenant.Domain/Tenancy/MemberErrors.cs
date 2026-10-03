using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>
/// Define los rechazos de una membresía inactiva o de una transición de estado inválida. Permite comunicar
/// estas reglas mediante Result con códigos estables.
/// </summary>
public static class MemberErrors
{
    public const string InactiveCode = "Tenancy.Member.Inactive";
    public const string NotFoundCode = "Tenancy.Member.NotFound";
    public const string InvalidTransitionCode = "Tenancy.Member.InvalidTransition";
    public const string IdentityRequiredCode = "Tenancy.Member.IdentityRequired";
    public const string LastTenantAdminCode = "Tenancy.Member.LastTenantAdmin";
    public const string CannotDisableSelfCode = "Tenancy.Member.CannotDisableSelf";

    public static readonly Error Inactive = Error.Forbidden(InactiveCode, "The membership is inactive.");
    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The organization member was not found.");
    public static readonly Error InvalidTransition = Error.Conflict(InvalidTransitionCode, "The membership cannot change to that state.");
    public static readonly Error IdentityRequired = Error.Conflict(IdentityRequiredCode, "An invited member needs an identity before activation.");
    public static readonly Error LastTenantAdmin = Error.Conflict(LastTenantAdminCode, "The organization must keep at least one active owner.");
    public static readonly Error CannotDisableSelf = Error.Forbidden(CannotDisableSelfCode, "A member cannot disable their own organization access.");
}
