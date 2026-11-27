using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;

public static class FormsTexts
{
    internal static ResourceManager ResourceManager { get; } = new("ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources.FormsTexts", typeof(FormsTexts).Assembly);
    public static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Forms resource {key} is missing.");
}

internal sealed class FormsErrorTextSource : IErrorTextSource
{
    public string? Find(string code, CultureInfo culture) => FormsTexts.ResourceManager.GetString(code, culture);
}
