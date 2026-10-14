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
    /// <summary>Texto en la cultura de la petición: mensajes de validación y errores.</summary>
    public static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"WhatsApp resource {key} is missing.");
    /// <summary>Texto en una cultura del catálogo (es-AR), con la caída de .NET al idioma y al texto neutro.</summary>
    public static string Get(string key, string culture) => ResourceManager.GetString(key, CultureInfo.GetCultureInfo(culture))
        ?? throw new InvalidOperationException($"WhatsApp resource {key} is missing.");
    /// <summary>Texto en la cultura de la petición, o null si no hay uno para esa clave (por ejemplo, un código de Meta nuevo).</summary>
    public static string? Find(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
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
