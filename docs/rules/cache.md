# Caché: Redis compartido con generaciones

**Regla:** Redis es la **única** caché ([ADR 0052](../decisions/0052-cache-compartida-redis.md)). Toda lectura cacheada pasa por `SharedCache` desde una caché del inventario cerrado; la clave lleva las **generaciones** de sus etiquetas, que avanza el mismo commit que cambia los datos. Nadie invalida a mano.

## Cómo se hace
1. **La entrada:** `new CacheEntry(CacheKeys.Tenant(tenantId, "dato"), [CacheTags.Organization(tenantId), …], vencimiento)`. La clave lógica sale de `CacheKeys` (`t:`, `s:`, `u:`, `p:`) y las etiquetas, de `CacheTags`. Las de membresías, roles, asignaciones y empresas son por organización, nunca por persona.
2. **La lectura:** `cache.GetOrLoadAsync(entry, estado, static (services, estado, ct) => …, ct)`. La carga corre en un scope propio, sin el tracker ni la transacción de quien llama. Si la clave es privada (`t:`), primero se verifica que el pedido esté en esa organización.
3. **El valor:** un DTO. Puede ser un record de `Application/Models`, una fila de un reader, un enum o un arreglo, pero nunca una entidad, un secreto, un token, un código ni un correo o teléfono completos. `PlatformSettingsReader` deja afuera los correos de contacto: los lee de PostgreSQL y los une por versión.
4. **Escribir:** como siempre, con la unidad de trabajo ([guardado](guardado.md)). `CacheTagCollector` junta las etiquetas de lo que cambia; `CacheGenerationInterceptor` las avanza en la misma transacción, antes del commit, y las invalida en Redis después. Un rollback no avanza ni invalida nada. Sirve igual para seeds, workers e Identity.
5. **Cambiar datos por SQL** (una migración de datos, por ejemplo) se saltea el guardado: hay que avanzar a mano la generación de sus etiquetas, `INSERT … ON CONFLICT ("Tag") DO UPDATE SET "Generation" = "Generation" + 1` sobre `platform."CacheTagVersions"`. En los tests, `CacheGenerations.AdvanceAsync`.
- La clave física es `arquitecturabasemt:<ambiente>:v<esquema>:<clave lógica>:g<generaciones>`. Las generaciones empiezan por la identidad de la base, un número al azar que fija la migración, así una base nueva o recreada nunca lee lo que otra dejó en Redis.
- Si las generaciones cambian mientras se carga, el valor se descarta y se vuelve a leer; con cambios continuos se entrega una lectura directa, sin llenar la caché.
- Una caída de Redis nunca rompe un pedido: la lectura va a PostgreSQL y la escritura o la invalidación se pierden (`ResilientDistributedCache`), sin quitar corrección. `/health` falla mientras Redis no responde; `/alive`, no.
- Varias réplicas comparten Redis y también las claves de Data Protection, que viven en PostgreSQL: la cookie que emite una vale en la otra.
- El mismo Redis guarda los contadores de cuotas (`RedisRateLimitCounter`), con la etiqueta `{rl}` y sin pasar por `SharedCache`: son admisiones atómicas, no entradas de caché ([cuotas](cuotas.md)).
- En desarrollo, el AppHost levanta el recurso `cache` con el parámetro `redis-password` (se genera una vez y queda en los user-secrets del AppHost) y un volumen propio. El recorrido aislado usa un Redis efímero, y los tests, uno compartido por la corrida (`SharedRedis`).

| Dato | Clave lógica | TTL | Etiquetas |
|---|---|---|---|
| Estado de organización | `t:…:status` | 60 s | organización |
| Estado de cuenta | `u:…:status` | 60 s | cuenta |
| Membresía y clase de acceso | `t:…:member:<persona>:<clase>:status` | 60 s | organización, cuenta, membresías |
| Permisos de los roles de organización de una persona | `t:…:permissions:<persona>:organization` | 60 s | organización, cuenta, membresías, roles, asignaciones |
| Permisos de los roles de empresa de una persona | `t:…:permissions:<persona>:companies` | 60 s | las anteriores y empresas |
| Módulos y vencimientos | `t:…:features` | 5 min | módulos |
| Preferencias de organización | `t:…:settings` | 5 min | preferencias |
| Configuración no sensible de Plataforma | `p:settings` | 5 min | configuración de Plataforma |
| Catálogos de referencia | `p:ref:<catálogo>` | 1 h | referencias |

## Prohibido
- Usar `HybridCache`, `IDistributedCache` o Redis fuera de `Infrastructure/Caching`.
- Una caché nueva fuera del inventario: se agrega a la ADR, a esta ficha y a `CacheArchitectureTests`.
- Invalidar a mano después del commit, o una copia local en memoria (L1).
- Una etiqueta por persona para datos que cambian por organización.
- Cachear entidades, datos personales o una clave privada leída desde otra organización.
- Leer las generaciones con el contexto de quien llama: vería lo que todavía no se confirmó.

## Copiá de
- `Infrastructure/Caching/TenantStatusCache.cs` para una entrada con una etiqueta (E9).
- `Infrastructure/Caching/RolePermissionCache.cs` para una clave privada y una carga con RLS (E9).

## Lo verifica
- `CacheArchitectureTests` (E9)
- `DistributedCacheTests` (E9)
- `PermissionProjectionTests` (E9)
- `CacheKeyScopeTests` (E2)

## Detalle
[ADR 0052](../decisions/0052-cache-compartida-redis.md) · [backend.md §17](../architecture/backend.md#17-logging-openapi-health-rate-limiting-caché)
