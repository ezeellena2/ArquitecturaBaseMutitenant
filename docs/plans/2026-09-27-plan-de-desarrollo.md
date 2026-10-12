# Hoja de ruta

Orden vigente: **8 → 8b → 11 → 9 → 10**. [AGENTS.md](../../AGENTS.md) define el modo rápido:
contrato primero, programación por dueños y una sola puerta. Tras un arreglo se verifica sólo lo afectado.
La tabla indica el estado; las reglas y conducta viven en fichas, ADR y features.
El historial se recupera con git show historial-docs:<ruta>, sin cargar planes cerrados.

| Etapa | Estado | Fuente vigente |
|---|---|---|
| 0 · Esqueleto | Cerrada | [Backend](../architecture/backend.md), [front](../../../ArquitecturaBaseMutitenantFront/docs/architecture/frontend.md) |
| 1 · Núcleo transversal | Cerrada | [Fichas](../rules/README.md), [datos de referencia](../architecture/datos-de-referencia.md) |
| 2 · Persistencia/aislamiento | Cerrada | [Persistencia](../rules/persistencia-ef.md), [multitenancy](../architecture/multitenancy.md) |
| 3 · Identidad/cuenta | Cerrada | [Identidad](../features/identidad.md) |
| 4 · Roles | Cerrada | [Roles](../features/roles.md) |
| 5 · Plataforma | Cerrada | [Plataforma](../features/plataforma.md), [ADR 0042](../decisions/0042-operadores-de-plataforma.md) |
| 6 · Organización | Cerrada | [Organizaciones](../features/organizaciones.md) |
| 7 · Público/Personal | Cerrada | [Sitio público](../features/sitio-publico.md), [Personal](../features/personal.md) |
| 8 · WhatsApp de plataforma | Puerta en curso | [WhatsApp](../features/whatsapp.md), [Identidad](../features/identidad.md), [plan activo](2026-10-07-etapa-8-whatsapp.md) |
| 8b · Asistente | En curso; pendiente de plan corto del coordinador | [ADR 0048](../decisions/0048-asistente.md), [0049](../decisions/0049-precisiones-whatsapp-y-asistente.md), [Asistente](../features/asistente.md) |
| 11 · WhatsApp de atención | Pendiente | [ADR 0038](../decisions/0038-whatsapp-de-atencion.md) |
| 9 · Endurecimiento | Pendiente | Decisiones de abajo |
| 10 · Operación | Pendiente | Decisiones de abajo |

## Etapa 8b: pendientes vigentes

El [plan anterior](2026-10-08-etapa-8b-asistente.md), su [revisión](../reviews/2026-10-10-revision-8b.md)
y [propuesta](propuesta-asistente-llm.md) se conservan hasta que el coordinador nuevo confirme
que su plan corto contiene los pendientes. Las decisiones D1–D26 están en ADR 0048;
0049/0050/0051 y las decisiones posteriores del usuario gobiernan las precisiones.

## Etapa 11: WhatsApp de atención

La plataforma y cada organización atienden con el mismo módulo: alta de números por Meta,
bot, equipos, bandejas de Clientes/Internos, contactos con consentimiento, plantillas y difusiones.
El número de Plataforma se conecta desde su pantalla; Cargar teléfonos no revela cuentas.
Las [precisiones vigentes](../decisions/0038-whatsapp-de-atencion.md) gobiernan sobre el
[plan anterior aún pendiente de reescritura](2026-10-03-etapa-11-whatsapp.md).
La fuente visual es el lienzo vigente; el plan corto se prepara después de la 8b.
Se reutilizan transporte, firma, outbox, plantillas e identidad de la 8; se agrega la fuente
productiva de Mis mensajes con empresas para el asistente Personal.
platform.whatsapp.manage entra cuando exista su primera ruta. Los globales respetan ADR 0050.
Antes de implementar el alta se verifican requisitos y precios vigentes de Meta según el plan de esa etapa.

## Etapa 9: endurecimiento

