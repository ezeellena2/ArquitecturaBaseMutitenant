# Base de la 12, segunda parte · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/base-de-la-12-segunda-parte.md` (contrato completo, datos y pasos con sus tests). Va después de la puerta de la base de la 12 y antes de la E12 (decisión 214). Tableros: lienzo v176; faltan los de la F10, los de roles en áreas plegables (260) los de la pieza para elegir y el orden de las tablas (262 y 264) y los de las reglas en Configuración (270 a 273), que se dibujan antes de programarlas. Sin decisiones pendientes: las de producto las tomó el usuario (255, 256, 260 a 264 y 270 a 273), y la 269 se adoptó sin pregunta.

**Fuente:** la 260 sale del traspaso 16, 01, de ArquitecturaBase (RO3-D y «un permiso incluye a otro»), y la 261 a la 264 y la 269, de los traspasos 16 y 17, con la sección del [backlog](../backlog.md#traspasos-16-y-17-para-la-segunda-parte-de-la-base) «Traspasos 16 y 17: para la segunda parte de la base»; la 270 a la 273, del traspaso 18, con la sección «[Traspaso 18, tanda 1: para la segunda parte de la base](../backlog.md#traspaso-18-tanda-1-para-la-segunda-parte-de-la-base)». Lo demás, de las secciones del [backlog](../backlog.md) «Traspaso 15, el núcleo: para programar», «Traspaso 15, diseño, fechas y números: para la segunda parte de la base», «Traspaso 15, la IA: para la segunda parte de la base» y, de «Traspaso 15, WhatsApp y ARCA: para programar», lo de WhatsApp y la CUIT. Lo que esas secciones mandan a otra etapa está en «Queda afuera».

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back A · núcleo y WhatsApp | B16, B1–B9 y B18 | Pendiente |
| Back B · formatos e IA | B10–B15 y B17 | Pendiente |
| Front | F1–F13 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: primero el contrato; después, a la vez, dos chats del back (A y B) y uno del front; una sola puerta al final ([etapa en paralelo](../guides/etapa-en-paralelo.md)). Cada tarea lleva su test dirigido, el build y los tests rápidos enteros de su repo, sin Docker y sin filtrar por módulo. Si la tarea toca rutas, persistencia, autenticación o aislamiento, al terminarla corren los tests de integración y de seguridad de su área. Un arreglo corre solo lo que toca.

Todo va en `main`, sin ramas, worktrees, stash, reset ni push. Cada chat commitea solo sus rutas con `git commit -- <rutas>`. Con Vite levantado nadie edita el `src` del front. La tabla de estado la actualiza la Guía.

**Migraciones en paralelo.** Back A y Back B comparten el checkout y el `ApplicationDbContextModelSnapshot`, así que una migración tomaría el modelo a medias del otro. Back A hace sus seis migraciones (B2, B3, B5, B6, B7 y B8), más la de la B16 si completa los roles con una. Back B no toca entidades, configuraciones de EF ni migraciones hasta que Back A commitee la B8: hace antes B10 a B12, que no cambian el modelo. Después de la B8, Back A sigue con B4 y B9, que tampoco lo cambian.

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md), «Forma de trabajo»). La llevan las tareas marcadas con **(R)**, completa y antes del commit: un subagente nuevo recibe el código y los escenarios, sin la conclusión de quien programó.

## Qué se verificó al arrancar

