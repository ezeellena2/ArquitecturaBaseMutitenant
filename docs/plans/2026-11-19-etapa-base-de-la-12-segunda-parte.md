# Base de la 12, segunda parte · plan ejecutable

**Estado:** cerrado · puerta del 2026-11-26; sigue el recorrido de los pasos B12B-12 a 15, y Acciones y Costuras arrancan sin esperarlo (decisión del usuario de ese día) · Spec: `docs/plans/formularios/base-de-la-12-segunda-parte.md`. Va después de la puerta de la base de la 12 y antes de la E12 (decisión 214). Tableros: lienzo v176, y los de la F4 (263), la F5 (260 y 269), la F10, la F11 y la F12 (262 y 264) y la F13 (270 a 273), aprobados en el lienzo v181, en la página «Segunda parte de la base»: cada `-Base2` manda sobre su tablero de siempre en lo que dibuja. Sin decisiones pendientes: las de producto las tomó el usuario (255, 256, 260 a 264 y 270 a 273), y la 269 se adoptó sin pregunta.

**Fuente:** del [backlog](../backlog.md), «Traspaso 15, el núcleo: para programar», «Traspaso 15, diseño, fechas y números: para la segunda parte de la base», «Traspaso 15, la IA: para la segunda parte de la base», «Traspaso 15, WhatsApp y ARCA: para programar» (WhatsApp y la CUIT), «Traspasos 16 y 17: para la segunda parte de la base» y «Traspaso 18, tanda 1: para la segunda parte de la base». Lo que mandan a otra etapa está en «Queda afuera».

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Hecha |
| Back A · núcleo y WhatsApp | B16, B1–B9 y B18 | Hecha |
| Back B · formatos e IA | B10–B15 y B17 | Hecha |
| Front | F1–F13 | Hecha |
| Integración y puerta | I1–I3 | Hecha; sigue el recorrido de B12B-12 a 15 |

## Forma de trabajo

Modo rápido ([etapa en paralelo](../guides/etapa-en-paralelo.md)): primero el contrato; después, a la vez, dos chats del back y uno del front; una sola puerta al final. Cada tarea lleva su test dirigido, y el build y los tests rápidos enteros de su repo, sin Docker ni filtros; si toca rutas, persistencia, autenticación o aislamiento, también los de integración y seguridad de su área. Un arreglo corre solo lo que toca.

Todo va en `main`, sin ramas, worktrees, stash, reset ni push. Cada chat commitea solo sus rutas con `git commit -- <rutas>`, con la línea `Tarea: base-de-la-12-segunda-parte/<id>`. Con Vite levantado nadie edita el `src` del front. La tabla de estado la actualiza la Guía.

## Contrato

### K0 · Lo que dejó la base de la 12

El chat del contrato confirma, contra el código y la tabla de la puerta, la lista cerrada que dejó la primera parte: la puerta cerrada (I1 a I4); `INotificationSource` y la campana; en el front, `InfoTip`, `Button.disabledReason`, `RowAction`, `FilterBar` con un solo «Filtros», `PageLoading`, `FormSkeleton`, `ConfirmDialog` con `pending`, las fechas completas, la escala y la galería a 320. Lo que difiera va a «Cambios al contrato» antes de programar.

### K1 · Publicar el contrato

El contrato completo está en la spec (§Contrato): los 403 de la 0061, los 429 con `retryAfter`, los campos nuevos (`invitationDelivery`, `Uncertain` y la revisión, `available`, `providerIssue`, `includes` y `grantable`), las rutas nuevas (el catálogo de Plataforma, la revisión de WhatsApp y las dos configuraciones de WhatsApp), los campos de la configuración de Plataforma y de Auditoría, las subidas del asistente, los datos de referencia y el `sort` de los listados. `includes` va vacío hasta la B16, y `grantable`, en `true` hasta la B1. El back entra antes que el front o junto con él: un front nuevo con un back viejo falla al leer `includes`.

