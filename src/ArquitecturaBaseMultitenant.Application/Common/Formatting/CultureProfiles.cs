using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;

namespace ArquitecturaBaseMultitenant.Application.Common.Formatting;

/// <summary>Adapts culture rows to .NET format providers without a code-based culture catalog.</summary>
public sealed class CultureProfiles(ICultureCatalog cultures)
{
    public async Task<CultureProfile> LoadAsync(string? requestedCode, CancellationToken cancellationToken)
    {
        var rows = await cultures.ListAsync(cancellationToken);
        var byCode = rows.ToDictionary(row => row.Code, StringComparer.OrdinalIgnoreCase);
        var defaultRow = rows.SingleOrDefault(row => row.IsDefault && row.IsEnabled)
            ?? throw new InvalidOperationException("The reference data has no enabled default culture.");
        var row = requestedCode is not null && byCode.TryGetValue(requestedCode, out var requested)
            && requested.IsEnabled ? requested : defaultRow;

        var cultureInfo = CultureInfo.GetCultureInfo(row.Code);
        var numbers = (NumberFormatInfo)cultureInfo.NumberFormat.Clone();
        numbers.NumberDecimalSeparator = row.DecimalSeparator;
        numbers.NumberGroupSeparator = row.GroupSeparator;

        var translationOrder = new List<string>();
        var resourceLanguages = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = row;
        while (seen.Add(current.Code))
        {
            translationOrder.Add(current.Code);
            if (!resourceLanguages.Contains(current.LanguageCode, StringComparer.OrdinalIgnoreCase))
            {
                resourceLanguages.Add(current.LanguageCode);
            }
            if (current.FallbackCulture is null ||
                !byCode.TryGetValue(current.FallbackCulture, out var fallback) || !fallback.IsEnabled)
            {
                break;
            }

            current = fallback;
        }

        if (seen.Add(defaultRow.Code))
        {
            translationOrder.Add(defaultRow.Code);
        }

        if (!resourceLanguages.Contains(defaultRow.LanguageCode, StringComparer.OrdinalIgnoreCase))
        {
            resourceLanguages.Add(defaultRow.LanguageCode);
        }

        return new CultureProfile(row, cultureInfo, numbers, translationOrder, resourceLanguages);
    }
}

public sealed record CultureProfile(
    CultureCatalogEntry Entry,
    CultureInfo Culture,
    NumberFormatInfo Numbers,
    IReadOnlyList<string> TranslationOrder,
    IReadOnlyList<string> ResourceLanguages)
{
    public TTranslation Translate<TTranslation>(IReadOnlyList<TTranslation> translations, Func<TTranslation, string> cultureCode)
    {
        foreach (var candidate in TranslationOrder)
        {
            foreach (var translation in translations)
            {
                if (string.Equals(cultureCode(translation), candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return translation;
                }
            }
        }

        throw new InvalidOperationException("A reference entry has no translation for its culture chain.");
    }
}
