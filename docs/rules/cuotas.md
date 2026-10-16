# Cuotas: límites de pedidos compartidos entre réplicas

**Regla:** toda petición a `/api` y toda ruta que declare un perfil cuentan en las **cuotas compartidas** antes de llegar al negocio. Los doce perfiles son cerrados: las cuotas viven en la configuración de Plataforma, que las edita un operador con versión y motivo. Un rechazo es 429 `Security.RateLimit.Exceeded` con `retryAfter` y `Retry-After`.

## Cómo se hace
1. **Perfiles generales.** `RateLimitMiddleware` corre después de autenticar y de resolver la organización, y aplica a la vez:
   - la IP real (`ip`, o `publicIp` en una ruta `[PublicSite]`);
   - la identidad firmada (`identity`), si hay sesión;
   - la organización del claim (`tenant`), nunca la del subdominio.
2. **Perfil de una ruta.** Va en la acción, con `[RateLimit(RateLimitProfile.LoginCode)]`. Las rutas de ingreso, invitaciones y webhook cuentan por IP; las del Asistente, por persona. Un módulo declara los perfiles en sus rutas y se van con ellas:
   - `assistantQuickQuery` en `quick`;
   - `assistantConversation` al crear una conversación y en cada turno;
   - `assistantNewKey`, con un filtro del módulo, solo cuando llega una clave nueva;
   - `whatsAppWebhook` en el webhook.
3. **Un error propio.** Una ruta que conserva su código implementa `IRateLimitRejection` en su metadata, como el webhook de un módulo.
4. **La admisión.** `IRateLimitService.AdmitAsync` lee las cuotas guardadas desde la caché compartida; mientras no hay configuración, usa las de arranque. Un solo script de Redis admite el pedido y suma únicamente si todas sus cuotas tienen lugar, así un rechazo no gasta las demás.
   - Las ventanas son fijas UTC.
   - La clave es `<prefijo>{rl}:<perfil>:<ventana>:<inicio>:<partición>`.
   - La IP entra como HMAC, nunca completa.
5. **Sin Redis.** Cuenta en `platform."RateLimitCounters"`, con la unidad de trabajo del servicio de cuotas, antes del negocio y sin anidar su transacción.
   - Al caer Redis, el contador sigue desde lo último que la instancia vio en Redis.
   - Al volver, Redis sigue desde lo que contó PostgreSQL. Ninguna caída reinicia una cuota.
6. **Cuotas de arranque.** Son `RateLimiting:<Perfil>:PermitLimit` y `WindowSeconds`, con `ValidateOnStart`. El seed las guarda solo al crear la configuración. Los tests arrancan con el tope (`TestRateLimits`) y fijan las suyas en una base y un ambiente de Redis propios.
- Quedan afuera los assets, los probes (`/health`, `/alive`) y `/connect` (la autorización OIDC). El límite de esas rutas en la capa de red queda para la E10.
- Las cuotas de negocio del Asistente (ADR 0048, 0049 y 0051) siguen aparte. Las compartidas frenan antes de llamar al proveedor.

## Prohibido
- `UseRateLimiter`, `[EnableRateLimiting]` o un limitador por proceso, que no se comparten entre réplicas.
- Un perfil fuera de los doce del contrato.
- Partir por algo que elige el cliente (un header o el subdominio), o guardar la IP completa.
- Llamar al proveedor antes de admitir la cuota de la ruta.

## Copiá de
- `Api/RateLimiting/RateLimitAttribute.cs` para declarar el perfil de una ruta (E9).
- `Api/RateLimiting/IRateLimitRejection.cs` para que una ruta conserve su error (E9).

## Lo verifica
- `DistributedRateLimitTests` (E9)
- `SecurityOptionsTests` (E9)
- `PlatformRateLimitsTests` (E9)

## Detalle
[Plan E9](../plans/2026-10-16-etapa-9-endurecimiento.md) · [ADR 0052](../decisions/0052-cache-compartida-redis.md) · [backend.md §17](../architecture/backend.md#17-logging-openapi-health-rate-limiting-caché)
