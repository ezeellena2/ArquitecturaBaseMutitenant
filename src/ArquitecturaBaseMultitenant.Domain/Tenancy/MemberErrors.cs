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
    public const string RolesNotHeldCode = "Tenancy.Member.RolesNotHeld";
    public const string HasMorePermissionsCode = "Tenancy.Member.HasMorePermissions";
    /// <summary>El nombre que escribió el asistente es de más de una persona: solo vuelve al asistente como dato, nunca como HTTP.</summary>
    public const string AmbiguousCode = "Tenancy.Member.Ambiguous";

    public static readonly Error Inactive = Error.Forbidden(InactiveCode, "The membership is inactive.");
    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The organization member was not found.");
    public static readonly Error InvalidTransition = Error.Conflict(InvalidTransitionCode, "The membership cannot change to that state.");
    public static readonly Error IdentityRequired = Error.Conflict(IdentityRequiredCode, "An invited member needs an identity before activation.");
    public static readonly Error LastTenantAdmin = Error.Conflict(LastTenantAdminCode, "The organization must keep at least one active owner.");
    public static readonly Error CannotDisableSelf = Error.Forbidden(CannotDisableSelfCode, "A member cannot disable their own organization access.");
    public static readonly Error Ambiguous = Error.Validation(AmbiguousCode, "More than one person matches.");

    /// <summary>ADR 0061: no se da ni se quita un rol con permisos que quien edita no tiene.</summary>
    public static readonly Error RolesNotHeld = Error.Forbidden(RolesNotHeldCode, "You cannot give or take away a role with permissions you do not have.");

    /// <summary>ADR 0061: no se cambia, desactiva ni quita a quien tiene permisos que quien edita no tiene.</summary>
    public static readonly Error HasMorePermissions = Error.Forbidden(HasMorePermissionsCode, "The member has permissions you do not have.");
}
