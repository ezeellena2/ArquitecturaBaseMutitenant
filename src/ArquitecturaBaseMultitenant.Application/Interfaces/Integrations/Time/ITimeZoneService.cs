using ArquitecturaBaseMultitenant.Application.Models.Time;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;

/// <summary>Convierte instantes UTC y fechas civiles con reglas IANA para que los casos de uso no calculen zonas horarias por su cuenta.</summary>
public interface ITimeZoneService
{
    DayRangeUtc GetDayRangeUtc(DateOnly date, string timeZoneId);

    TimeSpan GetUtcOffset(string timeZoneId);

    TimeSpan GetUtcOffset(string timeZoneId, DateTime instantUtc);

    DateTime ConvertToLocal(DateTime instantUtc, string timeZoneId);

    DateTime ConvertToUtc(DateTime localDateTime, string timeZoneId);
}