El back publica los DTO, los atributos, los códigos con sus textos y las rutas nuevas (la de revisión responde 501 hasta la B7, y las de la configuración de WhatsApp, hasta la B18), y genera `docs/contracts/openapi.json`. El front corre `npm run contracts` y no escribe tipos a mano. No hay permiso nuevo.

### Decisiones técnicas del contrato

- El estado de entrega de una invitación y el «Sin confirmar» de un mensaje se derivan al leer, desde `platform.OutboxMessages`, y no se guardan: la confirmación de la cola corre sin organización y no puede escribir en `tenant.*`.
- Los textos de WhatsApp y del asistente van en los recursos de su módulo, no en `Errors.resx` como dicen la 0064 y la 0066, por la N22.
- Los topes de invitaciones (por persona y por organización) cuentan solo los correos. Lo que va por WhatsApp tiene los límites de Meta.
- Los valores nuevos de los enums se leen y se escriben en el mismo despliegue, como excepción de la 0060: todavía no hay despliegue. La anota cada migración.
- Las 24 horas móviles se cuentan por horas: la espera tiene una hora de precisión.
- El lugar de una persona para subir va en Redis; sin Redis, vale el de la copia (0058). La cola de la copia es por proceso, como manda la 0064 §4.
- `grantable` lo calcula la `Policy` de la B1, la misma de la guarda: el front no repite la regla.
- El cupo de la IA (271) va en la B14 de Back B, dueño del asistente; la B18 de Back A no lo toca.

## Back

Los pasos están en la spec. Valen Result y ProblemDetails, `OperationLog.RunAsync`, `LoggerMessage`, `TimeProvider`, una UoW por escritura, RLS, auditoría y textos es/en. WhatsApp y el asistente siguen siendo quitables: el núcleo no los nombra.

### Back A · núcleo y WhatsApp

1. **B16 · Un permiso incluye a otro (R: permisos y datos).** Va primera, porque la B1 compara con lo efectivo: `Includes` en cada catálogo, una sola función de lo efectivo, también en la caché, guardar lo pedido más lo incluido y completar una vez los roles que ya existen. Se corrigen los 403 que pedían «Administrar» sin «Ver».
2. **B1 · Nadie da lo que no tiene (R: permisos).** Tres guardas por alcance, cada una con su `Policy` pura, que calcula también `grantable`, usadas dentro del límite por roles, usuarios, miembros de empresa y operadores. `DelegatedAdministrationTests` en los tres alcances.
3. **B2 · Retención del ingreso (R: datos y aislamiento).** Las dos purgas, por lotes, y `PersonalDataPurgeTests`.
4. **B3 · Topes de correo y de códigos (R: datos y cuotas por alcance).** Los topes de la 0063 y la 256, los 10 diarios por cuenta de WhatsApp y la IPv6 por su /64. El E2E y el recorrido corren con topes altos en su configuración.
5. **B5 · La cola no repite lo dudoso (R: datos y efectos fuera).** `Uncertain`, la reserva por destino, la prioridad, lo que vence y el SMTP que clasifica sus fallas.
6. **B6 · Estado de entrega de la invitación (R, con B5).** `OutboxMessageId` en cada invitación e `invitationDelivery` al leer.
7. **B7 · WhatsApp «Sin confirmar» (R: efectos fuera y permisos).** El transporte clasifica sus fallas; los ocho códigos, el orden por chat, la prioridad y la revisión en las tres bases.
8. **B8 · El chat cifrado (R: cifrado y aislamiento).** El conversor de EF por organización y la migración que vacía el contenido.
9. **B4 · El bloqueo no se usa contra otro.** Google no frena por códigos fallidos, y con la cuenta bloqueada un código correcto dice `LockedOut`.
10. **B9 · Lo transversal.** La resiliencia sin POST, el scope del log, las guardas de textos neutrales y de métricas, `ModelSnapshotTests` (traspaso 18, 04) y los datos de la instalación fuera del repo.
11. **B18 · Reglas en Configuración (R: datos y aislamiento, con migración).** Va después de la B9: el inventario (270), cada plazo en la configuración de su dueño con la purga de los chats de WhatsApp (272), «Nombre y correos» (273) y los países que reciben códigos (270). El cupo de la IA (271) va en la B14.

