# E21 · Reglas, aprobación, PDF y avisos: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E21](21-reglas-y-aprobacion.md), que dice qué leer y cómo se cierra.

## El bloque en las reglas (146 y 177)

- **en las condiciones:** al elegir en una condición una pregunta del bloque («Productos › ¿Urgente?»), la condición empieza con «Algún ítem con». Deciden como «Filtros» de la E19: las que tienen valor las cumple un mismo ítem, también varias del mismo bloque, y la presencia (`answered`, `unanswered` y `notAsked`) mira la respuesta entera. El editor ofrece para una pregunta del bloque solo las condiciones con valor, y una respuesta de antes del bloque no cumple ninguna sobre sus preguntas. La regla guardada dice «Si algún ítem de Productos con ¿Urgente? es Sí», con la pregunta y el valor en negrita, y en inglés, «If any item of Productos with…»;
- **en el mensaje:** en «Agregar dato», el bloque es un dato que trae todos sus ítems, y sus preguntas no se ofrecen sueltas: una del bloque no es un dato, y guardarla da `Forms.Rules.PlaceholderInvalid`. En «Así llega» y en el aviso, cada ítem va en su línea, numerado desde 1, con sus respuestas en el orden de las preguntas, separadas por « · » y sin las vacías: «1. Caño de 1/2 pulgada · 10 · Sí». El primero empieza en una línea nueva, salvo al comienzo del mensaje o después de un salto. Un ítem vacío deja solo su número, y sin ítems, o en una versión sin el bloque, el dato queda vacío.

## «Adjuntar el PDF» (113 y 162)

Las acciones que mandan un mensaje o un correo llevan esa casilla, y «Mandar el PDF a un cliente» lo manda siempre:
- **nada se desmarca solo:** ni al mover, quitar o cambiar una acción, ni al cambiar el evento;
- **si «Armar el PDF» falta o está después,** debajo de la casilla aparece en el momento «Para adjuntarlo, «Armar el PDF» tiene que ir antes de este mensaje.». Debajo, un botón lo arregla: «Subir "Armar el PDF"» pone el primero que está después justo antes del mensaje; si no hay ninguno, «Agregar "Armar el PDF" antes» lo agrega ahí, y el foco vuelve a la casilla. Con 10 acciones, «Agregar» queda apagado, con el motivo en su tooltip;
- **cuándo se puede** (106): si el formulario pide aprobación, el PDF existe solo al aprobar. En otro evento, la casilla sin marcar se apaga, con el motivo en su tooltip. Marcada no se apaga: dice lo mismo debajo, y la regla no se guarda hasta sacarla o cambiar el evento. Sin aprobación, vale al recibir;
- **«Mandar el PDF a un cliente»** no tiene casilla: el aviso y el arreglo van debajo de la acción;
- **cambiar el tipo de una acción conserva lo escrito:** el mensaje, el rol, la persona y el adjunto vuelven si se elige otra vez el tipo anterior. Al guardar, cada dato va solo con el tipo que lo usa.

## El motor de acciones

