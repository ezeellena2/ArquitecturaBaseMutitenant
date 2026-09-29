# Fechas y zonas horarias

**Regla:** todo instante se guarda y viaja en **UTC**. Una fecha sin hora es `DateOnly`. El backend no convierte para mostrar: eso lo hacen `shared/format` en el front y `DisplayFormatter` en correos y WhatsApp.

## Cómo se hace
- El reloj es `TimeProvider`, inyectado: `timeProvider.GetUtcNow().UtcDateTime`.
- Una propiedad de instante es `DateTime` con sufijo **`Utc`** (`CreatedAtUtc`, `ExpiresAtUtc`) y columna `timestamptz`.
- Fecha civil (vencimiento, nacimiento): `DateOnly`, `date`. Hora civil (horario de atención): `TimeOnly`, `time`.
- JSON: `"2026-09-27T17:35:00Z"`, `"2026-09-27"`, `"14:30:00"`. Un instante sin offset es rechazado con 400.
- **Zona efectiva:** la de la cuenta; si no hay, la de la empresa de la pantalla; si no, `TenantSettings.DefaultTimeZoneId`; al comienzo, la zona por defecto de la cultura. Son IDs IANA. `ValidTimeZone()` consulta `ITimeZoneCatalog` para aceptar una zona habilitada en datos nuevos; `ITimeZoneService` hace la aritmética temporal.
- "Hoy" o "este mes" para una regla (reportes, vencimientos): `ITimeZoneService.GetDayRangeUtc(DateOnly, tz)`, nunca la cuenta a mano.
- **Catálogo:** `GET /api/reference-data` o `/api/reference-data/time-zones` devuelve los IDs y ciudades traducidas desde JSON E1 / tablas E2. Reemplaza `GET /api/time-zones`. El offset se calcula con el reloj al mostrar, nunca se almacena.
- **Offset:** `ITimeZoneService.GetUtcOffset(id, instantUtc)` calcula el desfase de un instante UTC explícito (incluido un instante histórico); la sobrecarga sin instante usa el `TimeProvider` inyectado para «ahora». No se reutiliza el offset de hoy para otra fecha.
- **Perfil de cultura:** `Cultures` contiene los patrones y `AmDesignator`/`PmDesignator`. `CultureProfiles` clona la cultura .NET y fija calendario gregoriano, separadores de fecha `/` y hora `:`, designadores del catálogo y signo negativo `-`; `DisplayFormatter` no hereda esos valores del ICU del host.
- En los tests, `FakeTimeProvider`.

## Prohibido
- `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now` y `DateTimeOffset.UtcNow`.
- `DateTime` sin sufijo `Utc`, o con `Kind` distinto de UTC.
- Guardar la hora local.
- Formatear una fecha con `ToString("dd/MM/yyyy")`.

## Copiá de
- `Api/Json/UtcDateTimeConverter.cs` (E1) · `Infrastructure/Time/TimeZoneService.cs` (E1)

## Lo verifica
- `BannedSymbols.txt` (E0): el build falla con los cinco símbolos prohibidos.
- `UtcDateTimeTests` (E1), `DateOnlyTimeOnlyTests` (E1), `ReferenceDataCatalogTests` (E1) para IDs y traducciones.
- `NoManualFormattingTests` (E1): sin formatos de fecha fuera de `DisplayFormatter`.

## Detalle
[backend.md §11 y §18](../architecture/backend.md#11-fechas-utc-y-zonas-horarias)
