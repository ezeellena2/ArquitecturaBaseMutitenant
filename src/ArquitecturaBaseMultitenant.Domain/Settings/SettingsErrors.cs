using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// Define el error que se devuelve cuando no se encuentra la configuración global de la plataforma. Los
/// servicios lo usan para informar la ausencia de esa configuración.
/// </summary>
public static class SettingsErrors
{
    public const string NotFoundCode = "Settings.System.NotFound";
    public const string InvalidPlatformConfigurationCode = "Settings.System.InvalidConfiguration";
    public const string TenantNotFoundCode = "Settings.Tenant.NotFound";
    public const string TenantConfigurationRequiredCode = "Settings.Tenant.ConfigurationRequired";

    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The platform settings were not found.");
    public static readonly Error InvalidPlatformConfiguration = Error.Validation(InvalidPlatformConfigurationCode,
        "Valid signup modes and a positive organization limit are required.");
    public static readonly Error TenantNotFound = Error.NotFound(TenantNotFoundCode,
        "The organization settings were not found.");
    public static readonly Error TenantConfigurationRequired = Error.Validation(TenantConfigurationRequiredCode,
        "The organization culture, time zone, and currency are required.");
}
