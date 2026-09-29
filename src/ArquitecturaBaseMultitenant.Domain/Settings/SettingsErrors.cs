using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Settings;

public static class SettingsErrors
{
    public const string NotFoundCode = "Settings.System.NotFound";

    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The platform settings were not found.");
}
