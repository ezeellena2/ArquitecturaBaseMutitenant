# Hoja de ruta

Orden vigente: **8 → 8b → 11 → 9 → 10 → 12 a 17**. [AGENTS.md](../../AGENTS.md) define el modo rápido:
contrato primero, programación por dueños y una sola puerta. Tras un arreglo se verifica sólo lo afectado.
La tabla indica el estado; las reglas y conducta viven en fichas, ADR y features.
El historial se recupera con git show historial-docs:<ruta>, sin cargar planes cerrados. Los planes y la revisión retirados el 2026-10-13 (E8, E8b, correctivos y limpieza) se encuentran con git log --diff-filter=D -- <ruta> y se leen con git show <commit>^:<ruta>.

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
| 8 · WhatsApp de plataforma | Cerrada | [WhatsApp](../features/whatsapp.md), [Identidad](../features/identidad.md) |
| 8b · Asistente | Cerrada | [ADR 0048](../decisions/0048-asistente.md), [0049](../decisions/0049-precisiones-whatsapp-y-asistente.md), [Asistente](../features/asistente.md) |
| 11 · WhatsApp de atención | Plan corto listo; esperan las decisiones D1–D8 | [Plan](2026-10-13-etapa-11-whatsapp.md), [ADR 0038](../decisions/0038-whatsapp-de-atencion.md) |
| 9 · Endurecimiento | Pendiente | Decisiones de abajo |
| 10 · Operación | Pendiente | Decisiones de abajo |
| 12 · Formularios por WhatsApp | Pendiente | [Hoja de ruta de formularios](2026-10-12-formularios-dinamicos-hoja-de-ruta.md) |
| 13 · Catálogos | Pendiente | Ídem |
| 14 · Fotos y documentos | Pendiente | Ídem |
| 15 · Planilla y Excel | Pendiente | Ídem |
| 16 · Reglas, aprobación y PDF | Pendiente | Ídem |
| 17 · Biblioteca de plantillas | Pendiente | Ídem |

## Etapa 11: WhatsApp de atención

La plataforma y cada organización atienden con el mismo módulo: alta de números por Meta, bot, equipos, bandejas de Clientes e Internos, contactos con consentimiento, plantillas y difusiones, y la fuente productiva de «Mis mensajes con empresas». El [plan corto](2026-10-13-etapa-11-whatsapp.md) tiene el contrato, las tareas, los datos de Meta verificados el 2026-10-13 y las decisiones de producto pendientes.

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
- **Heredado de los correctivos del 12/10 y de la revisión de la 8b** (se difirió a esta etapa):
  - `PermissionService` hace una consulta por empresa en cada chequeo de Business (2 + N), aunque el permiso pedido no se proyecte desde las empresas. Proyectar solo los permisos de `Permissions.OrganizationProjectedFromCompany` y cachear junto con la caché de permisos.
  - Los tiempos de seguridad fijos (tokens, cookie, idempotencia, enlace de ingreso, lote de la baja) y el límite del webhook de WhatsApp pasan a opciones con `ValidateOnStart`, sin cambiar sus valores.
  - Un formateador en el CI, aplicado en un commit propio que reformatee todo el repo.
  - Límites de pedidos en el asistente: consultas fijas, creación de conversaciones y el `PUT` de configuración con clave nueva (llama a Anthropic y no debe servir para probar claves).
  - El cliente de Anthropic usa `IHttpClientFactory`, como WhatsApp, en vez de un `HttpClient` por llamada.
  - Una guarda de arquitectura: toda acción con modo de idempotencia durable declara `[IdempotencyDurable]`; hoy el filtro la deja pasar sin reserva si falta.
  - `AssistantOrganizationPolicy` conserva `DailyMessageLimit`, `MonthlyBudget` y `RetentionDays`, que nadie lee: se quitan con su migración.

## Etapa 10: despliegue y operación

- Dockerfile de la Api con el SPA en wwwroot.
- Chequeo cruzado de contratos en el CI del front: descarga también el repo del back y corre `contracts:check` sin saltearlo (enmienda de la ADR 0016). Si el repo del back es privado, se usa un token de solo lectura.
- Migration bundle ejecutado con `mt_owner`.
- Adaptador de nube de `IFileStore` (S3 o Azure Blob, se elige acá; [ADR 0048](../decisions/0048-asistente.md), D15). Lo usan el asistente y, desde la E14, los formularios.
- CI/CD con GitHub Actions y OIDC.
- Secretos, certificados de OpenIddict y SMTP real.
- `docs/operations/runbook.md`: alta de `mt_app` **y creación de la base con ICU `es-AR`**, backup y restore, rotación de certificados, suspensiones, purga y baja de datos personales.
- **Datos personales (P7, segunda parte):** `POST /api/me/data-export` (`[Idempotent]`), `platform.DataExports`, la preparación en segundo plano del JSON y el correo «Exportación de datos lista» (solo por correo), con un enlace de descarga que vence a las 48 h ([datos-personales](../rules/datos-personales.md)), y su participante de la baja, que borra los archivos. En el front, «Exportar mis datos» en la sección Privacidad de `/cuenta`. La baja de la cuenta ya está en la Etapa 3.

## Etapas 12 a 17: formularios por WhatsApp

Una organización arma formularios, los manda por WhatsApp a sus empleados y ve las respuestas numeradas en una planilla.
- **Etapas y fuente:** son seis, cada una con su sección y las decisiones de producto tomadas en la [hoja de ruta](2026-10-12-formularios-dinamicos-hoja-de-ruta.md).
- **Requisitos:** la E11 es requisito duro, porque sin número de la organización no hay formularios. Van después de la E10 para nacer con las guardas de la E9 y la nube de la E10.
- **Módulo:** `Forms` es un módulo quitable aparte de `WhatsApp`, unidos por puertos neutrales del núcleo.
- **ArquitecturaBase:** es referencia, no fuente de código; la estructura sale de este repo.
- **Specs:** cada etapa tiene su especificación detallada en [`formularios/`](formularios/README.md).
- **Cómo se trabaja cada etapa:**
  - se escribe en el formato de [etapa en paralelo](../guides/etapa-en-paralelo.md) cuando le toca;
  - sus tableros se dibujan en el lienzo antes de programar.
- **Después:** el asistente en WhatsApp con audios y sugerencias, y B2C.
