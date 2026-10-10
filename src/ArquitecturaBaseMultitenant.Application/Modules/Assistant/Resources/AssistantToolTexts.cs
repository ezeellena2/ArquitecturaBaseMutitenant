using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;

internal static class AssistantToolTexts
{
    private static readonly ResourceManager Resources = new("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources.Assistant", typeof(AssistantToolTexts).Assembly);
    internal static string Get(string key, CultureProfile profile)
    {
        foreach (var language in profile.ResourceLanguages)
            if (Resources.GetResourceSet(CultureInfo.GetCultureInfo(language), true, false)?.GetString(key) is { } text) return text;
        throw new InvalidOperationException($"Assistant resource {key} is missing.");
    }
}