- cada respuesta conserva la revisión con la que llegó, y la captura crea sus ejecuciones en el mismo commit;
- cada acción es una ejecución, con clave respuesta + evento + regla + acción, reserva y número de turno. El efecto va fuera de la transacción. **El worker reserva solo los tipos de acción que conoce:** uno de una versión más nueva espera, sin reservarse ni fallar, porque conviven dos versiones ([ADR 0060](../../decisions/0060-dos-versiones-conviven.md));
- lo confirmado nunca se repite. Cada acción espera solo a la anterior de su regla. Si la condición no se cumple, queda «No aplica», que no es un error;
- **la configuración se valida al reservar** y no cuelga nada: un rol borrado da «No se pudo» (`RoleMissing`), y una persona elegida que dejó la organización, `NotMember`. **Un aviso a un rol** sale a quien sigue en el rol al mandarlo: quien se fue queda «No aplica» (`NoLongerInRole`), sin frenar a los demás ni quedar por resolver (239);
- **pedir aprobación a un rol sin personas que puedan decidir,** sin contar a quien cargó ni a quien corrigió salvo con «Puede decidir quien cargó» (276), falla sin abrir tarea (`RoleWithoutMembers`) y queda por resolver. «Reintentar» la vuelve a correr cuando el rol ya tiene a alguien; antes, da 409 (239);
- **lo que quedó en duda** porque la app se cortó mientras corría, como un PDF a medio guardar, se consulta solo, sin reenviar: a los 15 segundos, el doble cada vez, hasta 15 minutos y ocho veces. Después queda «En duda», esperando a una persona, con «Reintentar», que consulta antes y nunca repite lo confirmado. Vale para lo que no sale por una cola (239);
- un destinatario inalcanzable no tumba a los demás. Lo sellado manda: no se copia la respuesta en cada ejecución;
- **«Reintentar»** va por persona en un aviso (158), solo si WhatsApp rechazó su aviso o no lo pudo mandar después de sus dos reintentos: reabre el aviso solo para ella, que vuelve a «En curso» y, al terminar, a «Hecho». No se repite a quienes lo recibieron ni vuelve a correr la acción siguiente de la regla. Va por acción en un pedido de aprobación que falló por el rol y en lo que quedó «En duda». **La ejecución guarda quién pidió el último reintento y cuándo** (`lastRequest`), que la actividad muestra (239);
- **lo que no se sabe si salió, por WhatsApp o por correo, queda «Sin confirmar»** (240, que cambia la 80; [ADR 0066](../../decisions/0066-lo-que-no-se-sabe-si-salio.md) §7 y la 281): la cola no lo repite. Por WhatsApp lo revisa en su bandeja cualquiera que puede enviar en ella, con quién y cuándo, y revisar no reenvía ni dice que llegó; revisado, deja de estar por resolver, y mandarlo otra vez es «Reintentar». **Un correo no tiene bandeja:** «Reintentar» va en el ⋮ de su renglón para quien tiene «Administrar reglas y PDF», pide confirmar («Puede que ya le haya llegado. ¿Mandarlo otra vez?») y, mandado, deja de estar por resolver. Lo que seguro no salió se reintenta solo, dos veces como mucho;
- **por resolver** (228): una ejecución lo está cuando quedó «No se pudo» sin reintentarse sola, «En duda» o «Sin confirmar» esperando a una persona, o es un aviso fuera de las 24 horas sin plantilla (61). Es una sola regla del dominio (`FormActionExecution.Unresolved`), que usan la planilla, su cuenta y el aviso a quien cargó;
- **la traza:** cada ejecución guarda al crearse el contexto W3C de donde se escribió (`TraceParent`, opcional): la captura, la decisión o la corrección. El worker lo continúa en una actividad del `ActivitySource` del módulo, con etiquetas sin valores (el id y el tipo de acción); sus logs llevan el id de la ejecución, y una purga registra solo el tipo de la excepción.

## Los avisos y los correos

- Forms los pide por un puerto neutral, y WhatsApp los encola en su misma cola, sin duplicados por ejecución y destinatario;
- la ventana de 24 horas se mira al aceptar y al despachar. **Fuera, todos van con una plantilla aprobada de la organización,** que elige la regla (61). Sin plantilla, queda «No se pudo», a la vista;
- las plantillas suman **un encabezado con documento,** para mandar el PDF, y la regla llena cada variable con la lista de «Agregar dato» (149): `{number}`, `{reason}` al rechazar o una pregunta, sin el bloque ni la ubicación, porque una variable no admite saltos de línea (197 y 230);
- **lo que contesta sale antes** (H31 de allá, en la ADR 0066): lo que se le contesta a alguien (el operador, el asistente, la confirmación de un toque) sale antes que los avisos de las reglas, y dentro de un chat se respeta el orden;
- **el PDF a un cliente** (47 y 55) va al chat del contacto elegido en una pregunta de un catálogo «De los contactos». Sin contacto, «No se pudo»;
- **avisar a una persona:** la regla elige un miembro por id, y se valida al reservar;
- **quien cargó puede ser un cliente:** sigue la misma regla. **Quien cargó desde la web puede no tener dónde recibir** (89): queda «No aplica», y lo decide el canal.