| Fila | Qué se encontró | Qué cambia |
| --- | --- | --- |
| Base de la 12 | Contrato, back y front hechos; faltan I1 a I4. La 214 la cierra como está | K0 |
| Lo incluido (260) | Cada permiso es independiente; `PermissionItem` no trae `includes`; Plataforma no tiene catálogo en la API (lo arma `permissionCatalog.ts`), y `RolePermissionCache` guarda lo asignado | K1 y B16, antes de la B1 |
| Delegación (0061) | `RoleService` cuida solo los roles de sistema; `OrganizationUserAdministratorGuard` y `LastCompanyAdminGuard`, solo al último; Plataforma no tiene nada | B1, tres guardas |
| Retención (0062) | Nada purga códigos, ingresos, OpenIddict ni invitaciones. `PeriodicWorker` no tiene lease: llega con la B6 de la E10 | B2, purgas idempotentes por lotes |
| Topes de correo (0063) | `EmailOptions` no tiene topes; `WhatsAppCodeQuota` cuenta por número que manda, no por cuenta | B3, con una tabla global por horas |
| IPv6 (N20) | `RateLimitIpHasher` y `WhatsAppQuotaIpHasher` usan la dirección entera | B3 |
| Bloqueo (N3) | `LoginCodeAttemptVerifier` ya cuenta solo contra un código activo, pero con la cuenta bloqueada un código correcto entra. `GoogleAccountGuard` y `ExternalLoginService` frenan por el bloqueo | B4 |
| La cola (N5, N8, O3 y 0066) | `OutboxStatus` no tiene `Uncertain`. Toda excepción es `Outbox.Transport.Failed`, que se reintenta. Un lease vencido se vuelve a reclamar. `LockIdsAsync` no ordena por destino ni por prioridad | B5. La reserva por destino estaba en la B6 de la E10 y entra acá, porque «el chat espera» la necesita |
| Invitación | `Invitation` no tiene estado de entrega ni id de la cola. `IOutboxDeliveryObserver` solo registra lo aceptado, y la confirmación corre sin organización | B6 deriva el estado al leer, desde la cola, como `WhatsAppConversationReader`: la cola nunca escribe en `tenant.*` |
| Transporte de WhatsApp | `WhatsAppCloudClient` reintenta un 5xx, `Network`, `Timeout` e `InvalidResponse`. Sus códigos (`RecipientNotAllowed`, `WindowExpired`, `Credentials`, `Permissions`) no son los de la 0066 | B7; la migración pasa los guardados |
| Contenido del chat (0065) | En claro. Hay además un `engagement.WhatsAppConversationLinks.LastMessagePreview` que el backlog no nombra | B8 lo suma |
| La organización en EF | `TenantConnectionInterceptor` fija `app.tenant_id` desde `ITenantContext` | el conversor de B8 sale de ahí |
| Resiliencia (N18) | En `ServiceDefaults/Extensions.cs:34` falta `DisableForUnsafeHttpMethods`; WhatsApp ya lo tiene | B9 |
| El 500 (N10) | La Api no abre un scope con la persona y la organización, y la consola no es JSON | B9 |
| Textos neutrales (N22) | `Errors`, `Audit`, `Notifications`, `Permissions` y `Validation` nombran a WhatsApp o al asistente | B9, con una lista escrita para los permisos del catálogo |
| Datos de la instalación (B1) | Hay seis valores en `appsettings.Development.json` | B9; el usuario los carga en I1 |
| Formatos (205 y 208) | `cultures.source.json` va sin coma, con `LongDatePattern` y con `{number} %`. `FormatMonth` y `FormatMonthLabel` se usan solo en `AssistantBudgetEmailRenderer` | B10 |
| CUIT | `ArgentineCuitValidator` acepta once ceros | B11 |
| Cupo (0064 §1 y §12) | `RecoverAsync` ya libera el diario sin llamada. `ListReadyAsync` toma 32 y procesa de a 4 | B12: tests del mensual y tope de 4 |
| Subidas (§2 y §4) | `[FromForm]` lee el cuerpo antes del servicio, y `UploadAsync` no mira el cupo | B12, con un filtro de recurso |
| Proveedor (§6 y §7) | Hay solo `Unavailable` y `ContextTooLarge`. `AssistantChatClient` conserva el estado HTTP, pero el worker lo pasa todo a `Unavailable`. Las configuraciones no traen la falla | B13 y K1 |
| Fecha (§9) | `AssistantTurnPreparationBuilder` usa primero la zona de la organización | B14 |
| Borrar (§5) | El borrado ya limpia los archivos en la misma operación; el front dice «con sus adjuntos» | F8 y los índices de B14 |
| Anillo (§11) | No hay control del anillo ni traza en el turno | B15 |
| Front | `toastError` no vence; `ModelEditorDialog` pliega «Avanzado»; `PermissionPicker` usa áreas plegables; no hay `BroadcastChannel`; `parseDecimal` no detecta lo ambiguo | F1 a F8 |
| Lienzo v176 | Están el diseño y la IA. Los tableros de roles que la v175 cambió se redibujan en áreas plegables (260). Faltan la entrega y «Sin confirmar» (Usuarios, Empresa, Cuentas, chats y difusiones) y la (i) de Auditoría de Plataforma | F5 y F10, al final; se dibujan antes |

