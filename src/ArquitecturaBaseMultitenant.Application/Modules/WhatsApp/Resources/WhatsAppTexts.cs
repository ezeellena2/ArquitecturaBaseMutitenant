using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources;
public static class WhatsAppTexts
{
    public static string Format(string key, CultureProfile profile, params object[] values) => DisplayFormatter.FormatResource(Get(key, profile), profile, values);
    internal static ResourceManager ResourceManager { get; } = new(
        "ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Resources.WhatsAppTexts", typeof(WhatsAppTexts).Assembly);
    public static string Get(string key, CultureProfile profile)
    {
        foreach (var language in profile.ResourceLanguages)
            if (ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(language), true, false)?.GetString(key) is { } value) return value;
        return ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException($"WhatsApp resource {key} is missing.");
    }
}
internal sealed class WhatsAppErrorTextSource : IErrorTextSource
{
    public string? Find(string code, CultureInfo culture) => WhatsAppTexts.ResourceManager.GetString(code, culture);
}
