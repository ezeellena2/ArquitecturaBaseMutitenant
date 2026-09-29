Readers de EF: consultas `AsNoTracking`, proyección a contratos de Application y sin escrituras. `ReferenceDataReader` lee las once tablas globales de `platform`, devuelve también filas deshabilitadas y usa HybridCache `p:ref:<catálogo>` con una conexión de otro scope. Solo las asociaciones `TimeZoneCountry` habilitadas forman `CountryCodes` actuales.

Antes de editar, leé: [datos-de-referencia](../../../../docs/rules/datos-de-referencia.md) · [persistencia-ef](../../../../docs/rules/persistencia-ef.md) · [multitenancy](../../../../docs/rules/multitenancy.md).

Copiá de: `ReferenceDataReader.cs` para catálogos globales; `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Persistence/Readers/SystemSettingsReader.cs` para la lectura con caché en otro scope. Lo verifica `Api.IntegrationTests/Persistence/ReferenceDataReaderTests.cs`.
