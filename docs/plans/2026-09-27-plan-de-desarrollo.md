# Hoja de ruta

**Estado:** en curso · la tabla dice el estado de cada etapa. Lo que quedó para después está en el [backlog](../backlog.md).

Orden vigente: **8 → 8b → 11 → 9 → 10 → base → 12 → 13 → 14 → 15 → 16 → 17 → 18 → 19 → 20 → 21 → 22 → 23 → 24 → 25**. [AGENTS.md](../../AGENTS.md) define el modo rápido:
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
| 11 · WhatsApp de atención | Cerrada el 2026-10-21. El alta de una organización con un número real pasó a la I1 de la 10 | [Plan](2026-10-13-etapa-11-whatsapp.md), [ADR 0038](../decisions/0038-whatsapp-de-atencion.md) |
| 9 · Endurecimiento | Cerrada | [Plan](2026-10-16-etapa-9-endurecimiento.md), [ADR 0052](../decisions/0052-cache-compartida-redis.md) |
| 10 · Operación | Pendiente. El plan corto está listo, con las decisiones del usuario; la F5 de botones pasó a la base de la 12 | [Plan](2026-10-16-etapa-10-despliegue.md), [manual de operación](../operations/runbook.md) |
| Base de la 12 | Pendiente: lo que se copia de la maqueta, con los botones (la F5 de la 10), la campana en el núcleo y la letra al 85 %, que confirmó el usuario | [Formularios](#etapas-12-a-25-formularios-por-whatsapp) |
| 12 · Catálogos | Pendiente. Spec lista, con los tableros aprobados en el lienzo v141 y al día en la v145 | [Spec](formularios/12-catalogos.md), [hoja de ruta de formularios](2026-10-12-formularios-dinamicos-hoja-de-ruta.md) |
| 13 · Campos de los catálogos | Pendiente. Spec lista, con los tableros aprobados en el lienzo v145 (decisión del usuario del 27/10) | [Spec](formularios/13-campos.md) |
| 14 · Formularios | Pendiente. Spec lista, con los tableros aprobados en el lienzo v141 y al día en la v145; se redibuja `FRM-Editor`, con los títulos de sección | [Spec](formularios/14-formularios.md) |
| 15 · Respuestas, planilla y Excel | Pendiente. Spec lista; tableros a revisar y por dibujar | [Spec](formularios/15-respuestas.md) |
| 16 · Datos de las personas y filtros | Pendiente. Spec lista; tableros por dibujar | [Spec](formularios/16-datos-de-las-personas.md) |
| 17 · Ciclo de vida y copiar | Pendiente. Spec lista; tableros a revisar | [Spec](formularios/17-ciclo-de-vida.md) |
| 18 · Fotos y documentos | Pendiente. Spec lista, con los archivos del chat; tableros a revisar y por dibujar | [Spec](formularios/18-fotos-y-documentos.md) |
| 19 · Condiciones, cálculos y líneas | Pendiente. Spec lista; tableros por dibujar, y antes de planificar, la prueba con Meta | [Spec](formularios/19-condiciones-calculos-y-lineas.md) |
| 20 · Cargar desde la web | Pendiente. Spec lista; tableros por dibujar | [Spec](formularios/20-cargar-desde-la-web.md) |
| 21 · Reglas, aprobación y PDF | Pendiente. Resumen listo; tableros a revisar, 4 que se redibujan y otros por dibujar | [Resumen](formularios/21-reglas-y-aprobacion.md) |
| 22 · Biblioteca de plantillas | Pendiente. Spec lista; tableros a revisar, 12 que se redibujan y uno por dibujar | [Spec](formularios/22-biblioteca.md) |
| 23 · Asistente con Casos | Pendiente. Resumen listo; tableros a revisar y por dibujar | [Resumen](formularios/23-asistente.md) |
| 24 · Varios asistentes | Pendiente. Resumen listo; tableros por dibujar | [Resumen](formularios/24-varios-asistentes.md) |
| 25 · Documentos por WhatsApp | Pendiente. Resumen listo; tableros por dibujar, y antes de planificar, la prueba con Meta del archivo precargado | [Resumen](formularios/25-documentos.md) |

## Etapa 11: WhatsApp de atención

La plataforma y cada organización atienden con el mismo módulo: alta de números por Meta, bot, equipos, bandejas de Clientes e Internos, contactos con consentimiento, plantillas y difusiones, y la fuente productiva de «Mis mensajes con empresas». El [plan corto](2026-10-13-etapa-11-whatsapp.md) tiene el contrato, las tareas, los datos de Meta verificados el 2026-10-13 y las decisiones del usuario.

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
- Adaptador de nube de `IFileStore` (S3 o Azure Blob, se elige acá; [ADR 0048](../decisions/0048-asistente.md), D15). Lo usan el asistente y, desde la E18, los formularios.
- CI/CD con GitHub Actions y OIDC.
- Secretos, certificados de OpenIddict y SMTP real.
- `docs/operations/runbook.md`: alta de `mt_app` **y creación de la base con ICU `es-AR`**, backup y restore, rotación de certificados, suspensiones, purga y baja de datos personales.
- **Datos personales (P7, segunda parte):** `POST /api/me/data-export` (`[Idempotent]`), `platform.DataExports`, la preparación en segundo plano del JSON y el correo «Exportación de datos lista» (solo por correo), con un enlace de descarga que vence a las 48 h ([datos-personales](../rules/datos-personales.md)), y su participante de la baja, que borra los archivos. En el front, «Exportar mis datos» en la sección Privacidad de `/cuenta`. La baja de la cuenta ya está en la Etapa 3.

## Etapas 12 a 25: formularios por WhatsApp

Una organización arma formularios, los manda por WhatsApp a sus empleados y a sus clientes (decisión 49) y ve las respuestas numeradas en una planilla.
- **Etapas y fuente:** son catorce, de la 12 a la 25, en el orden de la decisión 78: **el número es el orden**, con los catálogos primero. Cada una tiene su sección y las decisiones de producto tomadas en la [hoja de ruta](2026-10-12-formularios-dinamicos-hoja-de-ruta.md). El alcance de cada una sale de la [evaluación del traspaso 2](2026-10-24-formularios-evaluacion-traspaso-2.md), que usa los números de antes, y de las decisiones 47 a 82. La equivalencia está en [`formularios/`](formularios/README.md).
- **La regla del orden:** cada etapa usa solo lo de las anteriores, lo que suma se ve en las pantallas que ya existen, y nada nombra, prueba ni dibuja algo de una etapa posterior. La controla `/plan-etapa`.
- **La base, al principio de la 12.** Se copia de la maqueta (`MaquetaMultitenant`, commits del 25/10):
  - los botones y avisos de la decisión 43, que eran la F5 de la 10;
  - la campana, en el núcleo, con las dos rutas de avisos;
  - «Más filtros» en `FilterBar`, con el buscador y hasta tres filtros en una fila;
  - un dato por celda, en un renglón: `DataTable` deja de dibujar `detail` en la computadora, y cada pantalla pasa a columnas lo que hace falta (decisión del usuario del 25/10);
  - las fechas siempre con fecha y hora completas en la zona de la cuenta: salen `relative`, `formatCalendar` y las fechas cortas, también en Usuarios y en la tarjeta de Auditoría de Inicio (decisión del usuario del 25/10);
  - los carteles de las pantallas cerradas pasan a la banda o a la campana: Usuario, Cuenta y Organización de Plataforma, Legales, Recuperaciones, Inicio personal y Mi cuenta;
  - la pestaña «Miembros» de una empresa no se dibuja sin el permiso (es un error del sistema real);
  - los filtros de Chats en una fila;
  - el tamaño general: la letra de la raíz al 85 %, con los tamaños de las clases en rem en lugar de px (sin `zoom`, que corre los menús); lo confirmó el usuario el 25/10;
  - la versión nueva se avisa en la campana, sin franja, y la columna ordenada muestra ▲ o ▼ (decisiones del usuario del 25/10);
  - «Dar de baja» y «Reactivar» en todo el sistema, con el estado «De baja»: Usuarios dejan «Deshabilitar», Roles y Empresas, «Inactivar», y los miembros de una empresa, «Quitar» y «Volver a sumar», en el front, en los textos del back y en sus tests (decisión del usuario del 25/10);
  - borrar la cuenta propia dice «Borrar mi cuenta» y su estado, «Por borrar», en lugar de «Baja pedida» (decisión del usuario del 26/10). Cambian también los textos de los correos y de las plantillas de WhatsApp de la cuenta: se actualiza `docs/operations/whatsapp-plantillas.md` y las plantillas se vuelven a aprobar en Meta, con el mismo nombre (eso lo hace el usuario);
  - un link del otro acceso abre «Otro acceso» (tableros `Otro-Acceso` y `Otro-Acceso-Persona` del lienzo v144), que ofrece entrar con ese acceso y después abre el link (decisión del usuario del 25/10).
- **Para la prueba con Meta de la 14:** una organización con su número conectado, que prueba la I1 de la 10.
- **Requisitos:** la E11 es requisito duro, porque sin número de la organización no hay formularios. Van después de la E10 para nacer con las guardas de la E9 y la nube de la E10.
- **Módulo:** `Forms` es un módulo quitable aparte de `WhatsApp`, unidos por puertos neutrales del núcleo.
- **ArquitecturaBase:** es referencia, no fuente de código; la estructura sale de este repo.
- **Specs:** cada etapa tiene su spec o su resumen en [`formularios/`](formularios/README.md).
- **Cómo se trabaja cada etapa:**
  - se escribe en el formato de [etapa en paralelo](../guides/etapa-en-paralelo.md) cuando le toca;
  - sus tableros se dibujan en el lienzo antes de programar.
- **Después:** primero las integraciones (decisión 76), y los catálogos con orígenes externos. Los clientes completan formularios desde el principio (decisión 49).
