# Etapa 11: WhatsApp de atención — plan corto

| Parte | Tareas | Estado |
|---|---|---|
| 0 · Decisiones de producto | D1–D8 | Hechas por el usuario el 2026-10-13 |
| 1 · Contrato | K1 | Hecha: OpenAPI, permisos, migración de datos, textos y tipos del front |
| 2a · Back | B1–B7 | Pendiente |
| 2b · Front | F1–F8 | Pendiente |
| 3 · Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Rige `AGENTS.md` («Velocidad y cierre liviano») y la guía [etapa en paralelo](../guides/etapa-en-paralelo.md): contrato primero; Back y Front a la vez, un chat por repo; integración y **una sola puerta**. Tests donde hay lógica: permisos, aislamiento, carreras, ventana de 24 h, consentimiento, cuotas y recorridos. Commits chicos, verificados, en español y con pathspec; sin push. Cada verificación completa pasa una vez; después de un arreglo, solo lo que toca. Sin actas ni revisiones por tarea: una revisión de código al cerrar.

## Fuentes

- **Producto:** [ADR 0038](../decisions/0038-whatsapp-de-atencion.md) con sus precisiones del 03/10 y del 11/10 (incluida la invitación española como Marketing, decisión del 12/10), [0043](../decisions/0043-las-pantallas-dicen-si-hay-cuenta.md), [0049](../decisions/0049-precisiones-whatsapp-y-asistente.md) y [0050](../decisions/0050-auditoria-global-de-modulos.md). Lo que ya existe: [WhatsApp](../features/whatsapp.md), [plantillas](../operations/whatsapp-plantillas.md) y [configuración](../operations/configuracion.md).
- **Pantallas:** lienzo v121 del front, todos validados. Organización: `WA-*` y `M-WA-*` (51). Plataforma: `Plat-WA-*` y `M-Plat-WA-*` (14). Además: área WhatsApp de `Rol`, botón «Cargar teléfonos» de `Usuarios`, «Mensajes» del perfil personal, correo «Chats esperando» de `Mensajes`, fila WhatsApp de Módulos en `Organizacion`, grupo «Números sin medio de pago» de `Inicio-Org`, `M-Mas-Plat`, `M-WA-Menu` y `Criterio-Decisiones`. Los datos de ejemplo de los tableros no son datos de prueba: algunos contradicen las reglas (ver «Para Diseño»).
- **Meta, documentación oficial verificada el 2026-10-13:**
  - Embedded Signup **v4** (v2 y v3 se retiran el 15/10/2026); `extras` vacío y productos elegidos en la configuración de Facebook Login for Business (`config_id`). El evento `WA_EMBEDDED_SIGNUP` devuelve `FINISH` (con `phone_number_id` y `waba_id`), `CANCEL` (con `current_step`) o `ERROR` (con `error_id`); cerrar en la última pantalla no es cancelar. El `code` vive 30 segundos: se canjea en el servidor, luego `POST /<WABA_ID>/subscribed_apps` y `POST /<PHONE_ID>/register` con PIN de 6 dígitos. Coexistencia: `featureType: whatsapp_business_app_onboarding`, app WhatsApp Business 2.24.17 o más nueva, webhooks `history`, `smb_app_state_sync` y `smb_message_echoes`, sin `/register`, 20 mensajes por segundo.
  - **Precios desde el 01/10/2026** (página de precios, actualizada el 30/09/2026): por mensaje, según país y categoría. Argentina, USD: marketing 0,0618; utility 0,0260; authentication 0,0260; service 0,0260. **Cada número tiene 1.000 mensajes de servicio entregados gratis por mes**, que no se acumulan y se reinician a las 00:00 de la zona de la cuenta. **Sin medio de pago, Meta entrega los de servicio solo dentro de esos 1.000**; las plantillas siempre exigen medio de pago (error 131042).
  - Medio de pago: `GET /<WABA_ID>?fields=primary_funding_id,timezone_id`; sin `primary_funding_id` no hay medio de pago. `health_status` no lo informa.
  - Alta de organizaciones: 10 nuevas cada 7 días; 200 con empresa verificada y App Review. Access Verification ya no se exige. Cada organización paga a Meta con su tarjeta.
  - Graph API vigente: **v26.0**; la v25.0 vence el 29/07/2028.

## Decisiones técnicas

