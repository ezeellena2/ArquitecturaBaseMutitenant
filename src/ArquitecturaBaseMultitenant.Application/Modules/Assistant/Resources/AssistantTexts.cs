using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;

public static class AssistantTexts
{
    internal static ResourceManager ResourceManager { get; } = new("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources.AssistantTexts", typeof(AssistantTexts).Assembly);
    public static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Assistant resource {key} is missing.");

    /// <summary>Texto en la cultura de un aviso, con la caída de idiomas del catálogo (correos en segundo plano).</summary>
    public static string Get(string key, CultureProfile profile)
    {
        foreach (var language in profile.ResourceLanguages)
            if (ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(language), true, false)?.GetString(key) is { } value) return value;
        return ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"Assistant resource {key} is missing.");
    }

    /// <summary>Completa un texto con valores ya formateados por DisplayFormatter.</summary>
    public static string Format(string key, CultureProfile profile, params object[] values) =>
        DisplayFormatter.FormatResource(Get(key, profile), profile, values);
}

internal sealed class AssistantErrorTextSource : IErrorTextSource
{
    public string? Find(string code, CultureInfo culture) => AssistantTexts.ResourceManager.GetString(code, culture);
}
