using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class FormatCasesContractTests
{
    private static readonly string Root = FindSolutionRoot();

    private static readonly string[] FormatTypes =
    [
        "date", "dateTime", "time", "dateLong", "relative", "dateRange",
        "integer", "decimal", "quantity", "percent", "money", "compact",
        "fileSize", "duration", "phone", "timeZone", "culture", "taxId",
        "email", "enum", "boolean", "empty", "text"
    ];

    [Fact]
    public void Cases_have_a_fixed_clock_required_fields_and_unique_ids()
    {
        using var document = ReadJson("docs", "contracts", "format-cases.json");
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("2026-09-27T15:00:00Z", root.GetProperty("now").GetString());

        var cases = root.GetProperty("cases");
        Assert.Equal(JsonValueKind.Array, cases.ValueKind);
        Assert.NotEmpty(cases.EnumerateArray());

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in cases.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Object, item.ValueKind);
            var id = RequiredString(item, "id");
            Assert.True(ids.Add(id), $"Duplicate format case id: {id}");
            Assert.Contains(RequiredString(item, "type"), FormatTypes);
            RequiredString(item, "culture");
            RequiredString(item, "timeZone");
            Assert.True(item.TryGetProperty("input", out _), $"Missing input: {id}");
            RequiredString(item, "expected");
        }
    }

    [Fact]
    public void Every_format_type_has_a_case_for_each_enabled_culture()
    {
        using var document = ReadJson("docs", "contracts", "format-cases.json");
        using var catalog = ReadCatalog("cultures.json");
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        var enabledCultures = catalog.RootElement.GetProperty("Cultures").EnumerateArray()
            .Where(culture => culture.GetProperty("IsEnabled").GetBoolean())
            .Select(culture => culture.GetProperty("Code").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(enabledCultures);
        foreach (var item in cases)
        {
            Assert.Contains(RequiredString(item, "culture"), enabledCultures);
        }

        foreach (var type in FormatTypes)
        {
            foreach (var culture in enabledCultures)
            {
                Assert.Contains(cases, item => item.GetProperty("type").GetString() == type
                    && item.GetProperty("culture").GetString() == culture);
            }
        }
    }

    [Fact]
    public void Money_cases_fix_catalog_symbols_and_zero_two_three_minor_units()
    {
        using var document = ReadJson("docs", "contracts", "format-cases.json");
        using var catalog = ReadCatalog("currencies.json");
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        var currencies = catalog.RootElement.GetProperty("Currencies").EnumerateArray()
            .ToDictionary(currency => currency.GetProperty("Code").GetString()!, StringComparer.Ordinal);

        AssertMoney(cases, currencies, "ARS", 2, "es-AR", 1234.5m, "$ 1.234,50");
        AssertMoney(cases, currencies, "ARS", 2, "en-US", 1234.5m, "ARS 1,234.50");
        AssertMoney(cases, currencies, "USD", 2, "es-AR", 1234.5m, "US$ 1.234,50");
        AssertMoney(cases, currencies, "USD", 2, "en-US", 1234.5m, "$1,234.50");
        AssertMoney(cases, currencies, "CLP", 0, "es-AR", 1234m, "CLP 1.234");
        AssertMoney(cases, currencies, "CLP", 0, "en-US", 1234m, "CLP 1,234");
        AssertMoney(cases, currencies, "BHD", 3, "es-AR", 1234.567m, "BHD 1.234,567");
        AssertMoney(cases, currencies, "BHD", 3, "en-US", 1234.567m, "BHD 1,234.567");

        AssertCase(cases, "money-ars-negative-es-AR", "money", "es-AR", "-$ 1.234,50");
        AssertCase(cases, "money-ars-negative-en-US", "money", "en-US", "-ARS 1,234.50");
        Assert.Contains(cases, item => item.GetProperty("type").GetString() == "money"
            && item.GetProperty("input").GetProperty("amount").GetDecimal() < 0);
    }

    [Fact]
    public void Phone_cases_fix_national_and_international_text_in_both_cultures()
    {
        using var document = ReadJson("docs", "contracts", "format-cases.json");
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();

        AssertPhone(cases, "phone-ar-es-AR", "es-AR", "+5491123456789", "011 15-2345-6789");
        AssertPhone(cases, "phone-ar-en-US", "en-US", "+5491123456789", "+54 9 11 2345 6789");
        AssertPhone(cases, "phone-us-es-AR", "es-AR", "+12125550123", "+1 212 555 0123");
        AssertPhone(cases, "phone-us-en-US", "en-US", "+12125550123", "(212) 555-0123");
        AssertPhone(cases, "phone-uy-es-AR", "es-AR", "+59894123456", "+598 94 123 456");
        AssertPhone(cases, "phone-uy-en-US", "en-US", "+59894123456", "+598 94 123 456");
    }

    [Fact]
    public void Civil_values_relative_boundary_and_reference_data_examples_are_explicit()
    {
        using var document = ReadJson("docs", "contracts", "format-cases.json");
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();

        foreach (var (culture, date, time, oldDate, cultureName, booleanTrue) in new[]
        {
            ("es-AR", "27/09/2026", "14:35", "20/09/2026", "Inglés (Estados Unidos)", "Sí"),
            ("en-US", "09/27/2026", "2:35 PM", "09/20/2026", "English (United States)", "Yes")
        })
        {
            AssertInput(cases, $"date-civil-{culture}", "date", culture, "2026-09-27", date);
            AssertInput(cases, $"time-civil-{culture}", "time", culture, "14:35:00", time);
            AssertInput(cases, $"relative-old-{culture}", "relative", culture,
                "2026-09-20T15:00:00Z", oldDate);
            AssertInput(cases, $"relative-recent-{culture}", "relative", culture,
                "2026-09-27T14:55:00Z", culture == "es-AR" ? "hace 5 minutos" : "5 minutes ago");
            AssertInput(cases, $"culture-en-US-{culture}", "culture", culture, "en-US", cultureName);
            AssertCase(cases, $"boolean-true-{culture}", "boolean", culture, booleanTrue);
            AssertCase(cases, $"boolean-false-{culture}", "boolean", culture, "No");
            AssertCase(cases, $"tax-id-cuit-{culture}", "taxId", culture, "20-12345678-6");
            AssertCase(cases, $"tax-id-dni-{culture}", "taxId", culture, "12345678");
            AssertCase(cases, $"time-zone-buenos-aires-{culture}", "timeZone", culture,
                "Buenos Aires (GMT−3)");
            AssertCase(cases, $"file-size-{culture}", "fileSize", culture,
                culture == "es-AR" ? "1,5 MB" : "1.5 MB");
        }
    }

    private static void AssertMoney(JsonElement[] cases, Dictionary<string, JsonElement> currencies,
        string code, int minorUnits, string culture, decimal amount, string expected)
    {
        var item = AssertCase(cases, $"money-{code.ToLowerInvariant()}-{culture}", "money", culture, expected);
        var input = item.GetProperty("input");
        Assert.Equal(code, input.GetProperty("currency").GetString());
        Assert.Equal(amount, input.GetProperty("amount").GetDecimal());
        var currency = currencies[code];
        Assert.Equal(minorUnits, currency.GetProperty("MinorUnits").GetInt32());
        var displaySymbol = currency.GetProperty("Translations").EnumerateArray()
            .Single(translation => translation.GetProperty("Culture").GetString() == culture)
            .GetProperty("DisplaySymbol").GetString();
        Assert.Contains(displaySymbol!, expected, StringComparison.Ordinal);
    }

    private static void AssertPhone(JsonElement[] cases, string id, string culture, string input, string expected) =>
        AssertInput(cases, id, "phone", culture, input, expected);

    private static void AssertInput(JsonElement[] cases, string id, string type,
        string culture, string input, string expected)
    {
        var item = AssertCase(cases, id, type, culture, expected);
        Assert.Equal(input, item.GetProperty("input").GetString());
    }

    private static JsonElement AssertCase(JsonElement[] cases, string id, string type,
        string culture, string expected)
    {
        var item = Assert.Single(cases, candidate => candidate.GetProperty("id").GetString() == id);
        Assert.Equal(type, item.GetProperty("type").GetString());
        Assert.Equal(culture, item.GetProperty("culture").GetString());
        Assert.Equal(expected, item.GetProperty("expected").GetString());
        return item;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        var text = Assert.IsType<string>(value.GetString(), exactMatch: false);
        Assert.False(string.IsNullOrWhiteSpace(text), $"{name} cannot be empty.");
        return text;
    }

    private static JsonDocument ReadCatalog(string name) =>
        ReadJson("src", "ArquitecturaBaseMultitenant.Infrastructure", "Persistence", "Seed", "ReferenceData", name);

    private static JsonDocument ReadJson(params string[] parts)
    {
        var path = Path.Combine([Root, .. parts]);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Solution root was not found.");
    }
}
