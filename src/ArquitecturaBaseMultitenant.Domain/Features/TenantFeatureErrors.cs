using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Features;

/// <summary>Errores de catálogo y habilitación global de módulos opcionales.</summary>
public static class TenantFeatureErrors
{
    public const string InvalidKeyCode = "Features.Module.InvalidKey";
    public const string InvalidSourceCode = "Features.Module.InvalidSource";
    public const string ExpiryRequiredCode = "Features.Module.ExpiryRequired";
    public const string InvalidExpiryCode = "Features.Module.InvalidExpiry";
    public const string NotFoundCode = "Features.Module.NotFound";
    public const string InvalidScopeCode = "Features.Module.InvalidScope";

    public static readonly Error InvalidKey = Error.Validation(InvalidKeyCode, "The module key is invalid.");
    public static readonly Error InvalidSource = Error.Validation(InvalidSourceCode, "The module source is invalid.");
    public static readonly Error ExpiryRequired = Error.Validation(ExpiryRequiredCode, "A trial requires an expiry instant.");
    public static readonly Error InvalidExpiry = Error.Validation(InvalidExpiryCode, "The expiry day cannot be represented as a UTC range.");
    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The module was not found.");
    public static readonly Error InvalidScope = Error.Validation(InvalidScopeCode, "The module does not support this access.");
}