1. **Módulo:** todo vive en `Modules/WhatsApp` (Domain/WhatsApp y las tres capas), con la llave de módulo por organización `whatsapp` en el catálogo de módulos (`[FeatureGate("whatsapp")]`, como `assistant`). Las funciones de la E8 del número de la plataforma (códigos, invitaciones, avisos, bot de ingreso) no dependen de esa llave.
2. **Registro de números global:** `identity.WhatsAppChannels` (número, `PhoneNumberId` único, `WabaId`, `OrganizationId` nulo para Plataforma, empresa atendida, estado `Connected | PaymentMissing | Disconnected`, coexistencia, zona de la cuenta de Meta, token cifrado con DataProtection por canal `[NotAudited]`, PIN cifrado, alta y baja). Es global con `IGlobalSecurityAudited` (ADR 0050): `TenantFeaturesChanged` con la organización como destino y `PlatformSettingsChanged` para Plataforma. Se copia el molde de `AssistantOrganizationPolicy`: global, con la llave de organización, leída por el claim. El webhook y el outbox lo leen sin entrar a una organización.
3. **Número de la plataforma:** al arrancar, si `WhatsApp:PhoneNumberId` está configurado y no hay canal de Plataforma, se crea uno con esos datos (actor System y su evento). Desde ahí manda el canal: conectar desde la pantalla lo reemplaza, y sin canal conectado WhatsApp queda apagado (ingreso y registro solo con correo y Google). `PaymentMissing` en Plataforma: códigos y avisos salen solo por correo; las consultas siguen hasta el tope gratuito. `AppSecret`, `VerifyToken`, `AppId` y `EmbeddedSignupConfigId` siguen siendo configuración de la instalación.
4. **Entrada:** el webhook sigue global; ubica el canal por `PhoneNumberId` (nunca por el remitente ni el host) y persiste en la cola durable de la E8, con `ChannelId`. El worker procesa cada entrada de organización con `TenantJobRunner` (`ITenantScope.Enter`), revisa el módulo con `IFeatureService` y escribe en las tablas de la organización. La fila global se purga al procesarse.
5. **Empleado o cliente:** un puerto neutral del núcleo `IActiveMemberPhoneReader` responde si un E.164 es un método Phone verificado de un miembro activo de la organización actual. Nada se publica sobre cuentas ajenas.
6. **Datos de atención:** las tablas de organización son `ITenantOwned` con RLS: contactos, etiquetas, conversaciones, mensajes, notas, equipos y miembros, bot, plantillas y sus variantes por WABA, difusiones y destinatarios. Plataforma tiene gemelas en `platform` para consultas, equipos, bot y plantillas «Para consultas», con la base común de entidades que usa el asistente. El vínculo contacto↔cuenta es `IConsumerBusinessShared` (`engagement`).
7. **Salida:** el outbox de la E8, con `ChannelId` en el payload cifrado; el dispatcher toma token y `PhoneNumberId` del canal. Llamadas a Meta fuera de la UoW. Graph v26.0 para el servidor y el SDK.
8. **Contador del mes:** los webhooks de estado traen `pricing.category` y `pricing.type`. Se cuentan los `service` entregados por canal y mes, con el mes de la nota 13. Lo cobrado se cuenta con `type=regular`, nunca restando 1.000.
9. **Costos estimados:** tabla de datos de referencia `WhatsAppRates` (país, categoría, moneda, precio, vigencia), cargada del CSV oficial vigente desde el 01/10/2026 y actualizada por seed versionado cuando Meta publique otra (solo lo hace al empezar un trimestre). El costo es `Money` y lo calcula el back.
10. **Importar contactos:** `.xlsx` y `.csv` con ClosedXML (MIT; versión exacta al agregarla, con al menos dos semanas de publicada). La E15 la reutiliza.
11. **Invitación por teléfono:** hoy la identidad de una invitación es siempre un correo. «Cargar teléfonos» necesita invitaciones cuya identidad y prueba sean un Phone: el núcleo suma `InvitationIdentity` Email|Phone y la aceptación exige ese Phone verificado en la cuenta que ingresa (ADR 0047). Va con tests de seguridad de identidad.
12. **Tiempo real:** sondeo con TanStack Query (10 s en bandejas, 5 s en la conversación abierta). Sin SignalR en esta etapa.
13. **Notas de B1 y B2 (decididas al programar):** un número de Plataforma que salió de la configuración la sigue (si cambia el identificador, se reemplaza) hasta que alguien lo conecta por la pantalla; el mes del cupo se corta en la zona de la organización (en Plataforma, UTC), porque Meta devuelve `timezone_id` numérico sin un catálogo por API; `account_update` no informa el medio de pago (referencia de Meta de mayo de 2026), así que `PaymentMissing` sale del error 131042 y de «Revisar»; el cambio de un número hecho por el Dueño deja `TenantFeaturesChanged` con actor persona (`SecurityEvent.ForOrganizationModule`); la cola revisa el módulo con la habilitación de la organización (`ITenantFeatureCache`), porque `IFeatureService` responde por el acceso de una sesión y el worker no tiene.
14. **Ayuda del asistente:** artículos es/en de las áreas nuevas, con requisito del módulo `whatsapp`, y la guarda que exige artículo por cada área con pantallas (D25 de la ADR 0048).
15. **Notas de B3 (decididas al programar):** una conversación termina cuando se cierra su ventana de 24 horas (no hay acción de cerrar): la entrada siguiente vuelve a pasar por el bot, sin equipo ni quien la tome. «Al terminar la primera conversación» es al terminar la parte del bot (elegir una opción, o la bienvenida sin menú), porque después de la ventana el bot ya no puede escribir; se pregunta una sola vez por contacto. Un bot sin configurar responde con valores iniciales (de lunes a viernes de 9 a 18 en la zona de la organización; en Plataforma, la del país de la cultura predeterminada). La opción «Entrar a mi cuenta» va primera en el menú de Plataforma. Los equipos se crean en B3 porque el menú los nombra. El ofrecimiento del vínculo (D2) va con B5, junto con el vínculo. El número de Plataforma que sale de la configuración arranca con las consultas apagadas (sigue el bot de la E8); el conectado por la pantalla, prendidas.
17. **Notas de B5 (decididas al programar):** la fila rechazada lleva su número en el archivo (el encabezado es la 1), como se ve en la planilla; las columnas se reconocen por encabezado en las culturas del catálogo; una baja no se deshace importando; las difusiones de un contacto llegan con B6 (hasta entonces, la lista vacía); el alta de contactos se serializa por organización, porque una importación crea hasta 5.000 a la vez.
18. **Notas de B5, vínculo (decididas al programar):** el vínculo y las copias de los mensajes son datos compartidos (`engagement`): la persona no puede leer las tablas de la organización, así que lee la copia. La cola deja de usar `TenantJobRunner` para la entrada de una organización y abre su scope como esa organización (`ITenantAccessInitializer.SetFromAccess(…, Business)`, precedente `RolePermissionCache`), porque el guardado sella la parte del dato compartido y `ITenantScope.Enter` deja la clase en null (decisión 4 ajustada). Para ofrecer y crear el vínculo alcanza con que el teléfono siga siendo verificado de la cuenta (`OriginalMethodOwned`), aunque el ingreso por teléfono esté apagado. El ofrecimiento sale una vez por contacto.
19. **Notas de B6, plantillas (decididas al programar):** el estado en Meta es global por cuenta de WhatsApp (`identity.WhatsAppTemplateStates`), como los números, para que el webhook y la sincronización no entren a una organización. Además de los webhooks, una sincronización cada hora trae el estado de todas las cuentas: sin ella, las plantillas del sistema creadas a mano en Meta quedarían pendientes. El nombre de Meta sale del que escribe la persona. Sin un número vigente, crear una plantilla responde `MetaRejected` (Meta no tiene dónde recibirla). Un idioma de Meta fuera del catálogo de culturas no se lista. Las tarifas se cargaron con la tabla verificada en el plan (Argentina, USD); el CSV completo de Meta entra con el seed versionado cuando se habilite otro país. La variable de la plantilla se publica como `{label, example}`, el mismo par del alta (CC23).
20. **Notas de B6, difusiones (decididas al programar):** la difusión la manda el primer número conectado de la organización cuya cuenta tiene la plantilla aprobada. Los destinatarios se fijan al crearla y al salir se vuelve a mirar el consentimiento; quien ya no acepta queda como «Se dio de baja». El tope de Meta es de toda la cartera: se descuentan los contactos a los que la organización les mandó una difusión en las últimas 24 horas y lo que no entra sale en la vuelta siguiente. «Respondieron» es escribir en los 7 días siguientes. En la conversación, Marketing solo a quien acepta promociones. Que la fecha programada no esté en el pasado lo revisa el servicio, porque los validadores no reciben el reloj.
16. **Notas de B4 (decididas al programar):** quien tiene el permiso de una bandeja la ve entera (los conteos de CC15 ya lo suponen); los equipos dicen a quién le llega y filtran. «Sin tomar» es sin quien la tome; `unread` es que el último mensaje es de la otra parte. «Pasar a» una persona ofrece las del equipo del chat (sin equipo, las de los equipos de la bandeja). El estado de entrega se deriva al leer (outbox y fila `accepted`), porque el envío es global y no entra a la organización. El «ya avisado» del correo «Chats esperando» (D6) vive en una tabla propia por equipo, porque marcarlo en el equipo dejaría una entrada de auditoría (o un evento de seguridad en Plataforma) por cada aviso.