## Contrato

### K0 · Lo que dejó la base de la 12

El chat del contrato confirma, contra el código y la tabla de la puerta, la lista cerrada que dejó la primera parte: la puerta cerrada (I1 a I4); `INotificationSource` y la campana; en el front, `InfoTip`, `Button.disabledReason`, `RowAction`, `FilterBar` con un solo «Filtros», `PageLoading`, `FormSkeleton`, `ConfirmDialog` con `pending`, las fechas completas, la escala y la galería a 320. Lo que difiera va a «Cambios al contrato» antes de programar.

### K1 · Publicar el contrato

El contrato completo está en la spec (§Contrato): las rutas que cambian, la ruta nueva de revisión, los códigos de falla y los textos. En resumen:
- **403 nuevos** de la 0061 en roles, usuarios, miembros de empresa y operadores;
- **429 con `retryAfter`:** `Messaging.Email.DailyLimit`, en códigos e invitaciones por correo, y `WhatsApp.LoginCode.AccountDailyLimit`, en Mi cuenta;
- **campos nuevos:**
  - `invitationDelivery` en usuarios, miembros de empresa y cuentas de Plataforma;
  - `Uncertain`, `reviewedByName` y `reviewedAtUtc` en los mensajes;
  - `Uncertain` en los destinatarios de una difusión;
  - `available` en las conversaciones del asistente;
  - `providerIssue` y `unsupportedProvider` en las dos configuraciones del asistente;
- **`includes` (260):** cada permiso del catálogo trae los códigos que incluye, en los tres catálogos: organización y empresa en `GET /api/permissions`, y Plataforma en `GET /api/platform/permissions`, nueva. Va vacío hasta la B16. El back entra antes que el front o junto con él: un front nuevo con un back viejo falla al leer `includes`;
- **ruta nueva:** `POST {base}/conversations/{id}/messages/{messageId}/review` en las tres bases de WhatsApp;
- **subidas del asistente:** sus 429 y 400 antes de leer el cuerpo, y `Assistant.Conversation.Unavailable`;
- **datos de referencia:** valores nuevos en los patrones y en `format-cases.json`, y la hora de cada país para la zona horaria (262);
- **las reglas en Configuración (270 a 273):** la configuración de Plataforma suma «Privacidad y retención» (los plazos de sus datos y el máximo de los de cada organización) y «Nombre y correos»; la de una organización, el plazo de sus chats de WhatsApp. Cada valor con su mínimo, su máximo y su revisión, y el 409 de siempre;
- **el orden (264):** el `sort` de cada listado paginado del núcleo suma a `SortableFields` toda columna con un dato que muestra la pantalla; uno desconocido sigue con el 400 de siempre.

El back publica los DTO, los atributos, los códigos con sus textos y las dos rutas nuevas (la de revisión responde 501 hasta la B7), y genera `docs/contracts/openapi.json`. El front corre `npm run contracts` y no escribe tipos a mano. No hay permiso nuevo.

### Decisiones técnicas del contrato

- El estado de entrega de una invitación y el «Sin confirmar» de un mensaje se derivan al leer, desde `platform.OutboxMessages`, y no se guardan: la confirmación de la cola corre sin organización y no puede escribir en `tenant.*`.
- Los textos de WhatsApp y del asistente van en los recursos de su módulo, no en `Errors.resx` como dicen la 0064 y la 0066, por la N22.
- Los topes de invitaciones (por persona y por organización) cuentan solo los correos. Lo que va por WhatsApp tiene los límites de Meta.
- Los valores nuevos de los enums se leen y se escriben en el mismo despliegue, como excepción de la 0060: todavía no hay despliegue. La anota cada migración.
- Las 24 horas móviles se cuentan por horas: la espera tiene una hora de precisión.
- El lugar de una persona para subir va en Redis; sin Redis, vale el de la copia (0058). La cola de la copia es por proceso, como manda la 0064 §4.

