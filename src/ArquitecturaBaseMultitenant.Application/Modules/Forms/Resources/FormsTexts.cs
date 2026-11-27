using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;

public static class FormsTexts
{
    internal static ResourceManager ResourceManager { get; } = new("ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources.FormsTexts", typeof(FormsTexts).Assembly);
    public static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Forms resource {key} is missing.");

    /// <summary>Texto en una cultura del catálogo, con su caída: los encabezados de la planilla en cada cultura habilitada.</summary>
    public static string Get(string key, CultureProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        foreach (var language in profile.ResourceLanguages)
            if (ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(language), true, false)?.GetString(key) is { } value) return value;
        return ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Forms resource {key} is missing.");
    }
}

internal sealed class FormsErrorTextSource : IErrorTextSource
{
    public string? Find(string code, CultureInfo culture) => FormsTexts.ResourceManager.GetString(code, culture);
}
