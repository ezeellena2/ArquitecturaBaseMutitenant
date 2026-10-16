# ADR 0052 · Caché compartida en Redis

**Estado:** decisión técnica para implementar en E9, 2026-10-16. No declara la migración realizada. Ejecuta el alcance de Redis de la [hoja de ruta](../plans/2026-09-27-plan-de-desarrollo.md#etapa-9-endurecimiento).

## Contexto

`CachingRegistration` registra HybridCache sin almacenamiento distribuido. Sus implementaciones conservan valores locales entre 60 segundos y una hora. Quitar una entrada en una API no elimina la copia de otra. Tampoco alcanza con agregar Redis conservando esos valores locales: los estados y permisos revocados deben cambiar para todas las instancias.

Además, una carga iniciada antes de una mutación puede terminar después de invalidar. Una invalidación perdida durante una caída de Redis puede dejar entradas antiguas al volver el servicio. El TTL limita cuánto duran, pero no satisface la regla de ausencia de datos viejos.

## Decisión

**Redis es la única caché de datos.** HybridCache conserva su API y la coordinación de cargas dentro de una instancia, con lectura y escritura local deshabilitadas (`DisableLocalCache`). No hay L1, backplane ni suscriptores por proceso. Los consumidores acceden por las implementaciones de `Infrastructure/Caching`; una guarda impide agregar cachés paralelas y accesos directos fuera de los adaptadores declarados.

Las etiquetas tienen una generación durable en `platform.CacheTagVersions`. El repositorio técnico avanza las generaciones afectadas **dentro de la misma UoW** que cambia los datos; no abre otra transacción. Después del commit, el servicio invalida esas etiquetas en Redis. La generación es un entero monotónico, no una hora ni un valor calculado en la API. La tabla contiene identificadores de alcance y versiones, sin contenido de negocio, y se declara como infraestructura global con consumidores exactos en las guardas de persistencia.

Cada lectura obtiene de PostgreSQL, sin caché y en una consulta agrupada, las generaciones de sus etiquetas. La clave física contiene esas generaciones y la versión del esquema de serialización. Carga desde un scope propio, sin tracker ni transacción del escritor; verifica otra vez las generaciones antes de entregar el valor. Al cambiar alguna, descarta el resultado y vuelve a leer de PostgreSQL; ante cambios continuos entrega una lectura directa consistente, sin llenar la caché. Nunca etiqueta datos anteriores con una generación posterior.

Así, una petición iniciada después del commit no puede recuperar el valor anterior, aunque Redis siga conservándolo o una carga antigua lo escriba después. La invalidación por etiquetas después del commit evita reutilizar entradas; las generaciones hacen que su pérdida o retraso no quite corrección. No se retienen generaciones en memoria. Las lecturas en curso se ordenan en su última comprobación de versión; no se promete deshacer respuestas ya entregadas.

**Claves:** `arquitecturabasemt:<ambiente>:v<esquema>:<alcance>:<dato>:g<generaciones>`. El alcance sigue saliendo de `CacheKeys`: `t:<tenantId>`, `s:<sitioId>`, `u:<userId>` o `p:`. Ambiente obligatorio, identificadores internos, etiquetas igualmente prefijadas y nada derivado de un tenant enviado en un header o body. El adaptador verifica el contexto antes de usar una clave privada. No se comparten entradas entre acceso Personal y Business. `s:` queda reservado a proyecciones públicas autorizadas; E9 no agrega una proyección nueva.

### Inventario cerrado

El TTL administra espacio; no sustituye la invalidación. Los responsables invalidan después del commit también en workers y seeds, con la generación avanzada en su UoW.

| Datos / clave lógica | TTL | Etiquetas | Quién cambia e invalida |
|---|---|---|---|
| Estado de organización, `t:…:status` | 60 s | organización | Aprobar, suspender, reactivar o cerrar organización; provisión |
| Estado de cuenta, `u:…:status` | 60 s | cuenta | Identidad, suspensión/reactivación, baja y revocación de acceso |
| Membresía y clase de acceso, `t:…:member:<id>:status` | 60 s | organización, cuenta, membresías de organización | Alta/baja/cambio de membresía y estado de organización/cuenta |
| Permisos por rol, `t:…:role-permissions:<id>` | 1 h | roles de organización | Roles: editar, inactivar o quitar |
| Permisos efectivos y proyección de empresa por persona, `t:…:permissions:<userId>` | 60 s | organización, cuenta, roles, asignaciones y empresas de organización | Roles/asignaciones; membresías de organización y empresa; cambios de estado de empresa/cuenta |
| Módulos y vencimientos, `t:…:features` | 5 min | módulos de organización | Plataforma y provisión; el vencimiento se evalúa con TimeProvider en cada uso |
| Preferencias de organización, `t:…:settings` | 5 min | preferencias de organización | Configuración/nombre de organización |
| Configuración no sensible de Plataforma, `p:settings` | 5 min | configuración de plataforma | PlatformSettingsService y seed |
| Monedas, países, zonas, culturas y tipos fiscales, `p:ref:<catálogo>` | 1 h | referencias | ReferenceDataSeeder/DatabaseSeeder |

Los permisos efectivos se obtienen con lecturas agrupadas. `HasPermissionAsync` consulta empresas únicamente para claves de `Permissions.OrganizationProjectedFromCompany`. El mismo conjunto alimenta los listados de permisos; no se introduce una segunda interpretación de sus alcances. Las etiquetas de empresa son por organización para no tener una dependencia creciente de etiquetas por persona.

**Serialización explícita:** DTO internos permitidos, JSON UTF-8 con un sobre `{schemaVersion, tagVersions, value}`, sin serializar entidades, tipos arbitrarios ni nombres CLR. Un sobre incompatible se trata como miss y se reemplaza. Caché negativa sólo para las proyecciones del inventario y con las mismas etiquetas. Se excluyen secretos, tokens, claves de autenticador, códigos, correo y teléfono completos. `PlatformSettingsReader` separa `SalesEmail` y `SupportEmail` de su actual `CachedSettings`: esos contactos se leen de PostgreSQL y se unen con una versión coherente, sin entrar al sobre. El resto de los DTO del inventario se revisa con la misma lista permitida.

**Caída y recuperación:** errores de conexión/timeout/serialización de Redis provocan lectura directa de PostgreSQL; no se capturan cancelaciones del cliente ni errores de PostgreSQL como un miss. Logs mediante LoggerMessage y métricas de hit/miss/fallback/invalidation-failure, sin contenido ni identificadores en etiquetas métricas. Las generaciones siguen avanzando durante la caída, por lo que no reaparece contenido viejo al reconectar. No hay cola ni worker nuevo para reparar invalidaciones. Un timeout acotado y opciones validadas al arrancar evitan bloquear cada pedido durante segundos.

**Infraestructura:** recurso Redis en `AppHost.cs`, volumen persistente en desarrollo normal; recorrido aislado y tests con Redis efímero propio. Parámetro secreto `redis-password`, cargado por los mecanismos existentes, sin valor en el repo; conexión por `ConnectionStrings:cache`. En producción se exige Redis administrado con TLS y credenciales externas. Readiness (`/health`) incluye Redis y falla cuando no está disponible; liveness (`/alive`) no depende de él. Las rutas de negocio conservan su fallback de lectura. Las réplicas comparten también las claves de Data Protection necesarias para sus cookies; E9 documenta el requisito y lo verifica en el host de pruebas, sin adelantar el despliegue de E10.

Los contadores de límites de pedidos usan el mismo Redis, con prefijo y expiración propios; son admisiones atómicas, no entradas HybridCache ni una fuente de autorización. Su contrato y comportamiento de degradación están en el [plan de E9](../plans/2026-10-16-etapa-9-endurecimiento.md).

## Alternativas descartadas

- L1 corta o pub/sub: dejan una ventana de permisos revocados y pierden mensajes durante desconexiones.
- Sólo Redis y TTL: una carga concurrente o una invalidación fallida puede resucitar un valor anterior.
- Cachear generaciones: reintroduce el problema que deben impedir.
- Replicar datos personales para ahorrar una consulta: contradice la restricción explícita del inventario.

El costo aceptado es una consulta agrupada de generaciones por lectura, además del miss. Reduce las consultas de datos y el 2 + N de permisos, sin prometer eliminar PostgreSQL del camino de autorización.

## Verificación

Testcontainers PostgreSQL + Redis y dos API independientes prueban: invalidar en A y leer actualizado en B; permisos/asignaciones/estados/módulos; rollback sin invalidación; carrera carga/commit; invalidación fallida y reconexión sin resurrección; Redis caído con respuesta de negocio 200; `t:` ajena inaccesible; ambiente distinto aislado; esquema viejo como miss; ningún secreto/contacto completo en bytes; `/health` no disponible y `/alive` vivo. Las nuevas convenciones llevan `CacheArchitectureTests` y la ficha `docs/rules/cache.md` durante B2.

Fuentes técnicas: [HybridCache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0) documenta que sus tags invalidan lógicamente y que la coordinación de cargas es por instancia; el [código de invalidación](https://source.dot.net/Microsoft.Extensions.Caching.Hybrid/Internal/DefaultHybridCache.TagInvalidation.cs.html) muestra su estado local. La garantía adicional de generaciones de esta ADR es una decisión del proyecto.
