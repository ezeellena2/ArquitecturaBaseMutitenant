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

/// <summary>
/// Comprueba que fechas, horas, monedas y números se presenten con el perfil compartido de cultura. Protege
/// la coherencia de formato entre tipos.
/// </summary>
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
        Assert.Equal("27/09/2026, 14:35", formatter.FormatInstant(
            new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Utc), context));
        Assert.Throws<ArgumentException>(() => formatter.FormatInstant(
            new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Unspecified), context));
        Assert.Equal("011 15-2345-6789", await formatter.FormatPhoneAsync(
            phone, context, TestContext.Current.CancellationToken));
        Assert.Equal("1.234,50", formatter.FormatDecimal(1234.5m, 2, context));
        Assert.Equal("12,5%", formatter.FormatPercent(0.125m, context));
        Assert.Equal(1, countedCultures.ListCalls);
    }

    [Fact]
    public async Task Typed_date_and_time_use_the_same_catalog_patterns_as_the_shared_contract()
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T15:00:00Z", CultureInfo.InvariantCulture));
        var formatter = new DisplayFormatter(catalog, catalog, catalog, catalog, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(clock), clock);
        var instantUtc = new DateTime(2026, 9, 27, 17, 35, 0, DateTimeKind.Utc);
        var spanish = await formatter.CreateAsync("es-AR", "America/Argentina/Buenos_Aires",
            TestContext.Current.CancellationToken);
        var english = await formatter.CreateAsync("en-US", "America/Argentina/Buenos_Aires",
            TestContext.Current.CancellationToken);

        Assert.Equal("27/09/2026", formatter.FormatDate(instantUtc, spanish));
        Assert.Equal("14:35", formatter.FormatTime(instantUtc, spanish));
        Assert.Equal("09/27/2026", formatter.FormatDate(instantUtc, english));
        Assert.Equal("2:35 PM", formatter.FormatTime(instantUtc, english));
        Assert.Throws<ArgumentException>(() => formatter.FormatDate(DateTime.SpecifyKind(instantUtc,
            DateTimeKind.Unspecified), spanish));
    }

    [Fact]
    public async Task Civil_dates_months_and_elapsed_times_follow_the_culture_without_a_zone()
    {
        var catalog = new JsonReferenceDataCatalog();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-09-27T15:00:00Z", CultureInfo.InvariantCulture));
        var formatter = new DisplayFormatter(catalog, catalog, catalog, catalog, catalog,
            new LibPhoneNumberDisplayFormatter(), new TimeZoneService(clock), clock);
        // Una zona muy adelantada no corre una fecha civil: no hay instante que convertir.
        var spanish = await formatter.CreateAsync("es-AR", "Pacific/Kiritimati", TestContext.Current.CancellationToken);
        var english = await formatter.CreateAsync("en-US", "Pacific/Kiritimati", TestContext.Current.CancellationToken);
        var october = new DateOnly(2026, 10, 1);

        Assert.Equal("01/11/2026", formatter.FormatDate(new DateOnly(2026, 11, 1), spanish));
        Assert.Equal("11/01/2026", formatter.FormatDate(new DateOnly(2026, 11, 1), english));
        // El mes como su rango (205): dentro de una oración y como rótulo, igual que `formatMonth` del front. Cualquier día
        // del mes da el mismo rango, y febrero de un bisiesto termina el 29.
        Assert.Equal("del 01/10/2026 al 31/10/2026", formatter.FormatMonthRange(october, spanish, MonthRangeStyle.Sentence));
        Assert.Equal("del 01/10/2026 al 31/10/2026",
            formatter.FormatMonthRange(new DateOnly(2026, 10, 31), spanish, MonthRangeStyle.Sentence));
        Assert.Equal("01/10/2026 – 31/10/2026", formatter.FormatMonthRange(october, spanish, MonthRangeStyle.Label));
        Assert.Equal("from 10/01/2026 to 10/31/2026", formatter.FormatMonthRange(october, english, MonthRangeStyle.Sentence));
        Assert.Equal("10/01/2026 – 10/31/2026", formatter.FormatMonthRange(october, english, MonthRangeStyle.Label));
        Assert.Equal("01/02/2028 – 29/02/2028",
            formatter.FormatMonthRange(new DateOnly(2028, 2, 15), spanish, MonthRangeStyle.Label));
        Assert.Equal("01/12/9999 – 31/12/9999", formatter.FormatMonthRange(DateOnly.MaxValue, spanish, MonthRangeStyle.Label));
        Assert.Throws<ArgumentOutOfRangeException>(() => formatter.FormatMonthRange(october, spanish, (MonthRangeStyle)7));

        // Cuánto pasó, en la unidad entera más grande y sin redondear para arriba.
        Assert.Equal("1 minuto", formatter.FormatElapsed(TimeSpan.FromSeconds(20), spanish));
        Assert.Equal("25 minutos", formatter.FormatElapsed(TimeSpan.FromMinutes(25.9), spanish));
        Assert.Equal("1 hora", formatter.FormatElapsed(TimeSpan.FromMinutes(119), spanish));
        Assert.Equal("23 horas", formatter.FormatElapsed(TimeSpan.FromHours(23.9), spanish));
        Assert.Equal("1 día", formatter.FormatElapsed(TimeSpan.FromHours(47), spanish));
        Assert.Equal("1.500 días", formatter.FormatElapsed(TimeSpan.FromDays(1500), spanish));
        Assert.Equal("1 minute", formatter.FormatElapsed(TimeSpan.FromMinutes(1), english));
        Assert.Equal("15 minutes", formatter.FormatElapsed(TimeSpan.FromMinutes(15), english));
        Assert.Equal("1 hour", formatter.FormatElapsed(TimeSpan.FromHours(1), english));
        Assert.Equal("2 days", formatter.FormatElapsed(TimeSpan.FromDays(2), english));
        Assert.Throws<ArgumentOutOfRangeException>(() => formatter.FormatElapsed(TimeSpan.FromMinutes(-1), spanish));
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