## Parte 1 · Contrato (K1)

Un solo chat publica todas las rutas en `openapi.json` con `501`, los permisos, la llave de módulo, los códigos con texto es/en y los tipos del front (`npm run contracts`). Las guardas de rutas quedan en verde.

**Permisos:**
- **Organización**, en el catálogo, `Permissions.resx` y `.en.resx`: `whatsapp.customers` (Atender clientes; incluye ver contactos), `whatsapp.internal` (Atender internos), `whatsapp.broadcasts` (Mandar difusiones) y `whatsapp.manage` (Administrar WhatsApp).
- **Plataforma:** `platform.whatsapp.manage` (Número, Bot, Equipos y Plantillas) y `platform.whatsapp.attend` (Consultas).
- **Migraciones de datos:** el Dueño (`TenantAdmin`) recibe los cuatro en el seed y en las organizaciones existentes, y ningún otro rol de sistema los recibe (D3); los operadores que ya tienen `platform.operators.manage` reciben los dos permisos de Plataforma, con su `SecurityEvent` (lección A1/M1 de la 8b).

**Texto para el chat del Contrato:**

```
Leé AGENTS.md de los dos repos y la parte 1 de docs/plans/2026-10-13-etapa-11-whatsapp.md (back). Publicá K1 completo: rutas con 501, permisos con resources y migraciones de datos, llave de módulo, códigos y textos es/en; después npm run contracts en el front. Guardas de rutas y tests de contrato en verde. Commits con pathspec en los dos repos, sin push.
```

**Rutas de organización:** `[Access(Business)]` y `[FeatureGate("whatsapp")]`, prefijo `/api/organization/whatsapp`. Listados con `PagedResult` de 10 y `search`; ediciones con `version`; `POST` que crean o mandan con `[Idempotent]`.

