# ADR 0058 · Sin Redis la copia sigue en el tráfico

**Estado:** aceptada como decisión técnica el 2026-11-18, con el traspaso 15 de ArquitecturaBase. Enmienda la [0052](0052-cache-compartida-redis.md) en lo que dice de la readiness; el resto de la 0052 sigue vigente. Toma la decisión 2 de la 0032 de ArquitecturaBase («Sin Redis la app sigue»), sin su decisión 1: acá los permisos ya leen sus generaciones de PostgreSQL en cada lectura y caen a la base sin Redis.

## Contexto

La 0052 dice que la readiness (`/health`) incluye Redis y falla cuando no está, y `RedisHealthCheck` respondía Unhealthy (503). El despliegue de la E10 usa `/health` como readiness. Con un corte de Redis de dos minutos, Container Apps saca del tráfico a todas las copias, y **todas las organizaciones** se quedan sin app, sin ingreso y sin webhooks de Meta.

Nada de eso necesita Redis. Por la misma 0052, sin Redis cada lectura de la caché va a PostgreSQL, permisos incluidos, y las cuotas cuentan en `platform."RateLimitCounters"` ([cuotas](../rules/cuotas.md) §5). Las claves de Data Protection viven en la base. La readiness sacaba del tráfico una copia que podía atender.

## Decisión

1. **El chequeo de Redis falla en Degraded, que responde 200.** La copia sigue en el tráfico mientras Redis no responde. `RedisHealthCheck` se registra con `failureStatus: Degraded` y devuelve ese estado tanto si atrapa la falla (sin conexión o un comando vencido) como si lanza otra excepción.
2. **`/alive` sigue sin incluirlo.** Reiniciar la copia no arregla un Redis caído.
3. **`/health` dice una sola palabra:** `Healthy`, `Degraded` o `Unhealthy`, sin el detalle de cada chequeo. Un monitoreo que quiera ver la caída de Redis mira esa palabra o las métricas de fallback de la caché, no el código HTTP.

## Consecuencias

- **Con Redis caído anda todo:** el arranque, el ingreso, los permisos, la configuración, la app y los webhooks. Cada lectura de la caché va a PostgreSQL y cada cuota cuenta ahí, así que la base recibe más consultas mientras dura el corte.
- **Un pedido no espera más que el timeout de una operación** (`Cache:OperationTimeoutMilliseconds`): la conexión falla rápido mientras Redis no responde y reconecta sola al volver.
- **Una espera que quiera saber si Redis volvió** lee la palabra `Healthy`, no el 200. Así lo hace `DistributedRateLimitTests`.
- **El front** muestra un texto propio para un 5xx sin `traceId` (el 502 o el 504 del ingress, o una respuesta que no es ProblemDetails), en lugar de pedir que se informe una referencia que no existe. Es la otra mitad de la 0032 de ArquitecturaBase y va con el front.

## Alternativas descartadas

- **Dejarlo como estaba:** un corte de Redis saca del tráfico a todas las copias y, en el multitenant, deja sin app a todas las organizaciones a la vez.
- **Sacar el chequeo de Redis:** la readiness dejaría de decir que Redis no responde. Degraded lo dice sin sacar la copia del tráfico.
- **Sacar la caché de permisos, como la decisión 1 de la 0032 de ArquitecturaBase:** acá no hace falta. La 0052 ya lee las generaciones de la base en cada lectura y cae a PostgreSQL sin Redis.

## Verificación

`HealthCheckTests` comprueba que el chequeo `cache` falle en Degraded y no vaya en liveness. `DistributedCacheTests` levanta una API con un Redis inexistente: el registro, el ingreso y `/api/me` responden 200, `/alive` responde 200 y `/health` responde 200 con `Degraded`.
