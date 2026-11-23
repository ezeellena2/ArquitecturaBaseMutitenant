# Cuotas: límites de pedidos compartidos entre réplicas

**Regla:** toda petición a `/api` y toda ruta que declare un perfil cuentan en las **cuotas compartidas** antes de llegar al negocio. Los doce perfiles son cerrados: las cuotas viven en la configuración de Plataforma, que las edita un operador con versión y motivo. Un rechazo es 429 `Security.RateLimit.Exceeded` con `retryAfter` y `Retry-After`.

## Cómo se hace
1. **Perfiles generales.** `RateLimitMiddleware` corre después de autenticar y de resolver la organización, y aplica a la vez:
   - la IP real (`ip`, o `publicIp` en una ruta `[PublicSite]`);
   - la identidad firmada (`identity`), si hay sesión;
   - la organización del claim (`tenant`), nunca la del subdominio.
2. **Perfil de una ruta.** Va en la acción, con `[RateLimit(RateLimitProfile.LoginCode)]`. Las rutas de ingreso, invitaciones y webhook cuentan por IP; las del Asistente, por persona. Un pedido sin sesión no cuenta en una cuota de persona: lo cuenta la IP y el acceso lo rechaza con 401. Un módulo declara los perfiles en sus rutas y se van con ellas:
   - `assistantQuickQuery` en `quick`;
   - `assistantConversation` al crear una conversación y en cada turno;
   - `assistantNewKey`, con un filtro del módulo, solo cuando llega una clave nueva;
   - `whatsAppWebhook` en el webhook.
3. **Un error propio.** Una ruta que conserva su código implementa `IRateLimitRejection` en su metadata, como el webhook de un módulo.
4. **La admisión.** `IRateLimitService.AdmitAsync` lee las cuotas guardadas desde la caché compartida; mientras no hay configuración, usa las de arranque. Un solo script de Redis admite el pedido y suma únicamente si todas sus cuotas tienen lugar, así un rechazo no gasta las demás.
   - Las ventanas son fijas UTC.
   - La clave es `<prefijo>{rl}:<perfil>:<ventana>:<inicio>:<partición>`.
   - La IP entra como HMAC, nunca completa. Una IPv6 se agrupa por su /64 antes del HMAC (N20 de ArquitecturaBase, sin programar): hoy `RateLimitIpHasher` y `WhatsAppQuotaIpHasher` hashean la dirección entera, y quien tiene un /64 cambia de dirección para saltear la cuota.
5. **Sin Redis.** Cuenta en `platform."RateLimitCounters"`, con la unidad de trabajo del servicio de cuotas, antes del negocio y sin anidar su transacción.
   - Al caer Redis, el contador sigue desde lo último que la instancia vio en Redis.
   - Al volver, Redis sigue desde lo que contó PostgreSQL. Ninguna caída reinicia una cuota.
6. **Cuotas de arranque.** Son `RateLimiting:<Perfil>:PermitLimit` y `WindowSeconds`, con `ValidateOnStart`. El seed las guarda solo al crear la configuración. Los tests arrancan con el tope (`TestRateLimits`) y fijan las suyas en una base y un ambiente de Redis propios.
- Quedan afuera los assets, los probes (`/health`, `/alive`) y `/connect` (la autorización OIDC). El límite de esas rutas en la capa de red queda para la E10.
- Las cuotas de negocio del Asistente (ADR 0048, 0049 y 0051) siguen aparte. Las compartidas frenan antes de llamar al proveedor. Las de IA ([ADR 0064](../decisions/0064-reglas-de-toda-la-ia.md), sin programar) cortan a la medianoche de la zona de la organización, y fuera de una, a la de la zona del perfil (271): el cupo diario de cada persona en cada organización, también las relecturas, y las lecturas de documentos por persona o contacto y por organización (`Forms:DocumentReadsPerOrganizationPerDay`, decisión 217 de formularios). El tope mensual va por el mes de la organización. Lo que venció sin llegar al proveedor devuelve lo reservado, y sin cupo no se lee ni se guarda el archivo de una subida. Las subidas toman un lugar por persona, y la cola de cada copia la reparte entre organizaciones.
- Los topes diarios de correo también son cuotas de negocio y van aparte, sin sumar un perfil a los doce ([ADR 0063](../decisions/0063-topes-de-correo-de-la-plataforma.md), sin programar): uno global de la plataforma por debajo del cupo de la cuenta, con una reserva para invitaciones y aviso al 80 %; uno de invitaciones por persona y otro por organización; uno de códigos por dirección y otro por cuenta para Mi cuenta. Cuentan en 24 horas móviles, en una tabla global y durable, y sus valores van en `Email:*`.
- El tope de consultas a Meta de Formularios (la vista oficial y «Consultar publicación») también es una cuota de negocio y va aparte, sin sumar un perfil a los doce: `Forms:MetaCalls:PerOrganizationPerMinute`, por organización, contado en la base y compartido entre copias (E14).

## Prohibido
- `UseRateLimiter`, `[EnableRateLimiting]` o un limitador por proceso, que no se comparten entre réplicas.
- Un perfil fuera de los doce del contrato.
- Partir por algo que elige el cliente (un header o el subdominio), o guardar la IP completa.
- Un tope de correo por proceso, o uno por organización sin el global: el cupo es de la cuenta que manda, no de cada organización (ADR 0063).
- Llamar al proveedor antes de admitir la cuota de la ruta.

## Copiá de
- `Api/RateLimiting/RateLimitAttribute.cs` para declarar el perfil de una ruta (E9).
- `Api/RateLimiting/IRateLimitRejection.cs` para que una ruta conserve su error (E9).

## Lo verifica
- `DistributedRateLimitTests` (E9)
- `SecurityOptionsTests` (E9)
- `PlatformRateLimitsTests` (E9)

## Detalle
Valores iniciales: `PlatformRateLimits.Initial` · topes de correo: [ADR 0063](../decisions/0063-topes-de-correo-de-la-plataforma.md) · [ADR 0052](../decisions/0052-cache-compartida-redis.md) · [backend.md §17](../architecture/backend.md#17-logging-openapi-health-rate-limiting-caché)
