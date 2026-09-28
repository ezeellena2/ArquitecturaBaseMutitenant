using ArquitecturaBaseMultitenant.Application.Models.Time;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;

public interface ITimeZoneService
{
    DayRangeUtc GetDayRangeUtc(DateOnly date, string timeZoneId);

    TimeSpan GetUtcOffset(string timeZoneId);

    DateTime ConvertToLocal(DateTime instantUtc, string timeZoneId);

    DateTime ConvertToUtc(DateTime localDateTime, string timeZoneId);
}