**Texto para el chat de Contrato**

```text
Leé AGENTS.md, este plan y la spec docs/plans/formularios/base-de-la-12-segunda-parte.md. Hacé K0 y después K1, sin adelantar lógica: DTO, atributos, códigos de error con sus textos es/en (núcleo en Errors.resx; WhatsApp y asistente en los recursos de su módulo) y la ruta de revisión con 501. No toques entidades, configuraciones de EF ni migraciones. Generá openapi.json y avisá a la Guía para que el front corra npm run contracts. Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo tuyo con git commit -- <rutas>. Aplicá api-http, permisos, result-y-errores, textos y tests. Al terminar: build y tests rápidos enteros del back, sin filtrar por módulo; como cambian rutas, los de integración y seguridad de Contracts.
```

## Back

Los pasos, con sus archivos, lo que copian y sus tests, están en la spec (§Pasos). Valen Result y ProblemDetails, `OperationLog.RunAsync`, `LoggerMessage`, `TimeProvider`, una UoW por escritura, RLS, auditoría y textos es/en. WhatsApp y el asistente siguen siendo quitables: el núcleo no los nombra.

### Back A · núcleo y WhatsApp

1. **B16 · Un permiso incluye a otro (R: permisos y datos).** Va primera, porque la B1 compara con lo efectivo. `Includes` en Domain, en cada catálogo, que el catálogo valida al componerse, y `includes` en el catálogo de la API. Una sola función de lo efectivo (los roles más lo que incluyen) para `PermissionService`, `PlatformPermissionService` y las guardas de la B1; la caché guarda lo efectivo. Al guardar un rol o un operador se guarda lo pedido más lo incluido. Los roles que ya creó cada organización se completan una vez, recorriendo las organizaciones, y lo efectivo queda como red durante un despliegue. Tests de lo efectivo, guardar y completar un rol viejo; se corrigen los 403 que pedían «Administrar» sin «Ver».
2. **B1 · Nadie da lo que no tiene (R: permisos).** Tres guardas por alcance, usadas dentro del límite por roles, usuarios, miembros de empresa y operadores. `DelegatedAdministrationTests` en los tres alcances.
3. **B2 · Retención del ingreso (R: datos y aislamiento).** `IdentityRetentionWorker` (global) e `InvitationRetentionWorker` (con `TenantJobRunner`), por lotes y con sus índices. `PersonalDataPurgeTests` exige la purga o el motivo de cada tabla.
4. **B3 · Topes de correo y de códigos (R: datos y cuotas por alcance).** `EmailQuotaOptions`, `EmailQuotaGuard` y `identity.MessageQuotaBuckets`, más los 10 diarios por cuenta de WhatsApp y la IPv6 por su /64. El E2E y el recorrido corren con topes altos en su configuración.
5. **B5 · La cola no repite lo dudoso (R: datos y efectos fuera).** Suma `Uncertain`, la reserva por destino, la prioridad y lo que vence en la cola. El SMTP clasifica sus fallas, un lease vencido queda «Sin confirmar» y la entrega cabe en el apagado. Hay chequeo `email` y contadores sin organización.
6. **B6 · Estado de entrega de la invitación (R, con B5).** El `OutboxMessageId` va en cada invitación, también la de un operador, y los tres readers derivan `invitationDelivery`.
7. **B7 · WhatsApp «Sin confirmar» (R: efectos fuera y permisos).** El transporte clasifica sus fallas y reintenta dos veces a los 30 s. Van también los ocho códigos, el orden por chat, la prioridad sobre las difusiones, `Uncertain` al leer y la revisión con su ruta en las tres bases.
8. **B8 · El chat cifrado (R: cifrado y aislamiento).** Va con un conversor de EF por organización, la migración que vacía el contenido, la lectura vacía de lo que no abre y los tests de aislamiento. Suma también las líneas del runbook.
9. **B4 · El bloqueo no se usa contra otro.** Google no frena por códigos fallidos, y con la cuenta bloqueada un código correcto dice `LockedOut`.
10. **B9 · Lo transversal.** Lleva la resiliencia sin POST y el scope del log con la consola en JSON. Suma las guardas de textos neutrales y de etiquetas de métricas, y el test que compara el modelo de EF con su snapshot (traspaso 18, 04). Los datos de la instalación salen del repo, con `StartupConfigurationTests` invertido y la configuración al día.
11. **B18 · Reglas en Configuración (R: datos y aislamiento, con migración).** Va después de la B9. El inventario de los valores de negocio del núcleo y de los módulos programados que siguen en `appsettings` o fijos (270); la retención en pantalla, con la purga de los chats de WhatsApp de cada organización por `TenantJobRunner` y los plazos de la B2 leídos de la configuración (272); el cupo diario de la IA por organización y a su medianoche, también con la clave de la plataforma (271); y «Nombre y correos», que deja `Email:AppName` y `FromName` con la regla de las dos versiones (273).

