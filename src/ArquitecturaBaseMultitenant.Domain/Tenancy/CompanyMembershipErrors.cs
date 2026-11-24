using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

public static class CompanyMembershipErrors
{
    public const string NotFoundCode = "Companies.Membership.NotFound";
    public const string AlreadyExistsCode = "Companies.Membership.AlreadyExists";
    public const string MemberInactiveCode = "Companies.Membership.MemberInactive";
    public const string LastCompanyAdminCode = "Companies.Membership.LastCompanyAdmin";
    public const string RolesNotHeldCode = "Companies.Membership.RolesNotHeld";
    public const string HasMorePermissionsCode = "Companies.Membership.HasMorePermissions";

    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The company membership was not found.");
    public static readonly Error AlreadyExists = Error.Conflict(AlreadyExistsCode, "The member already belongs to this company.");
    public static readonly Error MemberInactive = Error.Conflict(MemberInactiveCode, "The organization member is not active.");
    public static readonly Error LastCompanyAdmin = Error.Conflict(LastCompanyAdminCode,
        "The only active company administrator cannot be removed.");

    /// <summary>ADR 0061: no se da ni se quita un rol de la empresa con permisos que quien edita no tiene en ella.</summary>
    public static readonly Error RolesNotHeld = Error.Forbidden(RolesNotHeldCode, "You cannot give or take away a company role with permissions you do not have.");

    /// <summary>ADR 0061: no se cambia ni se desactiva a quien tiene en la empresa permisos que quien edita no tiene.</summary>
    public static readonly Error HasMorePermissions = Error.Forbidden(HasMorePermissionsCode, "The company member has permissions you do not have.");
}
