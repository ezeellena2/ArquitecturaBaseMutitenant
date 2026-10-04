using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Platform;

public static class PlatformOperatorErrors
{
    public const string PermissionsRequiredCode = "Platform.Operator.PermissionsRequired";
    public const string PermissionNotFoundCode = "Platform.Operator.PermissionNotFound";
    public const string PermissionRepeatedCode = "Platform.Operator.PermissionRepeated";
    public const string LastManagerCode = "Platform.Operator.LastManager";

    public static readonly Error PermissionsRequired = Error.Validation(PermissionsRequiredCode,
        "An operator needs at least one platform permission.");
    public static readonly Error PermissionNotFound = Error.Validation(PermissionNotFoundCode,
        "A permission is not in the platform catalog.");
    public static readonly Error PermissionRepeated = Error.Validation(PermissionRepeatedCode,
        "A platform permission is repeated.");
    public static readonly Error LastManager = Error.Conflict(LastManagerCode,
        "At least one active operator must be able to manage operators.");
}
