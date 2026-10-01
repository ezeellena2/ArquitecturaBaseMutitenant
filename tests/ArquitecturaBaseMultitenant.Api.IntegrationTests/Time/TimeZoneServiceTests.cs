using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Time;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Time;

/// <summary>
/// Comprueba rangos UTC de días civiles según una zona IANA. Incluye cambios de horario, medianoches
/// inexistentes y horas repetidas.
/// </summary>
public sealed class TimeZoneServiceTests
{
    [Theory]
    [InlineData(2026, 3, 8, "2026-03-08T05:00:00Z", "2026-03-09T04:00:00Z", 23)]
    [InlineData(2026, 11, 1, "2026-11-01T04:00:00Z", "2026-11-02T05:00:00Z", 25)]
    public async Task Day_range_respects_daylight_saving_transitions(
        int year, int month, int day, string expectedStart, string expectedEnd, int expectedHours)
    {
        var zoneId = "America/New_York";
        var catalog = (ITimeZoneCatalog)new JsonReferenceDataCatalog();
        Assert.NotNull(await catalog.FindAsync(zoneId, TestContext.Current.CancellationToken));

        var range = CreateService().GetDayRangeUtc(new DateOnly(year, month, day), zoneId);

        Assert.Equal(ParseUtc(expectedStart), range.StartUtc);
        Assert.Equal(ParseUtc(expectedEnd), range.EndUtc);
        Assert.Equal(expectedHours, (range.EndUtc - range.StartUtc).TotalHours);
        Assert.Equal(DateTimeKind.Utc, range.StartUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, range.EndUtc.Kind);
    }

    [Fact]
    public void Day_range_uses_the_catalog_iana_id_for_argentina()
    {
        var range = CreateService().GetDayRangeUtc(
            new DateOnly(2026, 9, 27), "America/Argentina/Buenos_Aires");

        Assert.Equal(new DateTime(2026, 9, 27, 3, 0, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.Equal(new DateTime(2026, 9, 28, 3, 0, 0, DateTimeKind.Utc), range.EndUtc);
    }

    [Fact]
    public void Midnight_gap_starts_at_the_first_valid_local_time()
    {
        var range = CreateService().GetDayRangeUtc(new DateOnly(2026, 9, 6), "America/Santiago");

        Assert.Equal(new DateTime(2026, 9, 6, 4, 0, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 3, 0, 0, DateTimeKind.Utc), range.EndUtc);
    }

    [Fact]
    public void Repeated_midnight_starts_at_the_first_occurrence()
    {
        var range = CreateService().GetDayRangeUtc(new DateOnly(2026, 11, 1), "America/Havana");

        Assert.Equal(new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 2, 5, 0, 0, DateTimeKind.Utc), range.EndUtc);
    }

    [Fact]
    public void Numeric_offset_uses_the_injected_clock()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(clock);

        Assert.Equal(TimeSpan.FromHours(-5), service.GetUtcOffset("America/New_York"));

        clock.SetUtcNow(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromHours(-4), service.GetUtcOffset("America/New_York"));
    }

    [Fact]
    public void Numeric_offset_for_an_explicit_instant_respects_historical_daylight_saving()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(clock);
        var winterUtc = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var summerUtc = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(TimeSpan.FromHours(-5), service.GetUtcOffset("America/New_York", winterUtc));
        Assert.Equal(TimeSpan.FromHours(-4), service.GetUtcOffset("America/New_York", summerUtc));
        Assert.Equal(TimeSpan.FromHours(-5), service.GetUtcOffset("America/New_York"));
        Assert.Throws<ArgumentException>(() => service.GetUtcOffset("America/New_York",
            new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Conversion_requires_utc_instants_and_keeps_civil_time_without_utc_kind()
    {
        var service = CreateService();
        var instantUtc = new DateTime(2026, 9, 27, 15, 0, 0, DateTimeKind.Utc);

        var local = service.ConvertToLocal(instantUtc, "America/Argentina/Buenos_Aires");

        Assert.Equal(new DateTime(2026, 9, 27, 12, 0, 0), local);
        Assert.Equal(DateTimeKind.Unspecified, local.Kind);
        Assert.Throws<ArgumentException>(() => service.ConvertToLocal(new DateTime(2026, 9, 27, 15, 0, 0), "UTC"));
        Assert.Equal(instantUtc, service.ConvertToUtc(local, "America/Argentina/Buenos_Aires"));
        Assert.Throws<ArgumentException>(() => service.ConvertToUtc(instantUtc, "UTC"));
        Assert.Throws<ArgumentException>(() => service.ConvertToUtc(
            new DateTime(2026, 9, 6, 0, 30, 0), "America/Santiago"));
    }

    [Fact]
    public void Unknown_time_zone_is_rejected()
    {
        Assert.Throws<TimeZoneNotFoundException>(() => CreateService().GetDayRangeUtc(
            new DateOnly(2026, 9, 27), "Not/AZone"));
    }

    private static TimeZoneService CreateService(TimeProvider? clock = null) =>
        new(clock ?? new FakeTimeProvider(new DateTimeOffset(2026, 9, 27, 15, 0, 0, TimeSpan.Zero)));

    private static DateTime ParseUtc(string value) => DateTime.Parse(
        value,
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
}