**Texto para el chat del Back A**

```text
Leé AGENTS.md, este plan, la spec docs/plans/formularios/base-de-la-12-segunda-parte.md y las ADR 0060 a 0063, 0065 y 0066; las fichas permisos, multitenancy, concurrencia, cuotas, guardado, persistencia-ef, datos-personales, logs, api-http, result-y-errores, textos y tests, y las features identidad, organizaciones, roles y whatsapp; para la B16, la decisión 260 y el traspaso 16, 01, de ArquitecturaBase (git show origin/main:docs/plans/2026-11-21-traspaso-multitenant/01-roles-y-permisos.md). Después de K1, hacé en este orden B16, B1, B2, B3, B5, B6, B7, B8, B4, B9 y B18, con los pasos de la spec; para la B18, las decisiones 270 a 273. Avisale a la Guía cuando commitees la B8: desde ahí Back B toca el modelo, y vos no hacés más migraciones. Las tareas marcadas (R) llevan la revisión adversarial completa antes del commit: un subagente nuevo recibe el código y los escenarios, sin tu conclusión, e intenta romperla. No toques el front ni las tareas de Back B. Cualquier cambio de contrato imprescindible va a «Cambios al contrato» del plan. Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo tuyo con git commit -- <rutas>. Por tarea: test dirigido rojo y verde, build y tests rápidos enteros del back sin Docker y sin filtrar por módulo; si tocó rutas, persistencia, autenticación o aislamiento, sus tests de integración y de seguridad. Nunca pegues ni copies claves: los datos de la instalación los carga el usuario en I1. Ninguna corrida contra Meta real.
```

### Back B · formatos e IA

1. **B10 · Fechas y porcentaje.** `cultures.source.json` con `generar.mjs`, `format-cases.json` y `DisplayFormatter.FormatMonthRange`, que reemplaza a `FormatMonth` y `FormatMonthLabel`. `FormatPercent` y el correo del tope se ponen al día.
2. **B11 · La CUIT nunca once ceros.** `ArgentineCuitValidator`, su test y la línea de la ficha.
3. **B12 · El cupo y las subidas (R: aislamiento, por la cola por organización).** Tests de lo que vuelve y lo que no, y `ListReadyAsync` que toma 4. `AssistantUploadSlotAttribute` corre antes de enlazar el archivo: cupo, lugar de la persona, cola de la copia y 120 s.
4. **B13 · Las fallas del proveedor (R: efectos fuera, IA).** Lleva el clasificador por estado HTTP, la clave ilegible sin gastar cupo y `providerIssue` según quién paga. Un proveedor desconocido deja la conexión en pausa. *Después del commit de la B8.*
5. **B14 · Versiones, borrado y fecha (R: datos, migración).** Lleva `EngineVersion`, la conversación ilegible, los índices parciales y la zona del perfil con la hora al final del mensaje. Suma el test de que un pedido no lleva nada de otra persona (216).
6. **B15 · El anillo y la traza (R: cifrado).** Lleva `platform.AiKeyRingControls` y el chequeo `ai-keyring`: una copia que no coincide no toma turnos ni entradas de WhatsApp. El turno guarda `TraceParent`.
7. **B17 · El orden de los listados (264).** `ApplySort` deja lo vacío al final en los dos sentidos y ordena un estado por su circuito, nunca por su texto; cada `SortMap` del núcleo suma las columnas con un dato que muestra su pantalla, con su índice (`SortIndexTests`). Los listados de cursor (Auditoría y los chats) conservan su orden. Sus índices van en una migración después del commit de la B8. Test por listado: cada columna, lo vacío al final y el desempate.

