using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authorization;

public static class RoleAssignmentErrors
{
    public const string CompanyRequiredCode = "Roles.RoleAssignment.CompanyRequired";
    public const string CompanyNotAllowedCode = "Roles.RoleAssignment.CompanyNotAllowed";
    public const string CompanyMismatchCode = "Roles.RoleAssignment.CompanyMismatch";

    public static readonly Error CompanyRequired = Error.Validation(CompanyRequiredCode, "A company is required for this role assignment.");
    public static readonly Error CompanyNotAllowed = Error.Validation(CompanyNotAllowedCode, "A company is not allowed for this role assignment.");
    public static readonly Error CompanyMismatch = Error.Validation(CompanyMismatchCode, "The company does not match the role scope.");
}
