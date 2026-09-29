Caché con HybridCache: toda clave pasa por `CacheKeys` y tiene prefijo de alcance `t:`, `s:`, `u:` o `p:`. La invalidación ocurre después del commit; la lectura para llenarlo abre un scope propio.

Antes de escribir, leé: [multitenancy](../../../docs/rules/multitenancy.md) · [guardado](../../../docs/rules/guardado.md) · [backend §17](../../../docs/architecture/backend.md#17-logging-openapi-health-rate-limiting-caché).

Copiá de: `HybridCacheExtensions.cs` (adaptado de `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Caching/HybridCacheExtensions.cs`) para llenar caché desde otro scope; `ReferenceDataCache.cs` para las claves globales y su invalidación. Lo prueban `CacheKeyScopeTests` y `ReferenceDataReaderTests` (E2).