| Ruta | Permiso | Recibe → devuelve |
|---|---|---|
| GET `numbers`, GET `numbers/{id}` | manage | `{numbers: PagedResult<Number>, signup: {appId, configId, sdkVersion}?}` y el número. `Number = {id, displayNumber, verifiedName, company?: {id,name}, status, coexistence, serviceMessagesThisMonth, freeServiceMessages, chargedServiceMessagesThisMonth, connectedAtUtc, version}` |
| POST `numbers` | manage | `{code, phoneNumberId, wabaId, companyId?, coexistence}` → 201 `Number` |
| POST `numbers/{id}/check` | manage | `{version}` → `Number` (vuelve a consultar el medio de pago) |
| PUT `numbers/{id}` | manage | `{companyId?, version}` → `Number` |
| DELETE `numbers/{id}?version=` | manage | 204 (D7) |
| GET/PUT `numbers/{id}/bot` | manage | `{welcome, outOfHours, schedule: {timeZoneId, days[{day, from, to}]}, menu[≤10 {id, label, action: Team|Reply, teamId?, reply?}], askPromotions, offerAccountLink, employeesTeamId?, version}` |
| GET/POST `teams`, PUT/DELETE `teams/{id}` | manage | `{name, inbox: Customers|Internal, memberIds[], notifyAfterMinutes: 10|15|30|null, version}` |
| GET `customers/conversations` o `internal/conversations` | customers o internal, según la ruta | `?teamId&state=Untaken|Mine|All&search&page` → `{conversations: PagedResult<ConversationRow>, counts: {customers, internal, untaken}}` (CC15). Las acciones siguientes van bajo el mismo prefijo de bandeja. |
| GET `conversations/{id}` | ídem | `?page` → `{conversation, messages: PagedResult<Message>}`; `Message = {id, kind: Text|Template|Note|Event|Media, direction, author?, text?, mediaKind?, status, failureReason?, createdAtUtc}` |
| POST `conversations/{id}/take` | ídem | sin cuerpo → `ConversationRow` |
| POST `conversations/{id}/messages` | ídem | `{text}` → 201 `Message` (toma el chat si estaba sin tomar) |
| POST `conversations/{id}/notes` | ídem | `{text}` → 201 `Message` |
| POST `conversations/{id}/transfer` | ídem | `{teamId?, memberId?, version}` → `ConversationRow` |
| GET `conversations/{id}/template-options` | ídem | → `PagedResult<{templateId, name, category, variables[], estimatedCost: Money}>` |
| POST `conversations/{id}/templates` | ídem | `{templateId, variables[]}` → 201 `Message` |
| GET `contacts` | customers | `?search&tag&consent&page` → `PagedResult<Contact>` + `{total, accepting}` |
| GET `contacts/{id}`, `contacts/{id}/conversations`, `contacts/{id}/broadcasts` | customers | ficha y sus dos pestañas paginadas |
| PUT `contacts/{id}` | customers | `{name, tags[], consent?, version}` |
| GET `contacts/import-sample` | customers | archivo `.xlsx` (Nombre, Teléfono, Etiqueta, Acepta promociones) |
| POST `contacts/import` | customers | multipart `{file, tag?}` → `{imported, updated, rejected: [{row, reason}]}` |
| GET `contacts/tags` | customers | `PagedResult<string>` |
| GET/POST `templates`, GET/DELETE `templates/{id}`, POST `templates/{id}/duplicate` | manage | `{name, category: Utility|Marketing, language, body, variables[{label, example}]}`; estado `Approved|Pending|Rejected|Paused` con motivo |
| GET `broadcasts`, GET `broadcasts/{id}` | broadcasts | lista; resultados con contadores y `PagedResult<Recipient>` |
| GET `broadcasts/estimate` | broadcasts | `?templateId&tag` → `{recipients, estimatedCost: Money}` |
| POST `broadcasts` | broadcasts | `{name, templateId, variables[], audience: {kind: AllAccepting|Tag, tag?}, scheduledAtUtc?}` → 201 |
| POST `broadcasts/{id}/cancel`, POST `broadcasts/{id}/duplicate` | broadcasts | `{version}` / 201 |
| POST `phone-imports/preview` | `users.invite` | `{phones[≤200], companyId?, roleIds[]}` → filas `{phone, memberName?, outcome: AlreadyMember|Invite|Invalid}` |
| POST `phone-imports` | `users.invite` | igual → `{linked, invited, invalid}` |

**Rutas de Plataforma:** `[Access(Platform)]`, prefijo `/api/platform/whatsapp`. `number` (GET, POST para conectar, POST `check`, PUT `{consultationsEnabled, version}`), `bot`, `teams`, `conversations` (con las mismas acciones, sin `inbox`, y `account?: {id}` cuando el número es un Phone verificado de una cuenta), `templates?group=consultations|system` (las del sistema son de solo lectura y traen su estado en Meta). Permisos: `platform.whatsapp.manage`; Consultas, `platform.whatsapp.attend`.

**Rutas personales:** `[Access(Consumer)]`, `/api/me/whatsapp/conversations` (lista y detalle de solo lectura) y DELETE `/api/me/whatsapp/links/{id}` (desvincular). La misma lectura alimenta la fuente `whatsapp` de `IConsumerInteractionSource`.

**Errores** (texto es/en en los resources del módulo; la API no usa 422 para reglas: estado es 409 y dato es 400): `WhatsApp.Number.AlreadyConnected` 409, `WhatsApp.Number.SignupExpired` 400 (campo `code`), `WhatsApp.Number.MetaRejected` 503 (con `metaCode`), `WhatsApp.Number.NotFound` 404, `WhatsApp.Conversation.NotFound` 404, `WhatsApp.Conversation.TakenByOther` 409 (con `takenBy`), `WhatsApp.Conversation.WindowClosed` 409, `WhatsApp.Conversation.PaymentRequired` 409 (plantilla sin medio de pago o servicio pasado el tope), `WhatsApp.Conversation.ReadOnly` 409 (número desconectado, D7), `WhatsApp.Bot.TooManyOptions` 400, `WhatsApp.Team.NotFound` 404, `WhatsApp.Team.NameTaken` 409, `WhatsApp.Contact.NotFound` 404, `WhatsApp.Template.NotFound` 404, `WhatsApp.Template.NameTaken` 409, `WhatsApp.Template.NotApproved` 409, `WhatsApp.Broadcast.NotFound` 404, `WhatsApp.Broadcast.NoRecipients` 400, `WhatsApp.Broadcast.NotCancellable` 409, `WhatsApp.Import.InvalidFile` 400, `WhatsApp.Import.TooManyRows` 400 (más de 5.000), `WhatsApp.Link.NotFound` 404 y, del núcleo, `Invitations.Invitation.ChannelUnavailable` 503. Recurso ajeno: 404. Módulo apagado: 404 común.