**Texto para el chat del Back B**

```text
Leé AGENTS.md, este plan, la spec docs/plans/formularios/base-de-la-12-segunda-parte.md, la ADR 0064, la feature asistente y las fichas fechas-y-zonas, numeros-y-moneda, datos-de-referencia, identificacion-fiscal, cuotas, cache, concurrencia, persistencia-ef, logs, textos y tests, y para la B17 paginado-y-busqueda y la decisión 264. Después de K1, hacé B10, B11 y B12, que no tocan entidades, configuraciones de EF ni migraciones. B13, B14, B15 y B17 van recién cuando la Guía confirme el commit de la B8 de Back A: los dos comparten el ModelSnapshot. Las tareas marcadas (R) llevan la revisión adversarial completa antes del commit, con un subagente nuevo que no recibe tu conclusión. Los datos de referencia se cambian en la fuente y se regeneran, nunca a mano en el JSON generado. No toques el front ni las tareas de Back A. Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo tuyo con git commit -- <rutas>. Por tarea: test dirigido, build y tests rápidos enteros del back sin Docker y sin filtrar por módulo; si tocó rutas, persistencia o aislamiento, sus tests de integración y de seguridad. Ninguna corrida contra un proveedor de IA real: usá el cliente falso.
```

## Front

Las tareas siguen el lienzo v176 y las fichas del front (`formatos.md`, `pantallas-y-ui.md`, `tema.md`, `accesibilidad.md`, `formularios.md` y `paginado-y-listados.md`), con i18n, tipos generados y axe donde cambia una pantalla. Los pasos están en la spec.

