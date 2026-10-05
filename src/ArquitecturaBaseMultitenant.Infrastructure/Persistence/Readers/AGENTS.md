Readers de EF: consultas `AsNoTracking`, proyección a contratos de Application y sin escrituras. `ReferenceDataReader` lee las once tablas globales de `platform`, devuelve también filas deshabilitadas y usa HybridCache `p:ref:<catálogo>` con una conexión de otro scope. Solo las asociaciones `TimeZoneCountry` habilitadas forman `CountryCodes` actuales.

Antes de editar, leé: [datos-de-referencia](../../../../docs/rules/datos-de-referencia.md) · [persistencia-ef](../../../../docs/rules/persistencia-ef.md) · [multitenancy](../../../../docs/rules/multitenancy.md).

Copiá de: `RoleReader.cs` y `CompanyReader.cs` para un listado (`SortMap`, `ApplySearch`, `ToPagedResultAsync` y desempate por Id); `ReferenceDataReader.cs` para catálogos globales con caché en otro scope. Lo verifican `PaginationTests`, `SearchTests` y `Api.IntegrationTests/Persistence/ReferenceDataReaderTests.cs`.