## Parte 2a · Back

Cada tarea suma sus tests de integración en `tests/…Api.IntegrationTests/Modules/WhatsApp/` y los de aislamiento en `Tenancy/`. Moldes: `AssistantControllerBase.cs` (controllers), `AssistantConversationService.cs` (servicio con `OperationLog` y una UoW por método), `AssistantOrganizationPolicy` (global por organización) y `WhatsAppInboundService.cs` (cola durable).

- **B1 · Números y registro.** `WhatsAppChannel` y su configuración EF; protector del token (copiá `AssistantKeyProtector`); cliente Graph del alta (canje del `code`, `subscribed_apps`, `register`, `primary_funding_id` y `timezone_id`); arranque desde la configuración; `IWhatsAppChannelSettings` lee el canal. Fuente de «Para resolver» «Números sin medio de pago» (copiá `IPendingGroupSource` de C1). El webhook `account_update` y el error 131042 marcan `PaymentMissing`; `check` lo revisa. Contador del mes (decisión 8). Tests: alta idempotente, número de otra organización (409), token nunca en logs ni DTO, Plataforma sin canal (ingreso solo con correo y Google) y sin pago (códigos por correo).
- **B2 · Entrada multi-número.** Ruteo por `PhoneNumberId`, cola con `ChannelId`, `TenantJobRunner`, módulo apagado (no procesa y conserva), `IActiveMemberPhoneReader` en el núcleo (Infrastructure/Identity). Conserva todos los tests de la E8 del número de la plataforma (`WhatsAppWebhookTests`, `WhatsAppInboundProcessorTests`, `WhatsAppBotTests`). Tests: dos organizaciones con números distintos no se cruzan; un remitente que es miembro activo va a Internos y uno que dejó de serlo, a Clientes.
- **B3 · Bot.** Configuración por número, horario con zona del catálogo de referencia, lista interactiva de hasta 10 opciones, «Además» (preguntar por promociones al terminar la primera conversación; ofrecer el vínculo según D2; empleados directo a su equipo). Plataforma: la opción del sistema «Entrar a mi cuenta» con las ramas de la 0049, sin «Además»; con consultas apagadas, el bot es el de la E8. Respuestas libres solo dentro de las 24 h.
- **B4 · Bandejas y equipos.** Conversaciones, mensajes y notas; tomar con bloqueo de la conversación (el segundo recibe `TakenByOther` y su mensaje no sale); pasar a un equipo o persona; ventana de 24 h; estados de entrega; correo «Chats esperando» con `PeriodicWorker`, solo con el resumen y la frecuencia de D6; consultas de Plataforma con `account` y su enlace a la ficha si el operador tiene `platform.accounts.read`. Tests: carrera de dos que toman, nota que nunca llega a Meta, ventana vencida, correo sin teléfonos ni textos.
- **B5 · Contactos, consentimiento y personas.** Contactos, etiquetas y consentimiento `Accepted|Declined|NoAnswer|Unsubscribed` con origen (`Bot|Import|Manual|Unsubscribe`) y fecha; importar con ClosedXML (sin «Sí» queda «Sin respuesta»); vínculo con la cuenta según D2 y D5; rutas personales y fuente de `IConsumerInteractionSource`; «Cargar teléfonos» con la invitación por teléfono del núcleo (decisión 11) y la plantilla `invitacion_organizacion`. Tests: el preview no revela cuentas (0038, precisión del 11/10), un número reciclado no hereda historia (D5), aceptar una invitación por teléfono exige ese Phone verificado.
- **B6 · Plantillas y difusiones.** Plantillas por la API de Meta, con una variante por WABA; estado y categoría por los webhooks `message_template_status_update` y `template_category_update`; una aprobada se duplica, no se edita. Plantillas del sistema de Plataforma: las 16 de la E8, solo lectura. `WhatsAppRates` y costo estimado. Difusiones solo con plantillas Marketing aprobadas y solo a contactos con `Accepted`, revisado al enviar; respetan el límite de mensajería de la cuenta (`whatsapp_business_manager_messaging_limit`); botón «No recibir más» → `Unsubscribed`; resultados por contacto; programar, cancelar y duplicar. Tests: un contacto que se dio de baja entre programar y enviar no recibe, costo con la tarifa vigente, plantilla rechazada no se puede usar.
- **B7 · Privacidad, ayuda y retiro.** Retención según D4; participante de la baja (vínculos, contactos ligados, notas con su nombre); inventario para exportar (E10) en [datos personales](../rules/datos-personales.md); artículos de ayuda y su guarda; [quitar WhatsApp](../guides/quitar-whatsapp.md) actualizada; `ModuleIsolationTests` y clasificación de tablas en verde.

**Texto para el chat del Back:**

