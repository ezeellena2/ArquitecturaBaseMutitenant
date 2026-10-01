using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// Define el error que se devuelve cuando no se encuentra la configuración global de la plataforma. Los
/// servicios lo usan para informar la ausencia de esa configuración.
/// </summary>
public static class SettingsErrors
{
    public const string NotFoundCode = "Settings.System.NotFound";

    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The platform settings were not found.");
}