### Back B · formatos e IA

1. **B10 · Fechas y porcentaje.** Los patrones, `format-cases.json` y `FormatMonthRange`.
2. **B11 · La CUIT nunca once ceros.** `ArgentineCuitValidator`, su test y la línea de la ficha.
3. **B12 · El cupo y las subidas (R: aislamiento, por la cola por organización).** Lo que vuelve al cupo y `AssistantUploadSlotAttribute` antes de enlazar el archivo.
4. **B13 · Las fallas del proveedor (R: efectos fuera, IA).** El clasificador, la clave ilegible, `providerIssue` y el proveedor desconocido. *Después del commit de la B8.*
5. **B14 · Versiones, borrado, fecha y el cupo del día (R: datos, migración).** `EngineVersion`, la conversación ilegible, la zona del perfil y el cupo del día de la 271, por organización y a su medianoche.
6. **B15 · El anillo y la traza (R: cifrado).** El chequeo `ai-keyring` y `TraceParent`.
7. **B17 · El orden de los listados (264).** `ApplySort` y cada `SortMap` del núcleo, con sus índices en una migración después del commit de la B8. Los listados de cursor conservan su orden.

## Front

Las tareas siguen el lienzo v176 y las fichas del front (`formatos.md`, `pantallas-y-ui.md`, `docs/architecture/tema.md`, `accesibilidad.md`, `formularios.md` y `paginado-y-listados.md`), con i18n, tipos generados y axe donde cambia una pantalla. Los pasos están en la spec.

