using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authorization;

public static class RoleErrors
{
    public const string NameRequiredCode = "Roles.Role.NameRequired";
    public const string NameTooLongCode = "Roles.Role.NameTooLong";
    public const string DescriptionTooLongCode = "Roles.Role.DescriptionTooLong";
    public const string InvalidScopeCode = "Roles.Role.InvalidScope";
    public const string CompanyRequiredCode = "Roles.Role.CompanyRequired";
    public const string CompanyNotAllowedCode = "Roles.Role.CompanyNotAllowed";
    public const string PermissionNotFoundCode = "Roles.Role.PermissionNotFound";
    public const string PermissionScopeInvalidCode = "Roles.Role.PermissionScopeInvalid";
    public const string SystemRoleCannotChangeCode = "Roles.Role.SystemRoleCannotChange";
    public const string SystemRoleNameReservedCode = "Roles.Role.SystemRoleNameReserved";

    public static readonly Error NameRequired = Error.Validation(NameRequiredCode, "A role name is required.");
    public static readonly Error NameTooLong = Error.Validation(NameTooLongCode, "The role name is too long.");
    public static readonly Error DescriptionTooLong = Error.Validation(DescriptionTooLongCode, "The role description is too long.");
    public static readonly Error InvalidScope = Error.Validation(InvalidScopeCode, "The role scope is invalid.");
    public static readonly Error CompanyRequired = Error.Validation(CompanyRequiredCode, "A company is required for this role scope.");
    public static readonly Error CompanyNotAllowed = Error.Validation(CompanyNotAllowedCode, "A company is not allowed for this role scope.");
    public static readonly Error PermissionNotFound = Error.Validation(PermissionNotFoundCode, "A permission is not in the catalog.");
    public static readonly Error PermissionScopeInvalid = Error.Validation(PermissionScopeInvalidCode, "A permission is not available for this role scope.");
    public static readonly Error SystemRoleCannotChange = Error.Forbidden(SystemRoleCannotChangeCode, "System roles cannot be changed.");
    public static readonly Error SystemRoleNameReserved = Error.Conflict(SystemRoleNameReservedCode, "The name is reserved for a system role.");
}
