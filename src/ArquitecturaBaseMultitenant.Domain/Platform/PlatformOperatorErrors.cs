using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

public static class PlatformOperatorErrors
{
    public const string PermissionsRequiredCode = "Platform.Operator.PermissionsRequired";
    public const string PermissionNotFoundCode = "Platform.Operator.PermissionNotFound";
    public const string PermissionRepeatedCode = "Platform.Operator.PermissionRepeated";
    public const string LastManagerCode = "Platform.Operator.LastManager";
    public const string AccountExistsCode = "Platform.Operator.AccountExists";
    public const string NotOperatorCode = "Platform.Operator.NotOperator";

    public static readonly Error PermissionsRequired = Error.Validation(PermissionsRequiredCode,
        "An operator needs at least one platform permission.");
    public static readonly Error PermissionNotFound = Error.Validation(PermissionNotFoundCode,
        "A permission is not in the platform catalog.");
    public static readonly Error PermissionRepeated = Error.Validation(PermissionRepeatedCode,
        "A platform permission is repeated.");
    public static readonly Error LastManager = Error.Conflict(LastManagerCode,
        "At least one active operator must be able to manage operators.");
    /// <summary>
    /// El correo ya es de una cuenta: un operador no puede tener perfiles de persona. Es la excepción de la ADR 0041,
    /// porque invitar operadores exige <c>platform.operators.manage</c>.
    /// </summary>
    public static readonly Error AccountExists = Error.Conflict(AccountExistsCode,
        "The email already belongs to an account; an operator cannot have personal profiles.");
    public static readonly Error NotOperator = Error.NotFound(NotOperatorCode, "The account is not a platform operator.");
}