1. **F1 · Fechas y números (205, 207 y 208):** la coma, el mes como rango, el porcentaje pegado, `numberAmbiguous` y `MonthField`.
2. **F2 · Avisos (210):** blancos con filo e ícono; el error se va a los 10 s y uno con acción queda.
3. **F3 · Lo apagado y la (i) (211):** se abren sin hacer la acción, también en el teléfono.
4. **F4 · Un dato por columna, nada debajo y el ⋮ (206, 209, 261 y 263):** la revisión de las columnas, las filas del teléfono y los encabezados del núcleo, sin el texto secundario de `DataTable`, que la 263 sacó; ninguna fila con un botón con texto. Con sus tableros `-Base2` de la v181.
5. **F5 · Roles en áreas plegables (260):** `PermissionPicker` y `RoleEditorPage` con los detalles de la RO3-D y lo incluido apagado con su motivo, en la organización, con alcance de empresa, y en los permisos de un operador. El selector de roles de una persona y el editor de un rol apagan, con su motivo, lo que quien edita no puede dar, según `grantable` (269 y 0061). Con sus tableros `-Base2` de la v181.
6. **F6 · Las piezas (213):** el título de grupo con (i), el chip de solo lectura, las teclas con el menú enfocado, el desplegable de 48 y las confirmaciones en una frase.
7. **F7 · Sesión, conteos, topes y CUIT:** `BroadcastChannel` (N12), los conteos que se muestran (N30), la espera en horas y minutos, los textos de los 403 y 429 nuevos y los once ceros.
8. **F8 · El asistente (0064 y 254):** borrar con sus archivos, las subidas, la conversación no disponible, descartar lo desconocido, las fallas en las dos configuraciones y el diálogo del modelo en dos pasos.
9. **F9 · Los motivos de WhatsApp:** los ocho códigos en la conversación y en las difusiones.
10. **F10 · «Sin confirmar» y la entrega.** `invitationDelivery`, la revisión en las conversaciones, «Sin confirmar» en una difusión y la (i) de Auditoría de Plataforma con sus meses (272). Con sus tableros `-Base2` de la v181.
11. **F11 · Una sola pieza para elegir (262).** `Picker` en `shared/ui`, que reemplaza en todas las pantallas a las piezas viejas que nombra la spec; los segmentados de filtro pasan a un filtro sin cantidad, y la zona horaria dice la hora de su país. Suma la regla del arnés que impide otra pieza para elegir, el foco al cerrarse y la guarda de elegir lo ya elegido. Con su tablero `-Base2` de la v181.
12. **F12 · Ordenar por las columnas (264).** El encabezado de `DataTable` con ↑ ↓ y ↕, y «Ordenar por» al final de `FilterBar`, con el orden en la dirección; contra la B17. Con su tablero `-Base2` de la v181.
13. **F13 · Las reglas en Configuración (270 a 273).** «Privacidad y retención», «Nombre y correos» y «Países que reciben códigos» en Plataforma, el plazo de los chats en la de WhatsApp de la organización y el cupo agotado de la IA. Contra la B18 y la B14. Con sus tableros `-Base2` de la v181.

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
| Back | `dotnet build` sin advertencias y `dotnet test` con Docker, 3932 de 3932. Los arreglos de la puerta corrieron sus tests y los de su área: el 429 (`587dee22`), «Sin confirmar» de la difusión (`cec9e99b`, integración y seguridad de WhatsApp 28 de 28) y el preparador del recorrido (`90f8e45c`) |
| Front | build, lint sin errores, `npm test` 3833 de 3833 y `contracts:check`; con cada arreglo de la puerta, la suite entera otra vez |
| E2E real | 47 casos: 36 en la primera corrida, y los 11 que fallaron pasan (el E2E viejo en 5b, 5d y 6, el entorno, y «Reintentar» en la falla incierta, `d8950356`). El asistente E8b, otra vez con el corte final: 42 hitos |
| Documentación | `realinear-docs`, `estado-docs` y `conformidad` en 0 |
| Galería | 344 pantallas a 1440, 390 y 320 al lado de su tablero (`a4caeb44`); las 23 diferencias de la app, arregladas en el front, y sus casos otra vez (`5de7c993` y `f33042d0`, 55 de 55) |
| Recorrido | 246 de 246 con el front `eee6391f`, con la I3 como once pasos (B12B-01 a 11). Con el corte final, 123 de 127 a 1440: los cuatro pasos que pasan a bot lo que la I3 probaba a mano (B12B-12 a 15) esperan un arreglo del preparador, no de la app, y a mano pasaron. Sigue la corrida a 1440 y 390 |

## Relación con otras etapas

**Usa de las anteriores:** de la base de la 12, lo que confirma la K0. De la E11, `WhatsAppCloudClient`, `WhatsAppConversationReader`, `WhatsAppBroadcastReader` y los chats de las tres bases. De la E8b, el asistente del Inicio, `AssistantChatClient` y `AssistantTurnWorker`.

**Deja para las siguientes:**
- **la E12 y todo el bloque:** `Picker`, el orden de `DataTable` con «Ordenar por» y nada debajo de otro dato (262 a 264); las fechas con coma, `FormatMonthRange`, el porcentaje pegado y `numberAmbiguous`, para importar un catálogo (C9) y para el Excel. También los avisos, la (i) y lo apagado, `DataTable` sin texto secundario (263) y las piezas de la 213. Y los roles en áreas plegables con `Includes`, donde cada etapa declara lo que incluyen sus permisos (260);
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

