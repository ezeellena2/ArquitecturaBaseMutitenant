# Fechas y zonas horarias

**Regla:** todo instante se guarda y viaja en **UTC**. Una fecha sin hora es `DateOnly`. El backend no convierte para mostrar: eso lo hacen `shared/format` en el front y `DisplayFormatter` en correos y WhatsApp.

## Cómo se hace
- El reloj es `TimeProvider`, inyectado: `timeProvider.GetUtcNow().UtcDateTime`.
- Una propiedad de instante es `DateTime` con sufijo **`Utc`** (`CreatedAtUtc`, `ExpiresAtUtc`) y columna `timestamptz`.
- Fecha civil (vencimiento, nacimiento): `DateOnly`, `date`. Hora civil (horario de atención): `TimeOnly`, `time`.
- JSON: `"2026-09-27T17:35:00Z"`, `"2026-09-27"`, `"14:30:00"`. Un instante sin offset es rechazado con 400.
- **Zona efectiva:** la de la cuenta; si no hay, la de la empresa de la pantalla; si no, `TenantSettings.DefaultTimeZoneId`. Son IDs IANA, validados con `ValidTimeZone()`.
- "Hoy" o "este mes" para una regla (reportes, vencimientos): `ITimeZoneService.GetDayRangeUtc(DateOnly, tz)`, nunca la cuenta a mano.
- En los tests, `FakeTimeProvider`.

## Prohibido
- `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now` y `DateTimeOffset.UtcNow`.
- `DateTime` sin sufijo `Utc`, o con `Kind` distinto de UTC.
- Guardar la hora local.
- Formatear una fecha con `ToString("dd/MM/yyyy")`.

## Copiá de
- `Api/Json/UtcDateTimeConverter.cs` (E1) · `Infrastructure/Time/TimeZoneService.cs` (E1)

## Lo verifica
- `BannedSymbols.txt`: el build falla con los cinco símbolos prohibidos.
- `UtcDateTimeTests`, `DateOnlyTimeOnlyTests`.
- `NoManualFormattingTests`: sin formatos de fecha fuera de `DisplayFormatter`.

## Detalle
[backend.md §11 y §18](../architecture/backend.md#11-fechas-utc-y-zonas-horarias)
