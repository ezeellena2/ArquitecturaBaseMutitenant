using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

namespace ArquitecturaBaseMultitenant.Application.Common.Formatting;

/// <summary>Lee las culturas habilitadas del catálogo y exige una sola predeterminada para validar preferencias sin una lista fija en código.</summary>
public sealed class SupportedCultures
{
    private SupportedCultures(string defaultCulture, IReadOnlyList<string> codes)
    {
        DefaultCulture = defaultCulture;
        Codes = codes;
    }

    public string DefaultCulture { get; }

    public IReadOnlyList<string> Codes { get; }

    public static async Task<SupportedCultures> LoadAsync(ICultureCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var enabled = (await catalog.ListAsync(cancellationToken))
            .Where(culture => culture.IsEnabled)
            .ToArray();
        var defaults = enabled.Where(culture => culture.IsDefault).ToArray();
        if (defaults.Length != 1)
        {
            throw new InvalidOperationException("Exactly one enabled culture must be the default.");
        }

        var codes = enabled.Select(culture => culture.Code).ToArray();
        return new SupportedCultures(defaults[0].Code, Array.AsReadOnly(codes));
    }
}