- `grantable` también sale en `GET /api/roles/{id}`: el listado y el detalle comparten `RoleResponse`. (K1)
- `GET /api/platform/audit` responde `PlatformAuditListResponse` (`items` con `maxItems` 100, `nextCursor`, `hasMore` y `loginAuditRetentionMonths`) en lugar de `CursorResultOfPlatformAuditEventResponse`; el front cambia el nombre del tipo generado. (K1)
- La hora del país de cada zona horaria se llama `countryTimeName` («hora de Argentina» o «Argentina time»), puede venir en `null` y ya viene calculada desde K1. (K1)
- En `PUT /api/platform/settings` los seis campos nuevos (los tres plazos, `appName`, `senderName` y `emailLogoUrl`) son opcionales: si faltan, se conserva lo guardado. Se validan con sus cotas, pero no se guardan hasta la B18. El GET devuelve 12/30/30, el nombre y el remitente de la configuración de arranque, y `emailLogoUrl` en `null`. (K1)
- La configuración de WhatsApp de la organización trae `{chatRetentionDays, version}` y la de Plataforma `{allowedCountries, chatRetentionDays, maxOrganizationChatRetentionDays, version}`. Los dos PUT responden `VersionResponse` y no llevan `reason`. (K1)
- `failureReason` sigue siendo el texto traducido del motivo; el código guardado es uno de los diez de `WhatsAppSendFailures`. Se sumaron los textos `Send.Failure.NotAllowed`, `WindowClosed`, `TokenInvalid`, `PermissionMissing`, `Unavailable` y `ReadOnly`. (K1)
- `Uncertain` se suma al final de los enums `WhatsAppChatMessageStatus` y `WhatsAppRecipientStatus` del dominio; se deriva al leer y no se guarda. (K1)
- `Assistant.Attachment.UploadTimedOut` es un 400 sin campo. Las fallas del proveedor son constantes de `AssistantErrors` con su texto, y la descripción del `code` del evento `error` y la de `providerIssue` los enumeran. (K1)
- `docs/contracts/format-cases.json`: `date-time` es «27/09/2026, 14:35» y «09/27/2026, 2:35 PM»; `percent` es «12,5%» y «0,12%» en es-AR. (B10)
- `docs/contracts/format-cases.json`: sale el tipo `dateLong` (casos `date-long-*`) y entra el tipo `month`: entrada «yyyy-MM» y como salida el rango como rótulo («01/10/2026 – 31/10/2026», «10/01/2026 – 10/31/2026»), con casos `month-*` y `month-leap-february-*` para es-AR y en-US. El front lo arma con `formatMonth`. (B10)
- `GET /api/reference-data`: cambian solo valores (`DateTimePattern` con coma en es-AR y en-US, `PercentPattern` de es-AR «{number}%»); `LongDatePattern` sigue en la respuesta sin uso. (B10)
- GET /conversations/{id} (consumer, business y platform) declara ProducesProblem(409) con Assistant.Conversation.Unavailable. La respuesta ya estaba documentada, pero fuera de orden; ahora está bien declarada. (B14)
- GET /conversations/{id}/export suma 409 Assistant.Conversation.Unavailable. (B14)
- POST /conversations/{id}/messages suma Assistant.Conversation.Unavailable a su 409, antes de reservar el cupo. (B14)
- La descripción de la conversación ilegible en el contrato ahora dice que su GET y su exportación son 409, que un mensaje nuevo también, y que el DELETE la borra. docs/contracts/openapi.json se regeneró con el build. (B14)
- Todo listado paginado del núcleo: lo vacío va al final en los dos sentidos, un estado o un tipo ordena por su circuito (el orden de declaración del enum, igual en los dos idiomas) y, con el mismo valor, sigue el orden de siempre de la lista y después el Id. OpenAPI no cambia: sort sigue siendo texto. (B17)
- GET de roles paginado: sort admite type, scope, userCount y permissions (los permisos que valen en el alcance del rol, los que muestra la fila). (B17)
- GET de empresas paginado: sort admite taxId, timeZoneId, status y memberCount (miembros activos). (B17)
- GET de personas de la organización paginado: sort admite state, invitationDelivery (por su circuito: Pending, Sent, Uncertain, Failed) e invitationExpiresAtUtc; las dos últimas quedan vacías, y van al final, fuera de una invitación. (B17)
- GET de miembros de una empresa paginado: sort admite state, invitationDelivery e invitationExpiresAtUtc, estas dos solo para la invitación que incluye a esa empresa; si no, vacías y al final. (B17)
- GET de organizaciones de Plataforma paginado: sort admite status, ownerName (el nombre del Dueño o, sin nombre, su correo; vacío al final), companyCount y memberCount. (B17)
- GET de cuentas de Plataforma paginado: sort admite contact (teléfono principal o correo), lastSignInAtUtc (vacío en una invitación), state (por su circuito) e invitationDelivery. (B17)
- GET de recuperaciones de Plataforma paginado: sort admite createdAtUtc (el id de su columna en el front), status y accountName (sin cuenta o sin nombre, al final); createdAt sigue valiendo mientras conviven las dos versiones. (B17)
- GET /api/organization/whatsapp/contacts: nuevo query sort (name, number, consent, lastConversationAtUtc; consent por su circuito). Sin sort, por nombre. (B17)
- GET /api/organization/whatsapp/broadcasts: nuevo query sort (name, template, recipients, status, delivered, read, date). Sin sort, -date. delivered y read vacíos (antes de salir) van al final. (B17)
- GET /api/organization/whatsapp/broadcasts/{id}: nuevo query sort para los destinatarios (contact, number, status, changedAtUtc). Sin sort, -changedAtUtc. (B17)
- GET de plantillas de WhatsApp de la organización y de Plataforma: nuevo query sort (name, category, status, updatedAtUtc). En Plataforma, group=System también respeta sort; sin sort, por título como antes. (B17)
- GET de equipos de WhatsApp de la organización y de Plataforma: nuevo query sort (name, inbox, memberCount, notice; notice sin aviso va al final). (B17)
- GET /api/organization/whatsapp/contacts/{id}/conversations: nuevo query sort (startedAtUtc, team, messageCount; sin equipo, al final). lastMessagePreview (contenido cifrado) y assignee (un nombre de la cuenta, que el módulo no lee) no ordenan. Sin sort, -startedAtUtc (antes, por el último mensaje). (B17)
- GET /api/organization/whatsapp/contacts/{id}/broadcasts: nuevo query sort (name, sentAtUtc, status; status por su circuito). Sin sort, -sentAtUtc como antes. (B17)
- Los listados de plantillas, difusiones y destinatarios ahora validan: un sort fuera de la lista, un tamaño de página no admitido o un estado inválido es 400 Validation.Failed. (B17)
- docs/contracts/openapi.json regenerado con el parámetro sort en esas siete rutas. (B17)
- `GET/PUT /api/organization/whatsapp/settings` y `/api/platform/whatsapp/settings` ya no responden el 501: contestan con su `version`, su 400 de validación y su 409. `GET /api/auth/methods` no cambia de forma: los países del canal `whatsapp` salen de la configuración de WhatsApp de Plataforma y, sin fila, de `WhatsApp:AllowedCountries`. (B18)
- `GET /api/platform/settings`: `emailLogoUrl` es el logo que llega en los correos (sin guardar, el de arranque; quitado, `null`). (B18)
- `GET /api/organization/whatsapp/settings` suma `maxOrganizationChatRetentionDays`, el máximo que fija la plataforma: la pantalla lo nombra en la (i) y en el error (`WA-Config-Base2`). (integración)
- `GET /api/platform/settings` suma `senderAddress`, de solo lectura y `null` sin configurar: la dirección del «De» que fija la instalación (`Email:Smtp:FromAddress`), que muestra «Así llega» (`Config-Plat-Base2`). (integración)
- `PUT /api/platform/assistant/settings`: `retentionDays` va de 1 a 30, como «Conversaciones del Inicio» de `Config-Plat-Base2`; openapi.json publica las dos cotas y el front las lee de ahí. (integración)
- El estado del asistente (`GET /api/{organization|me|platform}/assistant`) suma `dailyResetTimeZoneId`, la zona en que corta el cupo del día (271): la de la organización en el acceso de empresa y la del perfil fuera de una. La pantalla dice «hora de Grupo Delta» (`Asistente-Org-Base2`). (integración)
- `docs/contracts/frontend-routes.json` suma `/org/whatsapp/configuracion`, que explica el artículo de ayuda «Administrar WhatsApp». (integración)
- Front: `src/shared/api/generated/schema.d.ts` regenerado con `npm run contracts`; suma el query `Sort` de las rutas de WhatsApp que ordenan desde la B17. Los dominios (`/api/platform/tenants/{id}/domains/paged`) y los roles de una empresa (`/api/companies/{id}/roles/paged`) ya admitían `sort`: el front ahora lo manda desde la dirección. (F12)
- openapi.json no publica las cotas de `loginAuditRetentionMonths` (1 a 120), `loginCodeRetentionDays` (1 a 3650), `closedInvitationRetentionDays` (7 a 3650), `appName` y `senderName` (60) en `UpdatePlatformSettingsHttpRequest`; el front las fija en `src/areas/platform/settings/lib/rules.ts`. (F13)
- `POST` de la revisión de un mensaje «Sin confirmar», con el `{base}` de la spec entero: `/api/organization/whatsapp/customers/conversations/{id}/messages/{messageId}/review`, `/api/organization/whatsapp/internal/conversations/{id}/messages/{messageId}/review` y `/api/platform/whatsapp/conversations/{id}/messages/{messageId}/review`. (K1)
- `PUT /api/organization/whatsapp/settings` (K1) responde 400 `WhatsApp.Settings.ChatRetentionOutOfRange` en `chatRetentionDays`, con el máximo de la plataforma en `maxChatRetentionDays`. (revisión)
- Las fallas del proveedor llevan el código entero: `Assistant.Provider.Limited`, `Assistant.Provider.ModelRejected`, `Assistant.Provider.Rejected`, `Assistant.Provider.Uncertain`, `Assistant.Provider.KeyUnreadable`, `Assistant.Provider.UnsupportedProvider` y `Assistant.Provider.PlatformUnavailable`; sin lugar en la cola de subidas, 429 `Assistant.Attachment.UploadQueueFull` con `retryAfter`. (K1)
- Migraciones: el `OutboxMessageId` de la invitación de un operador va en su cuenta, `AspNetUsers.OperatorInvitationOutboxMessageId` (B6); la última falla del proveedor va en `tenant.AssistantOrganizationProviderIssues` (con RLS) y `platform.AssistantPlatformProviderIssues`, con `Code` y `AtUtc`, y no en las configuraciones, que son versionadas (B13); el cupo del día suma `AssistantPlatformKeyDailyQuotas.OrganizationId` (B14).
- Front, piezas nuevas de `shared/ui`: `noticeTones` (F2), `InformativeTooltip` (F3), `permissionAreas` (F5), `GroupLabel` y `FilterChip` (F6), `tableSort` (F12), y `PickerList`, `pickerRows`, `usePickerFocus` y `usePickerKeyboard`, las partes de `Picker` (revisión de la F11).
- El 429 `Assistant.Limit.DailyReached` decía `{limit}` y `{time}` sin completar, y el `MonthlyReached`, «Llegaste al tope del mes.». Ahora su `detail` no lleva números: «Usaste tus consultas de hoy.» y «La organización llegó al tope del mes del asistente.», con su inglés. Al frenar una subida sin cupo, el front dice la frase de `LimitReached`, con `dailyLimit` y `dailyResetAtUtc` del 429 en la zona del estado (283). En openapi.json cambian solo los ejemplos. (puerta)
- `WhatsAppBroadcastResponse` suma `uncertain` (int32, requerido): los destinatarios «Sin confirmar», derivados al leer como el estado `Uncertain` de cada destinatario, sin consulta nueva; no suman a `delivered` ni a `failed`. «Resultados» lo muestra cuando hay alguno. Campo agregado: compatible con dos versiones (0060). (puerta)

## Para decidir

Nada abierto. El usuario decidió el 2026-11-19:
1. **la 255:** una difusión con un destinatario dudoso dice «Sin confirmar», sin revisión, y no cuenta como enviado ni como «No llegó» (`WA-Difusion-Resultados`);
2. **la 256:** mientras el Gmail se comparta con ArquitecturaBase (0063 §2), un tope global de 80 correos por día, con 20 para invitaciones, y los demás topes con los valores de la 0063, como valores de la configuración.
