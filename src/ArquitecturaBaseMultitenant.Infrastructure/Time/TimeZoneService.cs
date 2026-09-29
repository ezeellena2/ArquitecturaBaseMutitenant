using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Models.Time;

namespace ArquitecturaBaseMultitenant.Infrastructure.Time;

public sealed class TimeZoneService(TimeProvider timeProvider) : ITimeZoneService
{
    public DayRangeUtc GetDayRangeUtc(DateOnly date, string timeZoneId)
    {
        var zone = FindZone(timeZoneId);
        var startLocal = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return new DayRangeUtc(
            ConvertBoundaryToUtc(startLocal, zone),
            ConvertBoundaryToUtc(endLocal, zone));
    }

    public TimeSpan GetUtcOffset(string timeZoneId) =>
        GetUtcOffset(timeZoneId, timeProvider.GetUtcNow().UtcDateTime);

    public TimeSpan GetUtcOffset(string timeZoneId, DateTime instantUtc)
    {
        if (instantUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The instant must be UTC.", nameof(instantUtc));
        }

        return FindZone(timeZoneId).GetUtcOffset(instantUtc);
    }

    public DateTime ConvertToLocal(DateTime instantUtc, string timeZoneId)
    {
        if (instantUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The instant must be UTC.", nameof(instantUtc));
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(instantUtc, FindZone(timeZoneId));
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public DateTime ConvertToUtc(DateTime localDateTime, string timeZoneId)
    {
        if (localDateTime.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException("The local date and time must have unspecified kind.", nameof(localDateTime));
        }

        var zone = FindZone(timeZoneId);
        if (zone.IsInvalidTime(localDateTime))
        {
            throw new ArgumentException("The local date and time does not exist in the time zone.", nameof(localDateTime));
        }

        return ConvertValidLocalToUtc(localDateTime, zone);
    }

    private static TimeZoneInfo FindZone(string timeZoneId) =>
        TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

    private static DateTime ConvertBoundaryToUtc(DateTime boundaryLocal, TimeZoneInfo zone)
    {
        // Some zones start a day inside a clock change or skip the entire civil day.
        // Move to its first real wall-clock minute; a skipped day yields an empty range.
        while (zone.IsInvalidTime(boundaryLocal))
        {
            boundaryLocal = boundaryLocal.AddMinutes(1);
        }

        return ConvertValidLocalToUtc(boundaryLocal, zone);
    }

    private static DateTime ConvertValidLocalToUtc(DateTime localDateTime, TimeZoneInfo zone)
    {
        if (zone.IsAmbiguousTime(localDateTime))
        {
            // The first occurrence of midnight is the inclusive boundary.
            var earliestOffset = zone.GetAmbiguousTimeOffsets(localDateTime).Max();
            return new DateTimeOffset(localDateTime, earliestOffset).UtcDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, zone);
    }
}