```
Leé AGENTS.md del back y docs/plans/2026-10-13-etapa-11-whatsapp.md. Hacé B1 a B7 en orden, contra el contrato publicado en K1, sin cambiarlo (si hace falta, anotalo en «Cambios al contrato» y avisá al coordinador). Copiá los moldes que nombra cada tarea; no inventes. Tests donde hay lógica y los de aislamiento del módulo. Commits chicos con pathspec, sin push. No toques el front.
```

## Parte 2b · Front

Todo va en `src/modules/whatsapp`, autocontenido y aportado por `registration.ts`, como `src/modules/assistant`. Se copian los tableros vigentes; los estados que faltan los dibuja Diseño y se siguen las demás pantallas mientras tanto. Tests focales con MSW: permisos y módulo, tomar con conflicto, 24 h, alta de Meta (los tres resultados), consentimiento en difusiones y costo mostrado sin calcular dinero.

- **F1 · Navegación y módulo.** Menú «WhatsApp» con su contador y panel (Chats, Contactos, Difusiones); Administración › WhatsApp (Números, Bot, Equipos, Plantillas); teléfono como app (`M-WA-Menu`, barra con Chats y Contactos); Plataforma: «WhatsApp» directo a Consultas, Administración › WhatsApp y la barra con «Consultas» (`M-Mas-Plat`); Personal: «Mensajes». Fila WhatsApp en Módulos, área WhatsApp en `Rol` (sale del catálogo), botón «Cargar teléfonos» en `Usuarios` y grupo de «Para resolver».
- **F2 · Números y alta.** `WA-Admin*`, `WA-Alta*` y `Plat-WA-Numero`/`Plat-WA-Alta` con sus `M-*`. El SDK de Facebook se carga solo al conectar (v26.0, `config_id` de la API); el oyente de `WA_EMBEDDED_SIGNUP` maneja `FINISH`, `CANCEL` y `ERROR`; en el teléfono no se dibuja la ventana de Meta.
- **F3 · Chats.** `WA-Chats*` y `Plat-WA-Chats` con sus `M-*`: bandejas, filtros, conversación, tomar, pasar, notas, 24 h y plantilla con costo.
- **F4 · Contactos.** `WA-Contactos`, `WA-Contacto`, `WA-Contactos-Importar` y sus `M-*`.
- **F5 · Difusiones.** `WA-Difusiones`, `WA-Difusion-Nueva` y `WA-Difusion-Resultados` con sus `M-*`.
- **F6 · Bot, Equipos y Plantillas.** `WA-Respuestas`, `WA-Admin-Editar`, `WA-Bot-Recorrido`, `WA-Equipos*`, `WA-Plantilla*` y las de Plataforma, con sus `M-*`.
- **F7 · Personas y usuarios.** `WA-Mensajes`/`M-WA-Mensaje(s)` y `WA-Telefonos`/`M-WA-Telefonos`.
- **F8 · Secciones, vistas y filtros** (regla del usuario del 2026-10-13 en `docs/rules/pantallas-y-ui.md` del front; manda sobre los tableros dibujados antes). Las bandejas «Clientes | Internos» de Chats, «Para consultas | Del sistema» en las plantillas de Plataforma y «Configuración | Recorrido» del bot (`WA-Respuestas`, `WA-Bot-Recorrido`, `Plat-WA-Bot` y sus `M-*`; elección del usuario del 2026-10-13) van con `SegmentedControl` arriba de la lista, no con pestañas, y los filtros de los listados, como desplegables de `FilterBar`. Uso del asistente (`src/modules/assistant/usage/AssistantUsagePage.tsx`) pasa de pestañas a `SegmentedControl`, en la compu y en el teléfono (`Asistente-Uso-Org` y `M-Asistente-Uso-Org`). La guarda va en `src/test/harness.test.ts`: solo `src/shared/ui/Page.tsx` importa `shared/ui/tabs`.

**Texto para el chat del Front:**

```
Leé AGENTS.md del front y ../ArquitecturaBaseMutitenant/docs/plans/2026-10-13-etapa-11-whatsapp.md. Hacé F1 a F8 (F8 toca también src/modules/assistant/usage y src/test/harness.test.ts) en src/modules/whatsapp, contra los tipos generados en K1, copiando los tableros del lienzo vigente; si a un tablero le falta un estado, seguí con el resto y avisá al coordinador. Tests focales donde hay lógica. No toques el lienzo ni los archivos de Diseño: commiteá solo tus rutas, con pathspec y sin push. No toques el back.
```

## Parte 3 · Integración y puerta

- **I1 · Meta de desarrollo** (lo hace el usuario en Meta, según D8): app Business en modo desarrollo con el caso de uso WhatsApp; Facebook Login for Business con una configuración creada desde la plantilla de Embedded Signup (`config_id`); los seis interruptores de OAuth, «Allowed domains» y «Valid OAuth redirect URIs» con el dominio HTTPS del túnel; webhook al túnel con `messages`, `account_update`, `message_template_status_update`, `template_category_update`, `phone_number_name_update`, `history`, `smb_app_state_sync` y `smb_message_echoes`; quien prueba, con rol en la app; un número real libre y, para probar plantillas y difusiones, un medio de pago en esa cuenta. Los secretos van con `scripts/secretos/` (`WhatsApp:AppId`, `WhatsApp:AppSecret`, `WhatsApp:EmbeddedSignupConfigId`, `WhatsApp:VerifyToken`).
- **I2 · Conectar los dos repos.** Front contra la API real; lo que no encaja vuelve a su dueño o se anota en «Cambios al contrato».
- **I3 · Puerta** (skill `/puerta`, cada paso completo una vez): build sin advertencias y `dotnet test` completo; front `build`, `lint`, `test` y `contracts:check`; E2E real: conectar un número por el alta (la ventana de Facebook la completa el usuario, porque es su cuenta), escribir como cliente y como empleado, tomar en carrera, pasar, nota, plantilla, una difusión chica a un contacto que aceptó, vincular y ver «Mensajes» como persona; detector de documentación en 0; galería solo de las pantallas nuevas; recorrido con `/recorrido`; quitar el módulo una vez con base vacía.