1. **F1 · Fechas y números (205, 207 y 208):** la coma, el mes como rango, el porcentaje pegado, `numberAmbiguous` y `MonthField`.
2. **F2 · Avisos (210):** el `Toaster`, `FormError`, el cartel que bloquea y las franjas, con el filo y el ícono. El error se va a los 10 s y uno con acción queda.
3. **F3 · Lo apagado y la (i) (211):** clic, toque, Enter y Espacio sin hacer la acción, y el control entero responde. En el teléfono, el tooltip.
4. **F4 · Un dato por columna, nada debajo y el ⋮ (206, 209, 261 y 263):** la revisión de las columnas, las filas del teléfono y los encabezados del núcleo, sin el texto secundario de `DataTable`, que la 263 sacó; ninguna fila con un botón con texto.
5. **F5 · Roles en áreas plegables (260):** `PermissionPicker` y `RoleEditorPage` conservan su forma, con los detalles de la RO3-D: toda área con su casilla, se abren las que tienen algo, sin cuentas ni buscador, el Dueño leído con tildes y filas de 44 px en el teléfono. Lo incluido se ve marcado y apagado, con su motivo. Va en la organización, con alcance de empresa, y en los permisos de un operador (`OperatorPermissionsDialog`, `InviteOperatorDialog` y `AccountPermissionsTab`). El selector de roles de una persona apaga, con su motivo, lo que quien edita no puede dar (269 y 0061). Espera sus tableros.
6. **F6 · Las piezas (213):** el título de grupo con (i), el chip de solo lectura, las teclas con el menú enfocado, el desplegable de 48 y las confirmaciones en una frase.
7. **F7 · Sesión, conteos, topes y CUIT:** `BroadcastChannel` (N12), los conteos que se muestran (N30), la espera en horas y minutos, los textos de los 403 y 429 nuevos y los once ceros.
8. **F8 · El asistente (0064 y 254):** borrar con sus archivos, las subidas, la conversación no disponible, descartar lo desconocido, las fallas en las dos configuraciones y el diálogo del modelo en dos pasos.
9. **F9 · Los motivos de WhatsApp:** los ocho códigos en la conversación y en las difusiones.
10. **F10 · «Sin confirmar» y la entrega.** Va al final y espera sus tableros. Lleva `invitationDelivery` en Usuarios, Usuario, Empresa y Cuentas; la revisión en la conversación de la organización y de Plataforma; «Sin confirmar» en una difusión, y la (i) de Auditoría de Plataforma.
11. **F11 · Una sola pieza para elegir (262).** `Picker` en `shared/ui`, que reemplaza a `SearchSelect`, `MultiSelect`, `CountrySelect`, `TimeZoneSelect`, `CultureSelect`, los radios y el segmentado de `RadioGroupField` y los desplegables de `FilterBar`, en todas las pantallas; los segmentados de filtro pasan a un filtro sin cantidad, y la zona horaria dice la hora de su país. Suma la regla del arnés que impide otra pieza para elegir. Espera su tablero.
12. **F12 · Ordenar por las columnas (264).** El encabezado de `DataTable` con ↑ ↓ y ↕, y «Ordenar por» al final de `FilterBar`, con el orden en la dirección; contra la B17. Espera su tablero.
13. **F13 · Las reglas en Configuración (270 a 273).** «Privacidad y retención» y «Nombre y correos», con «Así llega», en Plataforma; el plazo de los chats en la Configuración de WhatsApp de la organización; lo que ven la persona y el operador cuando se agota un cupo de la IA. Contra la B18. Espera sus tableros.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, este plan y la spec docs/plans/formularios/base-de-la-12-segunda-parte.md, con las decisiones 205 a 211, 213, 254, 260 a 264 y 269 a 273 de la hoja de ruta, las fichas del front y los tableros del lienzo v176 que nombra la spec. Esperá K1, corré npm run contracts y hacé F1 a F4 y F6 a F9 en orden, contra los tipos generados. F5 y F10 a F13 van al final y arrancan cuando la Guía confirma sus tableros aprobados en el lienzo; nunca dibujes ni inventes esas pantallas. No toques el lienzo, las specs ni el back, ni el src si Vite está levantado. Usá i18n, shared/format, shared/ui, TanStack Query y las reglas de accesibilidad; no escribas tipos generados. Todo en main, sin push, ramas, worktrees, stash ni reset; commiteá solo lo tuyo con git commit -- <rutas>. Por tarea: tests focales, npx tsc -b y npm test completos, sin filtrar por módulo; al cerrar la parte, build, lint y contracts:check.
```

## Integración y puerta

1. **I1 · Integrar.** El front consume el contrato real y regenera los tipos; una diferencia vuelve a su dueño y se anota abajo. El usuario carga en sus user-secrets los datos de la instalación que sacó B9 (el `ClientId` de Google, el usuario y el remitente de Gmail, y los tres ids de Meta). Lo hace con los pasos que le da el chat, comando por comando: ningún agente los copia.
2. **I2 · Puerta.** Se corre `/puerta` una vez, sobre copias del corte:
   - `dotnet build` sin advertencias y `dotnet test` con Docker;
   - front build, lint, test y `contracts:check`;
   - el E2E real;
   - `realinear-docs --detectar` y `estado-docs --detectar` en 0;
   - la galería de lo cambiado a 1440, 390 y 320;
   - `/recorrido`.

   No hay prueba con Meta ni retiro de módulo: lo que pide un número conectado queda para la I1 de la E10 (123), y la composición no cambia.
3. **I3 · Recorrido.** El recorrido cubre:
   - un rol con un permiso ajeno, que da 403 con su texto;
   - invitar hasta el tope, con la espera en horas;
   - una invitación por correo en cada estado de entrega;
   - un mensaje «Sin confirmar» revisado, en la organización y en Plataforma;
   - el chat del Inicio sin cupo y con dos subidas a la vez;
   - la clave ilegible en las dos configuraciones;
   - un modelo nuevo en dos pasos;
   - una fecha, un porcentaje y un mes como rango;
   - un aviso de cada tipo;
   - un rol en áreas plegables, con lo incluido apagado y su motivo, en la compu y en el teléfono;
   - elegir con la pieza una opción, varias y la zona horaria, y un filtro que era un segmentado;
   - ordenar una tabla desde su encabezado y desde «Ordenar por», con lo vacío al final;
   - cambiar el plazo de los chats de una organización y el nombre de los correos, y ver el correo de un código con el nombre nuevo;
   - salir con dos pestañas abiertas.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan, la spec y la skill /puerta. Integrá contra K1, devolvé cada diferencia real a su dueño y anotala en «Cambios al contrato». Dale al usuario los pasos de I1, comando por comando, para cargar sus user-secrets: no copies ni pegues ningún valor. Corré la puerta una sola vez sobre copias del corte; un arreglo corre solo lo que toca. No hagas push ni toques el lienzo. Dejá en este plan solo la tabla final corta.
```

