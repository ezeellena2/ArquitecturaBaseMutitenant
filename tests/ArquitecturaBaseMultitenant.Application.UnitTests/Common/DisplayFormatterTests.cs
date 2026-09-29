using System.Text.Json;
using System.Globalization;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Phones;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Common;

public sealed class DisplayFormatterTests
{
    [Fact]
    public async Task Typed_methods_share_one_culture_profile_and_match_display_contract()
    {
        var catalog = new JsonReferenceDataCatalog();
        var countedCultures = new CountingCultureCatalog(catalog);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T15:00:00Z", CultureInfo.InvariantCulture));
        var formatter = new DisplayFormatter(catalog, catalog, catalog, countedCultures, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(clock), clock);
        var context = await formatter.CreateAsync("es-AR", "America/Argentina/Buenos_Aires",
            TestContext.Current.CancellationToken);
        var money = new Money(1234.5m, CurrencyCode.Create("ARS").Value);
        var phone = PhoneNumber.Create("+5491123456789").Value;

        Assert.Equal("$ 1.234,50", await formatter.FormatMoneyAsync(
            money, context, TestContext.Current.CancellationToken));
        Assert.Equal("27/09/2026 14:35", formatter.FormatInstant(
            new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Utc), context));
        Assert.Throws<ArgumentException>(() => formatter.FormatInstant(
            new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Unspecified), context));
        Assert.Equal("011 15-2345-6789", await formatter.FormatPhoneAsync(
            phone, context, TestContext.Current.CancellationToken));
        Assert.Equal("1.234,50", formatter.FormatDecimal(1234.5m, 2, context));
        Assert.Equal("12,5 %", formatter.FormatPercent(0.125m, context));
        Assert.Equal(1, countedCultures.ListCalls);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Matches_shared_format_contract(
        string id, string type, string culture, string timeZone, string inputJson, string expected)
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T15:00:00Z", CultureInfo.InvariantCulture));
        var formatter = new DisplayFormatter(
            catalog, catalog, catalog, catalog, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(clock), clock);
        using var input = JsonDocument.Parse(inputJson);

        var actual = await formatter.FormatAsync(type, input.RootElement, culture, timeZone, TestContext.Current.CancellationToken);

        Assert.True(string.Equals(expected, actual, StringComparison.Ordinal),
            $"{id}: expected '{expected}', got '{actual}'.");
    }

    public static IEnumerable<object[]> Cases()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Solution root not found."),
            "docs", "contracts", "format-cases.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (item.TryGetProperty("error", out _))
            {
                continue;
            }

            yield return
            [
                item.GetProperty("id").GetString()!,
                item.GetProperty("type").GetString()!,
                item.GetProperty("culture").GetString()!,
                item.GetProperty("timeZone").GetString()!,
                item.GetProperty("input").GetRawText(),
                item.GetProperty("expected").GetString()!
            ];
        }
    }

    [Fact]
    public async Task Instant_without_utc_designator_is_rejected()
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T15:00:00Z", CultureInfo.InvariantCulture));
        var formatter = new DisplayFormatter(
            catalog, catalog, catalog, catalog, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(clock), clock);
        using var input = JsonDocument.Parse("\"2026-09-27T17:35:00\"");

        await Assert.ThrowsAsync<FormatException>(() => formatter.FormatAsync(
            "dateTime", input.RootElement, "es-AR", "America/Argentina/Buenos_Aires",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Disabled_reference_entries_can_still_be_displayed()
    {
        var catalog = new JsonReferenceDataCatalog();
        var currency = await ((ICurrencyCatalog)catalog).FindAsync("CLP", TestContext.Current.CancellationToken);
        var zone = await ((ITimeZoneCatalog)catalog).FindAsync("America/New_York", TestContext.Current.CancellationToken);
        Assert.NotNull(currency);
        Assert.NotNull(zone);
        Assert.False(currency.IsEnabled);
        Assert.False(zone.IsEnabled);

        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T15:00:00Z", CultureInfo.InvariantCulture));
        var formatter = new DisplayFormatter(
            catalog, catalog, catalog, catalog, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(clock), clock);
        using var money = JsonDocument.Parse("{\"amount\":1234,\"currency\":\"CLP\"}");
        using var timeZone = JsonDocument.Parse("\"America/New_York\"");

        Assert.Equal("CLP 1.234", await formatter.FormatAsync(
            "money", money.RootElement, "es-AR", "America/Argentina/Buenos_Aires",
            TestContext.Current.CancellationToken));
        Assert.Equal("Nueva York (GMT−4)", await formatter.FormatAsync(
            "timeZone", timeZone.RootElement, "es-AR", "America/New_York",
            TestContext.Current.CancellationToken));
    }

    private sealed class CountingCultureCatalog(ICultureCatalog inner) : ICultureCatalog
    {
        public int ListCalls { get; private set; }

        public async Task<IReadOnlyList<CultureCatalogEntry>> ListAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            return await inner.ListAsync(cancellationToken);
        }

        public Task<CultureCatalogEntry?> FindAsync(string code, CancellationToken cancellationToken) =>
            inner.FindAsync(code, cancellationToken);
    }
}