- **TOTP obligatorio para operadores** (lienzo v117, front `eb5626ea`). Hasta esta etapa, el operador del seed entra sin segundo factor.
  - **Ingreso y M-Ingreso:** los estados «Operador: …». Son: segundo factor; código incorrecto; demasiados códigos incorrectos, con el mismo bloqueo de la cuenta (`Identity.Account.LockedOut`, 15 minutos); código de recuperación; recuperación incorrecta («Ese código no es válido o ya lo usaste.»); configurar el autenticador; su código incorrecto, y guardar los códigos. Nada menciona al operador antes del código (0043).
  - **Reautenticación reciente** antes de las acciones sensibles de Plataforma: operadores, cuentas, recuperaciones, organizaciones, documentos legales y configuración.
    - Se hace en «Confirmá que sos vos», con el código de la app o uno de recuperación.
    - La ventana de «reciente» la fija el plan de la etapa.
    - Las personas siguen sin código de confirmación en Mi cuenta (0045).
    - Lo aprobó el usuario el 2026-10-10.
  - **Restablecer el autenticador** de un operador, desde el ⋮ de Cuenta-Plat y con motivo.
    - Cierra sus sesiones y anula sus códigos de recuperación.
    - Al volver a entrar, el operador lo configura de nuevo.
    - Lo aprobó el usuario el 2026-10-10.
- **Caché en Redis.** Hasta esta etapa, `HybridCache` vive en la memoria de la API. Acá **toda la caché pasa a Redis**, pensada antes de programar en una ADR que fija qué se cachea, cuánto dura, quién invalida y cuándo:
  - **Redis en el AppHost**, junto con PostgreSQL, la API y el front, con volumen persistente y contraseña en user-secrets; en producción, Redis administrado con TLS.
  - **Ningún dato viejo después de una invalidación.** `HybridCache` usa Redis como caché compartida por todas las instancias de la API. La ADR decide si queda una capa local corta con su invalidación propagada por Redis o si se lee siempre de Redis. El estado de organizaciones, membresías y permisos se ve invalidado en todas las instancias al instante.
  - **Claves e invalidación:** las claves siguen saliendo de `CacheKeys` (`t:`, `s:`, `u:`, `p:`), con un prefijo de aplicación y ambiente para compartir un Redis. Se invalida por etiquetas, después del commit.
  - **Qué se guarda:** serialización explícita y versionada, para que un cambio de forma del dato no rompa al leer una entrada vieja. Nunca se cachean secretos, tokens ni correos o teléfonos completos.
  - **Si Redis se cae:** la app sigue leyendo de PostgreSQL, con log y métrica, sin devolver errores. El health check de Redis entra en la preparación (readiness).
  - **Rate limit compartido** entre instancias, sobre el mismo Redis.
  - **Documentación:** la estrategia va en una tabla de `backend.md` §17 y en la ficha nueva `docs/rules/cache.md` con su test.
  - **Tests con Testcontainers Redis:** dos instancias de la API contra el mismo Redis (se invalida en una y la otra lee el valor nuevo); Redis caído → la app responde; una clave `t:` de una organización nunca se lee desde otra.
- Rate limit por tenant, por identidad y por IP (más estricto en las páginas públicas); cuotas en `PlatformSettings`.
- Security headers, CSP estricta y revisión de cookies.
- OpenTelemetry con `tenant.id` y `tenant.kind`.
- `/security-review` y **fuzz de aislamiento**: IDs cruzados en todas las rutas, con los dos accesos, en datos privados, públicos y compartidos, y por subdominio.

## Etapa 10: despliegue y operación

- Dockerfile de la Api con el SPA en wwwroot.
- Chequeo cruzado de contratos en el CI del front: descarga también el repo del back y corre `contracts:check` sin saltearlo (enmienda de la ADR 0016). Si el repo del back es privado, se usa un token de solo lectura.
- Migration bundle ejecutado con `mt_owner`.
- CI/CD con GitHub Actions y OIDC.
- Secretos, certificados de OpenIddict y SMTP real.
- `docs/operations/runbook.md`: alta de `mt_app` **y creación de la base con ICU `es-AR`**, backup y restore, rotación de certificados, suspensiones, purga y baja de datos personales.
- **Datos personales (P7, segunda parte):** `POST /api/me/data-export` (`[Idempotent]`), `platform.DataExports`, la preparación en segundo plano del JSON y el correo «Exportación de datos lista» (solo por correo), con un enlace de descarga que vence a las 48 h ([datos-personales](../rules/datos-personales.md)), y su participante de la baja, que borra los archivos. En el front, «Exportar mis datos» en la sección Privacidad de `/cuenta`. La baja de la cuenta ya está en la Etapa 3.
