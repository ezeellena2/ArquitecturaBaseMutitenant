Caché con HybridCache: toda clave pasa por `CacheKeys` y tiene prefijo de alcance `t:`, `s:`, `u:` o `p:`. La invalidación ocurre después del commit; la lectura para llenarlo abre un scope propio.

Antes de escribir, leé: [multitenancy](../../../docs/rules/multitenancy.md) · [guardado](../../../docs/rules/guardado.md) · [backend §17](../../../docs/architecture/backend.md#17-logging-openapi-health-rate-limiting-caché).

Copiá de: `../ArquitecturaBase/src/ArquitecturaBase.Infrastructure/Caching/HybridCacheExtensions.cs` para el patrón de lectura (si se necesita en un reader). Lo prueba `CacheKeyScopeTests` (E2).