**Texto para el chat de Integración:** «Leé AGENTS.md de los dos repos y la parte 3 de docs/plans/2026-10-13-etapa-11-whatsapp.md. Conectá el front a la API real, corré la puerta con /puerta y dejá la tabla de resultado arriba del plan. Los pasos de Meta que exigen la cuenta del usuario se los pedís a él.»

## Relación con otras etapas

- **E12 a E17 (formularios):** usan `WhatsAppChannels` (`WabaId` y token por organización), las conversaciones de Internos, la regla de tomar y la cola de entrada. No se adelanta nada de formularios.
- **E9:** la CSP tiene que permitir el SDK de Facebook (`connect.facebook.net` y el marco de `facebook.com`), y los límites de pedidos cubren las rutas nuevas.
- **E10:** exportar mis datos incluye contactos ligados, vínculos y mensajes propios, según el inventario de B7.

## Decisiones del usuario (2026-10-13)

| # | Decisión |
|---|---|
| D1 | Cada número muestra siempre «N de 1.000 gratis este mes». Sin medio de pago, el aviso dice «Responde hasta 1.000 mensajes por mes. Sin medio de pago no manda plantillas ni difusiones». |
| D2 | La persona vincula su conversación con un botón del bot, que solo se ofrece si el número es un Phone verificado de una cuenta. Sin pantalla nueva. |
| D3 | Solo el Dueño recibe los cuatro permisos de entrada. Son permisos de organización, no por empresa. Ver, editar e importar contactos y cambiar el consentimiento a mano: «Atender clientes». |
| D4 | El texto de los chats se guarda mientras exista el contacto (en Plataforma, la conversación) y se borra con él. Apagar el módulo no borra. |
| D5 | «Mensajes» muestra la conversación desde que se vinculó. Un número reciclado nunca muestra mensajes del dueño anterior. |
| D6 | Un correo «Chats esperando» por equipo cuando el chat más viejo sin tomar pasa el tiempo elegido; no se repite hasta que la bandeja se vacía y vuelve a llenarse. «No avisar» es una opción. |
| D7 | Fotos, audios y documentos se ven como «Mandó una foto/audio/documento», sin descarga (llega con la E14). Desconectar un número deja sus chats para leer, sin responder. |
| D8 | Se desarrolla con una app nueva de Meta en modo desarrollo, en el mismo portfolio; «Servicios Ya» no se toca. |
| D9 | El bot no se apaga: con un número conectado siempre responde. Diseño saca «Activo» de los tableros del bot. |
| D10 | Al editar un contacto se puede crear una etiqueta nueva («Crear «VIP»»). Si ya hay una igual sin distinguir mayúsculas, se elige esa. Vale también para «Etiqueta para todos» al importar. Es solo del front; el contrato ya la acepta. |

## Para Diseño

Texto para la conversación de Diseño (única que toca el lienzo):

```
Etapa 11, sobre el lienzo v121. Contradicciones: Plat-WA-Plantillas y su M-* muestran invitacion_organizacion como Utilidad (es Marketing en español y Utility en inglés); Criterio-Decisiones cita la 0041 en Cargar teléfonos (vale la 0043 y la precisión del 11/10 de la 0038); las respuestas del bot en Mensajes dicen «Por ahora este chat solo sirve para entrar», que deja de ser cierto con Consultas; WA-Alta muestra {{res}} sin resolver; WA-Admin-Pago dice que sin medio de pago no se puede responder, y Meta entrega hasta 1.000 de servicio por mes: con la decisión D1, el número muestra siempre «N de 1.000 gratis este mes» y, sin medio de pago, «Responde hasta 1.000 mensajes por mes. Sin medio de pago no manda plantillas ni difusiones». Estados que faltan: el mensaje del bot que ofrece vincular la conversación a la cuenta, con su botón (D2); un mensaje «Mandó una foto/audio/documento» (D7); el chat de un número desconectado, solo para leer (D7); responder un chat que otro tomó; la bandeja Internos con un empleado; el contador pasado de 1.000 («1.240 · 240 se cobran»); chat sin medio de pago pasado el tope, y plantilla en revisión o rechazada al elegirla; Bot: editar bienvenida y fuera de horario (texto, días, horas y zona), los interruptores de «Además», elegir número con varios y el tope de 10; Contactos: editar contacto (etiquetas y consentimiento), desvincular y el resultado de importar con filas rechazadas; contenido de los ⓘ «Qué significa», «Por qué la rechazó Meta» y «Por qué no llegó»; Equipos: opciones del aviso con «No avisar»; correo «Consultas esperando» de Plataforma; difusión programada: cancelar y confirmar el envío, con la zona; Mensajes de la persona: vacío y desvincular; diálogos «Desconectar» y «Cambiar a quién atiende»; Cargar teléfonos, primer paso (pegar o subir) y su entrada desde el ⋮ de M-Usuarios; los dos permisos de WhatsApp en Cuenta-Plat; Plat-WA-Numero con consultas apagadas; invitaciones por teléfono en la lista de Usuarios. Los datos de ejemplo no deben contradecir las reglas: difusiones solo a quienes aceptan y ningún número a la vez de empleado y de cliente.
```