**Quien cargó se entera si su regla se frena** (237): cuando una acción queda por resolver, a quien cargó le llega un aviso, una vez por acción, por WhatsApp con el número de la organización, en el idioma de su cuenta o en el de la organización si no tiene, con el número y el formulario y sin valores: «Tu respuesta PED-000012 de «Pedido de materiales» quedó frenada. Ya le avisamos a quien la administra.» («Your response PED-000012 to “Pedido de materiales” is on hold. We've already told whoever manages it.»). No usa plantillas: con la ventana de 24 horas cerrada, o si cargó desde la web sin chat (89), queda «No aplica» y la acción se marca sin avisar. La clave del mensaje sale de la ejecución, así reintentar, consultar o dos workers no lo repiten; si el envío falla, la ejecución sigue sin marca y vuelve en la vuelta siguiente del worker.

**Mandar por correo** (62): a quien cargó, a un rol o a una persona, al correo de su cuenta; a una dirección fija, o al correo de una pregunta. Lleva el texto y, si se elige, el PDF. El asunto lo arma la app, «{formulario} · {número}» («Visita a cliente · VIS-000001»), sin campo en la regla: allá también lo arma la app (`FormIssueCopyService`). Se arma con la plantilla de correos del núcleo (`IEmailTemplateRenderer.RenderNotice`), con el nombre y el logo de la página pública de la organización, como la invitación, y el nombre de la aplicación y el remitente de Plataforma › Configuración (273); sin página pública, va con el nombre. Sale por la cola de correos del núcleo, que suma los adjuntos; cada destinatario guarda el id del envío (`OutboxMessageId`), y su estado se deriva de la cola: lo que no pudo mandar queda «No se pudo», y lo que no se sabe si salió, «Sin confirmar». Como los demás avisos, no cuenta en los topes de correo pero gasta el cupo, que el tope deja con margen (0063 y 256; allá, igual). Un cliente sin correo queda «No se pudo». Las direcciones nunca van completas a los logs.

## El PDF (59, 82, 106, 236 y 263)

- un diseño con revisiones propias, con «Guardar diseño»: nombre, tres estilos, que se eligen con la pieza (262), color, encabezado, secciones con título y orden, mostrar u ocultar preguntas, «Incluir las fotos», quién aprobó, hasta 3 firmas y pie. Sin editor libre (59);
- el logo y los datos salen de la página pública, y el color arranca con el del sistema (82). Sin página pública, va sin logo y con el nombre;
- muestra los campos «Dato», el valor de un catálogo tipado, las fotos y los cálculos. Lo que una condición ocultó no va, como en la respuesta (266 y 269). Cada dato de la opción de un catálogo va en su renglón, «Pregunta · Dato», y en el bloque, en su columna (263). «Incluir las fotos» sin marcar dice cuántas hay;
- **el bloque entero:** el diseño lo elige con su nombre, no sus preguntas, y el PDF lo pone como título de una tabla, con el número del ítem y una columna por pregunta, que sigue en la página siguiente con su encabezado si no entra. Sin ítems, «Sin respuesta». La vista previa suma dos ítems de ejemplo;
- al armarlo se fija una **copia fija** A4 en cada idioma del sistema, español e inglés, y cada una se escribe una sola vez. Cambiar reglas, catálogos o el diseño no la altera. A cada persona le llega, y la ve, en el idioma de su cuenta (42); a quien no tiene cuenta, como un cliente o una dirección de correo, en el de la organización;
- **las horas** van en la zona de la cuenta de quien cargó, con su ciudad: «17/11/2026, 18:30 (Madrid)»; a un cliente sin cuenta le va la de la organización, y una corrección conserva la de quien cargó (236);
- **una «Ubicación»** va con el punto y, debajo, «Ver en el mapa», un enlace a Google Maps; la vista previa usa un punto de ejemplo (230);
- solo se ve por la Api, con `formrecords.read` o, en lo propio, con `formrecords.create`, con `<object>` y descarga como alternativa.

## El chat de WhatsApp en «Mi cuenta» (159 y 170)

Debajo de los medios de ingreso, una zona «Chat de WhatsApp» con cada organización de la persona: «Vinculado», «Sin vincular», con «Vincular» en el ⋮ de su fila (261), «Sin chat todavía», con el enlace «Abrir el chat», o «Suspendida» si la organización está suspendida. Con el chat vinculado, los avisos dicen «Llegan hasta el …», con su (i), y, pasadas las 24 horas, «Solo con plantilla», con la suya: fuera de esa ventana llega solo un aviso con una plantilla aprobada de la organización (61), y escribir al chat la vuelve a abrir. Sin vincular, sin chat o con la organización suspendida, «No llegan». Vincular confirma el número con un código, como hoy, y «Verificado» sigue siendo entrar con el número. Lo da el módulo de WhatsApp; apagado, la zona no está.

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /{id}/rules` | `formrules.manage` | — | `{revision, rules[], questions[]}`: un mensaje guarda cada dato como `{FieldId}`, `{number}` o `{reason}`, y `questions[]` trae los nombres de hoy para las etiquetas. Las preguntas del bloque van debajo de él, con `rowsOf`: las condiciones con valor sobre ellas las cumple un mismo ítem, y en el mensaje el bloque es un solo dato, su `{FieldId}`; una de sus preguntas no es un dato. «Pedir aprobación» suma `submitterMayDecide` (false) y `rejectReasonRequired` (true), y se omiten con su valor por defecto (276) |
| `PUT /{id}/rules` | `formrules.manage`, y `formrecords.read` si un aviso cita preguntas, adjunta el PDF o manda el PDF a un cliente | `{revision, rules[]}` | 200 `{revision}`, 409 si otro guardó; sin «Ver respuestas», 403 `Forms.Rules.RecordsReadRequired` (239) |
| `GET /{id}/rules/revisions` y `…/{revision}` | `formrules.manage` | `page`, `pageSize` | la lista paginada y cada revisión |
| `GET /{id}/rules/options/{roles\|members\|templates}` | `formrules.manage` | `page`, `pageSize`, `search` | `PagedResult` de cada una, sin correos |
| `GET` y `PUT /{id}/pdf-design`, `POST /{id}/pdf-design/preview` | `formrules.manage` | `{design, revision}` | el diseño, su revisión, `organization` y un PDF de ejemplo, que no guarda |
| `GET /pending` | `formapprovals.decide` | `page`, `pageSize`, `search` | `PagedResult<{id, name, pending}>` |
| `GET /{id}/approvals`, `…/{taskId}` y `…/{taskId}/continuation` | `formapprovals.decide` | `page`, `pageSize`; `direction` y `limit` | la cola, sin valores: `{taskId, recordId, displayNumber, submittedAtUtc, roleName, respondent?}`, de la más vieja a la más nueva; `respondent` es el `{kind, name?}` de la E15, que da «Cargó» y «Es cliente» (206), y es `null` sin `formrecords.read` |
| `POST /{id}/records/{recordId}/decision` | `formapprovals.decide` | `{decision, reason?, version}`, con `[Idempotent]`; `reason` es obligatorio al rechazar si la tarea lo exige (276) | 200 `{version, state}` |
| `GET /{id}/records/{recordId}/workflow` | `formrecords.read` | — | `{ruleRevision?, approval?, activity[], pdf?}`; `approval.canDecide` dice si quien mira tiene el permiso y el rol de la tarea, y solo entonces el detalle la pide. Cada acción suma `lastRequest?{kind, by, atUtc}` y `canRetry`, y cada destinatario «Sin confirmar», `review?{by, atUtc}` (239 y 240; un correo no lo lleva, porque no tiene bandeja). Para los renglones (267): `approval` suma `reasonRequired`; cada «Armar el PDF» confirmado, `documentId`, y un aviso a un rol, `roleName`, el nombre actual del rol |
| `POST …/executions/{executionId}/recipients/{recipientId}/retry` | `formrules.manage` | `{version}`, la revisión del aviso, con `[Idempotent]`; si otra persona ya lo reintentó, 409 | 200; la actividad suma `canRetry` en cada destinatario, sea una cuenta, un contacto o una dirección (158) |
| `POST …/executions/{executionId}/retry` | `formrules.manage` | `{version}`, con `[Idempotent]`: un pedido de aprobación con `RoleWithoutMembers` o una acción «En duda»; si no se puede, 409 `Forms.Execution.NotRetryable` | 200 `{version}` (239) |
| `GET /api/me/whatsapp/chats` | la cuenta, con el módulo de WhatsApp | `page`, `pageSize` | 200 `PagedResult<{organizationId, organizationName, organizationStatus, link, noticesUntilUtc?}>`, como `GET /api/me/whatsapp/conversations`: `organizationStatus` es `Active` o `Suspended`, y `link`, `Linked`, `Unlinked` o `NoChat` (159). Pasado `noticesUntilUtc`, la zona dice «Solo con plantilla» (170) |
| `POST /api/me/whatsapp/chats/{linkId}/code` y `…/link` | lo mismo | `[Idempotent]`; como `…/login-methods/{id}/code` y `/verify`; el código cuenta en el tope diario por cuenta (H10) | 202 `{resendAfterSeconds}`; `link` devuelve la fila |
| `GET /{id}/records/{recordId}/pdf` | `formrecords.read` | — | 200 PDF en el idioma de la cuenta de quien lo pide, `no-store`; con `documentId?`, el de ese renglón de la actividad (267) |
| `GET /{id}/records/{recordId}/history` | `formrecords.read` | `page`, `pageSize` | `PagedResult<{id, correctedAtUtc?, correctedBy?, reason?, superseded}>`, de la más nueva a la más vieja |
| `GET /{id}/records/{recordId}/correction` | `formrecords.correct` | — | la versión activa, con lo cargado, los ítems y los archivos que ya estaban, `{fileId, name, byteLength}`, para «Ya cargado · 86 KB»; la pide la misma pantalla al empezar a corregir en el lugar (266) |
| `POST /{id}/records/{recordId}/corrections` | `formrecords.correct` | multipart: el de la E20, más `reason`, `version` y `keepFileIds[]`, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]` y `[IdempotencyDurable(typeof(FormEntryDurableAdapter))]` | 200 `{recordId, displayNumber, submittedAtUtc, calculations[], version}`, con `[ResponseVersion("Version")]` |
| `POST /{id}/records/{recordId}/void` | `formrecords.correct` | `{reason, version}` | 200 `{version}` |
| `…/own-records/{recordId}/pdf`, `history`, `correction`, `corrections` y `void` | `formrecords.create` | lo mismo | lo mismo, solo con lo propio y mientras se pueda |

**Rutas que cambian:** `GET /` y `GET /overview` suman `pending?` (`null` sin `formapprovals.decide`), `status=pendingApproval`, «Con algo por aprobar» (275), y `sort=pending` (264). Sin `formapprovals.decide`, ese filtro y ese orden dan 403; el filtro trae los activos y los dados de baja, y su predicado, uno solo, cuenta también `pending`, `GET /overview`, «Para resolver» y la campana. `recordCount` cuenta las vigentes sin anular. `POST …/records/search`, `facets`, `navigation`, `export` y `dashboard` suman el filtro `state`, con `Voided`, y `includeVoided` en el Excel, y cada fila de `search` suma `state`, `corrected` y `hasPdf`, la marca de la copia PDF confirmada, también en lo propio; `sort` suma `state`, por el circuito, `corrected` y `unresolvedActions` (264). **Por resolver** (228): las mismas suman el filtro `unresolvedActions` (`true`, solo esas; `false`, las otras), cada fila suma `hasUnresolvedActions`, omitido en falso, también en lo propio, y `GET /{id}/records/columns` suma `unresolvedActionCount?`, solo para quien tiene `formrules.manage` y ve todas las respuestas, y solo si alguna ya corrió una acción. El Excel lleva «Estado» entre sus columnas fijas. El detalle suma `state`, `corrected`, `correctedBy?{name, atUtc}`, `supersededBy?`, `voided?`, `canCorrect`, `canVoid` y `version`, y el de lo propio, `rejection?{reason?, atUtc}`, sin quién decidió, porque lo propio no tiene `workflow`. `GET /{id}/records/columns` suma `approvals`.

**Puertos neutrales:**

```csharp
// Forms → WhatsApp (implementa WhatsApp)
public interface IStructuredRecordActionChannel
{
    Task<PagedResult<StructuredActionTemplate>> ListTemplatesAsync(PagedRequest page, CancellationToken ct);
    Task<StructuredActionReach> GetReachAsync(StructuredRecipient recipient, CancellationToken ct);
    Task<Result<StructuredActionReceipt>> SendAsync(StructuredActionMessage message, CancellationToken ct);
    Task<IReadOnlyList<StructuredActionState>> ReadStatesAsync(IReadOnlyCollection<Guid> keys, CancellationToken ct);
}
```

`StructuredActionMessage(Key, Recipient, Text, Template?, Document?)`. `Key` es el id de ejecución y destinatario. `ReadStatesAsync` suma «Sin confirmar» y quién lo revisó (240). La zona y el idioma de una cuenta llegan por `IMemberDirectoryReader`, y los de la organización, por `ITenantSettingsReader`: el módulo no lee `ApplicationUser` (236). Sin WhatsApp, `GetReachAsync` dice que no hay canal y `ListTemplatesAsync` devuelve una lista vacía. El correo usa `IOutbox`, con el canal de correos. `IStructuredRecordSource.ReadAsync` lee por id, también las reemplazadas, y `StructuredRecordCard` suma `Corrected` y `Voided`, con el número, para la marca al pie de la tarjeta (166).

**La campana:** Forms aporta `formApprovals`, a quien puede decidir, y `ruleNoticeFailed`, a quien tiene `formrules.manage`, por `INotificationSource` (base de la 12).

**Errores**, en `FormRuleErrors.cs`, `FormPdfDesignErrors.cs`, `FormApprovalErrors.cs` y `FormActionExecutionErrors.cs`, nuevos, y `FormRecordErrors.cs` [E14], con sus textos en `FormsTexts.resx` y `.en.resx`; `WhatsApp.ChatLink.NotFound`, en `D/WhatsApp/WhatsAppErrors.cs` y `WhatsAppTexts.resx`:

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Rules.Invalid` | Validation (`rules[i]…`) | un evento, condición, acción, rol, persona, plantilla o pregunta fuera de regla, o el orden de las acciones |
| `Forms.Rules.TooMany` | Validation (`rules`) | más de 20 reglas, 10 condiciones o 10 acciones |
| `Forms.Rules.OptionUnavailable` | Validation (`rules[i].conditions[j]`) | una condición nombra una opción que ya no está en la versión ni en su catálogo; el texto la nombra |
| `Forms.Rules.RecordsReadRequired` | Forbidden | guardar una regla cuyo aviso cita preguntas, adjunta el PDF o manda el PDF a un cliente, sin «Ver respuestas» |
| `Forms.Rules.PlaceholderInvalid` | Validation (`rules[i].actions[j]…`) | un dato del mensaje o de una variable que no es el número, el motivo al rechazar, una pregunta de la versión o el bloque: una pregunta que ya no está o una del bloque |
| `Forms.Rules.RevisionNotFound` | NotFound | una revisión de las reglas que no existe |
| `Forms.PdfDesign.Invalid` | Validation | nombre, pie, firmas o preguntas fuera de regla |
| `Forms.Approval.NotFound` | NotFound | la tarea no existe o no se puede ver |
| `Forms.Approval.AlreadyDecided` | Conflict | otro decidió antes |
| `Forms.Approval.OwnRecord` | Forbidden | la cargó o la corrigió quien decide, sin «Puede decidir quien cargó» (276) |
| `Forms.Approval.RoleRequired` | Forbidden | no tiene el rol de la tarea |
| `Forms.Approval.ReasonRequired` | Validation (`reason`), 400 | rechazar sin motivo cuando la tarea lo exige (276) |
| `Forms.Record.NotCorrectable` | Conflict | ya aprobada, para quien solo la cargó, o anulada |
| `Forms.Record.Superseded` | Conflict | corregir o anular una que ya se reemplazó |
| `Forms.Record.AlreadyVoided` | Conflict | ya está anulada |
| `Forms.Record.ReasonRequired` | Validation (`reason`) | corregir o anular sin motivo |
| `Forms.Record.ReasonTooLong` | Validation (`reason`) | un motivo de más de 500 caracteres |
| `Forms.Record.PdfNotFound` | NotFound | no hay copia, o falta el archivo |
| `Forms.Execution.NotRetryable` | Conflict | una persona sin la falla confirmada, o una acción que todavía no se puede reintentar |
| `WhatsApp.ChatLink.NotFound` | NotFound | un chat de «Mi cuenta» que no es de la persona |

**Fallas de una ejecución,** que se ven en Actividad, con sus textos (es / en): `WindowClosed` «Pasaron 24 horas y no hay plantilla.» / "24 hours passed and there is no template."; `TemplateUnavailable` «La plantilla no está aprobada o no lleva el PDF.» / "The template isn't approved or doesn't carry the PDF."; `NoConversation` «WhatsApp no aceptó el mensaje: no tiene el WhatsApp vinculado.» / "WhatsApp didn't accept the message: their WhatsApp isn't linked."; `NoContact` «No se eligió un contacto.» / "No contact was chosen."; `NotMember` «Ya no es miembro.» / "No longer a member."; `RoleMissing` «El rol ya no existe.» / "The role no longer exists."; `RoleWithoutMembers` «Nadie del rol {0} puede decidirla.» / "Nobody in the {0} role can decide it."; `NoLongerInRole` «Ya no tiene el rol {0}.» / "No longer has the {0} role."; `NoEmail` «No tiene correo.» / "No email."; `NoAnswer` «La pregunta no tiene respuesta.» / "The question has no answer."; `NoPdf` «No hay PDF para adjuntar.» / "There's no PDF to attach.". `NoLongerInRole` es el motivo de «No aplica» de quien dejó el rol.

**Permisos nuevos:** `formrules.manage`, `formapprovals.decide` y `formrecords.correct`, con sus textos, sus áreas «Reglas y PDF» y «Aprobaciones» en los permisos de un rol (257) y la migración de los roles de sistema. `formrecords.correct` incluye `formrecords.read` (260); los otros dos no incluyen nada.

## Datos

Seis migraciones: `<fecha>_FormRules.cs`, `<fecha>_FormRecordCorrections.cs` y `<fecha>_SystemRoleFormRulesPermissions.cs` (B2), y en WhatsApp, `<fecha>_WhatsAppChatMessageOrigin.cs` (B9), `<fecha>_WhatsAppTemplateHeader.cs` (B10) y `<fecha>_WhatsAppFormActions.cs` (B17), en ese orden. Los valores nuevos (los estados de `FormRecords`, de la tarea y de las ejecuciones, el tipo `Email` y `Linked` y `Unlinked`) se agregan y se escriben en el mismo despliegue: es la excepción de la [0060](../../decisions/0060-dos-versiones-conviven.md), anotada en cada migración, porque todavía no hay despliegue (123). Todo es `ITenantOwned`, esquema `tenant`, con RLS, salvo el registro de trabajo.

| Tabla | Qué guarda | Regla |
| --- | --- | --- |
| **FormRuleSets** | `DefinitionId`, `Revision`, `Rules` jsonb, quién y cuándo | inmutable; único por los dos |
| **FormPdfDesigns** | `DefinitionId`, `Revision`, `Design` jsonb, quién y cuándo | inmutable; único por los dos |
| **FormApprovalTasks** (`IVersioned`) | `RecordId`, `DefinitionId`, `RoleId`, `State`, `RequestedAtUtc`, `SubmitterMayDecide`, `RejectReasonRequired`, decisión, motivo? | las dos casillas, copiadas al abrirse de la revisión con que llegó la respuesta (276); una abierta por respuesta, con índice único filtrado; índice por definición, rol, fecha e id |
| **FormActionExecutions** | `RecordId`, `DefinitionId`, `RuleSetId`, `Event`, `RuleId`, `ActionId`, `Position`, `Kind`, `State`, `FailureCode?`, `Fence`, `LeaseExpiresAtUtc`, `NextAttemptAtUtc`, `ConsultAttempts`, `StallNoticedAtUtc?`, `LastRequestKind?`, `LastRequestedByUserId?`, `LastRequestedAtUtc?` y `TraceParent?` | único por respuesta, evento, regla y acción, que sirve al `EXISTS` de la marca; índice `(TenantId, DefinitionId, RecordId)` para la cuenta de «Por resolver» |
| **FormActionRecipients** | `ExecutionId`, `Kind`, `UserId?`, `ContactId?`, `Address?` cifrada, `State` (con «Sin confirmar»), `FailureCode?`, `RenderedText?`, `OutboxMessageId?` en un correo | uno por destinatario; el error de uno no toca a los otros; quién revisó lo «Sin confirmar» lo guarda WhatsApp, por el id del envío |
| **FormRecordDocuments** | `RecordId`, `ExecutionId`, `DesignId`, `StorageKey`, `FileName`, `ByteLength`, `Sha256`, `Culture`, `TimeZoneId` | único por ejecución e idioma; cada uno se escribe una vez, con la zona de quien cargó (236) |
| **platform.FormActionWorks** | `TenantId`, `NextAtUtc`, `LeaseExpiresAtUtc` | registro técnico global, sin datos de negocio |

**Columnas nuevas:**
- `FormDefinitionStates`: `ActiveRuleSetId` y `ActivePdfDesignId`, por T7;
- `FormRecords`: `RuleSetId?`, `State` (`Received` para las que ya existen), `Version` (`IVersioned`), `CorrectionOfRecordId?`, `CorrectionReason?`, `CorrectedByUserId?`, `CorrectedAtUtc?`, `SupersededAtUtc?`, y `VoidedAtUtc?`, `VoidedByUserId?` y `VoidReason?`. **El índice único `(DefinitionId, Number)` pasa a filtrar `SupersededAtUtc IS NULL`,** y la planilla, los conteos, la navegación, el Excel y el tablero filtran las vigentes;
- en WhatsApp, las plantillas suman `HeaderFormat` (B10), y el mensaje de chat, el origen «acción de formulario» (B9).
