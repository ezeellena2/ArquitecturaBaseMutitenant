using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;

public static class AssistantTexts
{
    internal static ResourceManager ResourceManager { get; } = new("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources.AssistantTexts", typeof(AssistantTexts).Assembly);
    public static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Assistant resource {key} is missing.");
    public static string GetForCulture(string key, CultureInfo culture) => ResourceManager.GetString(key, culture)
        ?? throw new InvalidOperationException($"Assistant resource {key} is missing.");
}

internal sealed class AssistantErrorTextSource : IErrorTextSource
{
    public string? Find(string code, CultureInfo culture) => AssistantTexts.ResourceManager.GetString(code, culture);
}
