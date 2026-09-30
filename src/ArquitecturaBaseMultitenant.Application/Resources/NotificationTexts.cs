using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;

namespace ArquitecturaBaseMultitenant.Application.Resources;

/// <summary>Obtiene y compone mensajes de notificación según el perfil cultural, con respaldo en los recursos base.</summary>
public static class NotificationTexts
{
    internal static ResourceManager ResourceManager { get; } =
        new("ArquitecturaBaseMultitenant.Application.Resources.Notifications", typeof(NotificationTexts).Assembly);

    public static string Get(string key, CultureProfile profile)
    {
        foreach (var language in profile.ResourceLanguages)
        {
            var resources = ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(language),
                createIfNotExists: true, tryParents: false);
            if (resources?.GetString(key) is { } translated)
            {
                return translated;
            }
        }

        return ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Notification resource {key} is missing for {profile.Entry.Code}.");
    }

    public static string Format(string key, CultureProfile profile, params object[] args) =>
        DisplayFormatter.FormatResource(Get(key, profile), profile, args);
}