## Relación con otras etapas

**Usa de las anteriores:** de la base de la 12, la campana, `InfoTip`, `RowAction.disabledReason`, `FilterBar`, `PageLoading`, `ConfirmDialog` con `pending`, las fechas completas y la galería a 320 (K0). De la E11, `WhatsAppCloudClient`, `WhatsAppConversationReader`, `WhatsAppBroadcastReader` y los chats de las tres bases. De la E8b, el asistente del Inicio, `AssistantChatClient` y `AssistantTurnWorker`.

**Deja para las siguientes:**
- **la E12 y todo el bloque:** `Picker`, el orden de `DataTable` con «Ordenar por» y nada debajo de otro dato (262 a 264); las fechas con coma, `FormatMonthRange`, el porcentaje pegado y `numberAmbiguous`, para importar un catálogo (C9) y para el Excel. También los avisos, la (i) y lo apagado, `DataTable` con texto secundario y las piezas de la 213. Y los roles en áreas plegables con `Includes`, donde cada etapa declara lo que incluyen sus permisos (260);
- **la E14 y la E21:** `OutboxStatus.Uncertain`, `OrderingKey`, `Priority`, la revisión por id del envío, los ocho códigos y el conversor del contenido del chat, para que su contenido nazca cifrado (0065 §1);
- **la E23 y la E25:** el clasificador de fallas del proveedor, `providerIssue`, el lugar de subida, `EngineVersion`, el anillo controlado y la traza, que aplican al canal, la transcripción, los documentos y armar;
- **la E10:** la reserva por destino ya hecha, y su B6 la prueba con dos hosts. También el chequeo `ai-keyring` y los workers de purga, que reciben el lease de `PeriodicWorker`.

**Queda afuera:**
- el exportador OTLP y la alerta de 5xx (E10);
- el apagado del asistente, con `Interrupted` (0064 §8, B6 de la E10), y las dos versiones de WhatsApp (B6 de la E10);
- el número ambiguo por WhatsApp (E14, T9) y la respuesta de Flow (E14);
- el canal, la transcripción, los documentos, armar, `ProposalTooLarge`, los topes de lectura y los hechos del negocio (E23 y E25);
- las capacidades `forms.*` (E23 y E25);
- borrar con el ETag (E10 y E18);
- la guía de quitar WhatsApp con sus tablas nuevas (E14 a E25);
- la retención de las respuestas de formularios (antes de producción).

## Cambios al contrato

## Para decidir

1. **Una difusión con un destinatario dudoso:** «Sin confirmar», sin revisión, y no cuenta como enviado ni como «No llegó» (255, decidida por el usuario el 2026-11-19). La dibuja `WA-Difusion-Resultados`.
2. **La parte del Gmail compartido con ArquitecturaBase** (0063 §2): un tope global de 80 correos por día, con 20 reservados para invitaciones, hasta separar la cuenta antes de producción; los demás topes, con los valores de la 0063 (256, decidida por el usuario el 2026-11-19). Van como valores de la configuración.
