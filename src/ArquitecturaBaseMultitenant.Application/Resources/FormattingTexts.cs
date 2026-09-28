using System.Globalization;
using System.Resources;

namespace ArquitecturaBaseMultitenant.Application.Resources;

public static class FormattingTexts
{
    internal static ResourceManager ResourceManager { get; } =
        new("ArquitecturaBaseMultitenant.Application.Resources.Formatting", typeof(FormattingTexts).Assembly);

    public static string Get(string key, CultureInfo culture) =>
        ResourceManager.GetString(key, culture)
        ?? throw new InvalidOperationException($"Formatting resource {key} is missing for {culture.Name}.");
}