## Cambios al contrato

Aprobados por el usuario el 2026-10-13, después de cruzar el contrato con los tableros ([página de revisión](https://claude.ai/artifact/WvegbB8Pv8Z4ev8eqtJ6Nd)). Publicados en los dos repos.

| # | Cambio | Para |
|---|---|---|
| CC01 | GET `{bandeja}/conversations/{id}/transfer-options/teams` y `/people`, con el permiso de la bandeja: equipos con su cantidad de personas y personas con sus chats tomados | «Pasar a» (B4) |
| CC02 | `contactId` y `tags` en la fila de la conversación | «Ver ficha» y la etiqueta del encabezado (B4) |
| CC03 | `body` en las opciones de plantilla | Vista previa (B6) |
| CC04 | GET `broadcasts/template-options` y `broadcasts/tags` con «Mandar difusiones» | Nueva difusión (B6) |
| CC05 | POST `contacts/import/preview`: filas, nuevas, actualizadas y rechazadas, sin guardar | «Importar 48 contactos» (B5) |
| CC06 | Desvincular pasa a DELETE `/api/me/whatsapp/conversations/{id}/link` | «Mensajes» de la persona (B5) |
| CC07 | Conteos en las listas: `paymentMissing` en números, `scheduled` en difusiones y `approved` en plantillas | Resúmenes de los tableros |
| CC08 | `moduleCards` en el Inicio (`HomeModuleCard(key, total)`, hasta 10); B1 suma el puerto del núcleo y el agregado de `HomeService` | Tarjeta «WhatsApp · 2 números» (B1) |
| CC09 | GET `/api/organization/whatsapp/untaken`: sin tomar por bandeja, null en la que la persona no puede ver | Contador del menú (B4) |

Del cruce con el front ya programado (F1–F8), decididos por el coordinador por ser técnicos. Son aditivos, salvo CC17, y el Back los publica en un commit de contrato.

| # | Cambio | Para |
|---|---|---|
| CC10 | `Money` con su forma en el OpenAPI: `{amount: number decimal, currency: ISO de 3 letras}`, como `AssistantMoney` | Costos (B6) |
| CC11 | GET `teams/member-options`, en organización y Plataforma, paginado con `search`, con el permiso de administrar. `memberIds` son miembros activos en organización y `accountId` de operadores en Plataforma | Integrantes del equipo (B4) |
| CC12 | El detalle del equipo trae todos los integrantes sin paginar, hasta 200, y el validador limita `memberIds` a 200. La fila suma `memberPreview` (hasta 3 nombres) | Equipos (B4) |
| CC13 | La fila de la conversación suma `channel {id, displayNumber, status}` y `assignedToMe` | «Mío» y número sin medio de pago (B4) |
| CC14 | GET `{bandeja}/inbox-teams` con el permiso de la bandeja (Plataforma: `/api/platform/whatsapp/inbox-teams`, con `attend`) | Filtro «Equipo» (B4) |
| CC15 | Convenciones: la página 1 de mensajes trae los más recientes, en orden descendente; un saliente sin `author` es el bot. Los `counts` cuentan la bandeja entera, sin `search`, `state` ni `teamId`. En organización van `customers` e `internal` por bandeja, con 0 en la que la persona no ve, y `untaken` de la bandeja de la ruta. En Plataforma, `customers` son todas las consultas e `internal` es 0. `totalCount` solo pagina | Chat, banda y menú (B4) |
| CC16 | `status` en GET `broadcasts`; `search` sobre los destinatarios en GET `broadcasts/{id}`; `country` (ISO 3166) en la estimación | Difusiones (B6) |
| CC17 | Se retiran POST `broadcasts/{id}/duplicate` y `templates/{id}/duplicate`, en organización y Plataforma. «Duplicar» abre el formulario nuevo con los datos de la original | Difusiones y plantillas (B6) |
| CC18 | `lastMessagePreview` en las conversaciones de la ficha; `latestConversationId` en el contacto; `displayNumber` siempre en E.164 | Ficha del contacto (B5) |
| CC19 | Conteos por grupo en Plataforma (`consultations`, `system`; null en organización); `title` de las del sistema; `languages` (hasta 10) en lugar de `language`; el alta recibe la cultura del catálogo (es-AR) y el back la traduce a la de Meta | Plantillas (B6) |
| CC20 | `phoneNumberId` opcional con `coexistence`: el back resuelve el número desde la WABA | Alta (B1) |
| CC21 | GET `numbers/company-options` con «Administrar WhatsApp», paginado con `search` | «Atiende a» (B1) |
| CC22 | `maxLength` de cada etiqueta: `items.maxLength` en `tags` del contacto y `maxLength` en `tag` de la importación, de la lista de contactos y de la estimación | Etiquetas (B5) |
| CC23 | La difusión suma `variables` (los valores usados); la plantilla pasa `variables` a `[{name, example}]`. «Duplicar» copia todo; el nombre de una plantilla duplicada lo propone el front con `_2` (o el número siguiente), y si está tomado responde `NameTaken` | Duplicar (B6) |
| CC24 | `languages` pasa a `[{culture, category}]`, con la cultura del catálogo (es-AR, nunca el código de Meta) y el idioma principal primero; `category` queda como la del primer idioma | Plantillas del sistema, Marketing en español y Utility en inglés (B6) |
