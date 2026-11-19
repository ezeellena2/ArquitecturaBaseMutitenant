# E21 · Reglas, aprobación, PDF y avisos: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E21](21-reglas-y-aprobacion.md), que dice qué leer y cómo se cierra.

## El motor de acciones

- cada respuesta conserva la revisión con la que llegó, y la captura crea sus ejecuciones en el mismo commit;
- cada acción es una ejecución, con clave respuesta + evento + regla + acción, reserva y número de turno. El efecto va fuera de la transacción. **El worker reserva solo los tipos de acción que conoce:** uno de una versión más nueva espera, sin reservarse ni fallar, porque conviven dos versiones ([ADR 0060](../../decisions/0060-dos-versiones-conviven.md));
- lo confirmado nunca se repite. Cada acción espera solo a la anterior de su regla. Si la condición no se cumple, queda «No aplica», que no es un error;
- **la configuración se valida al reservar** y no cuelga nada: un rol borrado da «No se pudo» (`RoleMissing`), y una persona elegida que dejó la organización, `NotMember`. **Un aviso a un rol** sale a quien sigue en el rol al mandarlo: quien se fue queda «No aplica» (`NoLongerInRole`), sin frenar a los demás ni quedar por resolver (239);
- **pedir aprobación a un rol sin personas que puedan decidir,** sin contar a quien cargó ni a quien corrigió, falla sin abrir tarea (`RoleWithoutMembers`) y queda por resolver. «Reintentar» la vuelve a correr cuando el rol ya tiene a alguien; antes, da 409 (239);
- **lo que quedó en duda** porque la app se cortó mientras corría, como un PDF a medio guardar, se consulta solo, sin reenviar: a los 15 segundos, el doble cada vez, hasta 15 minutos y ocho veces. Después queda «En duda», esperando a una persona, con «Reintentar», que consulta antes y nunca repite lo confirmado. Vale para lo que no sale por una cola (239);
- un destinatario inalcanzable no tumba a los demás. Lo sellado manda: no se copia la respuesta en cada ejecución;
- **«Reintentar»** va por persona en un aviso (158), solo si WhatsApp rechazó su aviso o no lo pudo mandar después de sus dos reintentos: reabre el aviso solo para ella, que vuelve a «En curso» y, al terminar, a «Hecho». No se repite a quienes lo recibieron ni vuelve a correr la acción siguiente de la regla. Va por acción en un pedido de aprobación que falló por el rol y en lo que quedó «En duda». **La ejecución guarda quién pidió el último reintento y cuándo** (`lastRequest`), que la actividad muestra (239);
- **lo que no se sabe si salió por WhatsApp queda «Sin confirmar»** (240, que cambia la 80; [ADR 0066](../../decisions/0066-lo-que-no-se-sabe-si-salio.md)): la cola no lo repite; lo revisa en su bandeja cualquiera que puede enviar en ella, con quién y cuándo, y revisar no reenvía ni dice que llegó. Revisado, deja de estar por resolver, y mandarlo otra vez es «Reintentar». Lo que seguro no salió se reintenta solo, dos veces como mucho;
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

**Mandar por correo** (62): a quien cargó, a un rol o a una persona, al correo de su cuenta; a una dirección fija, o al correo de una pregunta. Lleva el texto y, si se elige, el PDF. Sale por la cola de correos del núcleo, que suma los adjuntos. Un cliente sin correo queda «No se pudo». Las direcciones nunca van completas a los logs.

## El chat de WhatsApp en «Mi cuenta» (159 y 170)

Debajo de los medios de ingreso, una zona «Chat de WhatsApp» con cada organización de la persona: «Vinculado», «Sin vincular» con «Vincular», «Sin chat todavía» con «Abrir el chat», o «Suspendida» si la organización está suspendida. Con el chat vinculado, los avisos dicen «Llegan hasta el …», con su (i), y, pasadas las 24 horas, «Solo con plantilla», con la suya: fuera de esa ventana llega solo un aviso con una plantilla aprobada de la organización (61), y escribir al chat la vuelve a abrir. Sin vincular, sin chat o con la organización suspendida, «No llegan». Vincular confirma el número con un código, como hoy, y «Verificado» sigue siendo entrar con el número. Lo da el módulo de WhatsApp; apagado, la zona no está.

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /{id}/rules` | `formrules.manage` | — | `{revision, rules[], questions[]}`: un mensaje guarda cada dato como `{FieldId}`, `{number}` o `{reason}`, y `questions[]` trae los nombres de hoy para las etiquetas. Las preguntas del bloque van debajo de él, con `rowsOf`: las condiciones con valor sobre ellas las cumple un mismo ítem, y en el mensaje el bloque es un solo dato, su `{FieldId}`; una de sus preguntas no es un dato |
| `PUT /{id}/rules` | `formrules.manage`, y `formrecords.read` si un aviso cita preguntas, adjunta el PDF o manda el PDF a un cliente | `{revision, rules[]}` | 200 `{revision}`, 409 si otro guardó; sin «Ver respuestas», 403 `Forms.Rules.RecordsReadRequired` (239) |
| `GET /{id}/rules/revisions` y `…/{revision}` | `formrules.manage` | `page`, `pageSize` | la lista paginada y cada revisión |
| `GET /{id}/rules/options/{roles\|members\|templates}` | `formrules.manage` | `page`, `pageSize`, `search` | `PagedResult` de cada una, sin correos |
| `GET` y `PUT /{id}/pdf-design`, `POST /{id}/pdf-design/preview` | `formrules.manage` | `{design, revision}` | el diseño, su revisión, `organization` y un PDF de ejemplo, que no guarda |
| `GET /pending` | `formapprovals.decide` | `page`, `pageSize`, `search` | `PagedResult<{id, name, pending}>` |
| `GET /{id}/approvals`, `…/{taskId}` y `…/{taskId}/continuation` | `formapprovals.decide` | `page`, `pageSize`; `direction` y `limit` | la cola, sin valores: `{taskId, recordId, displayNumber, submittedAtUtc, roleName, respondent?}`, de la más vieja a la más nueva; `respondent` es el `{kind, name?}` de la E15, que da «Cargó» y «Es cliente» (206), y es `null` sin `formrecords.read` |
| `POST /{id}/records/{recordId}/decision` | `formapprovals.decide` | `{decision, reason?, version}`, con `[Idempotent]` | 200 `{version, state}` |
| `GET /{id}/records/{recordId}/workflow` | `formrecords.read` | — | `{ruleRevision?, approval?, activity[], pdf?}`; `approval.canDecide` dice si quien mira tiene el permiso y el rol de la tarea, y solo entonces el detalle la pide. Cada acción suma `lastRequest?{kind, by, atUtc}` y `canRetry`, y cada destinatario «Sin confirmar», `review?{by, atUtc}` (239 y 240) |
| `POST …/executions/{executionId}/recipients/{recipientId}/retry` | `formrules.manage` | `{version}`, la revisión del aviso, con `[Idempotent]`; si otra persona ya lo reintentó, 409 | 200; la actividad suma `canRetry` en cada destinatario, sea una cuenta, un contacto o una dirección (158) |
| `POST …/executions/{executionId}/retry` | `formrules.manage` | `{version}`, con `[Idempotent]`: un pedido de aprobación con `RoleWithoutMembers` o una acción «En duda»; si no se puede, 409 `Forms.Execution.NotRetryable` | 200 `{version}` (239) |
| `GET /api/me/whatsapp/chats` | la cuenta, con el módulo de WhatsApp | `page`, `pageSize` | 200 `PagedResult<{organizationId, organizationName, organizationStatus, link, noticesUntilUtc?}>`, como `GET /api/me/whatsapp/conversations`: `organizationStatus` es `Active` o `Suspended`, y `link`, `Linked`, `Unlinked` o `NoChat` (159). Pasado `noticesUntilUtc`, la zona dice «Solo con plantilla» (170) |
| `GET /{id}/records/{recordId}/pdf` | `formrecords.read` | — | 200 PDF en el idioma de la cuenta de quien lo pide, `no-store` |
| `GET /{id}/records/{recordId}/history` | `formrecords.read` | `page`, `pageSize` | `PagedResult<{id, correctedAtUtc?, correctedBy?, reason?, superseded}>`, de la más nueva a la más vieja |
| `GET /{id}/records/{recordId}/correction` | `formrecords.correct` | — | la versión activa, con lo cargado, los ítems y los archivos que ya estaban, `{fileId, name, byteLength}`, para «Ya cargado · 86 KB» |
| `POST /{id}/records/{recordId}/corrections` | `formrecords.correct` | multipart: el de la E20, más `reason`, `version` y `keepFileIds[]`, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]` y `[IdempotencyDurable(typeof(FormEntryDurableAdapter))]` | 200 `{recordId, displayNumber, submittedAtUtc, calculations[], version}`, con `[ResponseVersion("Version")]` |
| `POST /{id}/records/{recordId}/void` | `formrecords.correct` | `{reason, version}` | 200 `{version}` |
| `…/own-records/{recordId}/pdf`, `history`, `correction`, `corrections` y `void` | `formrecords.create` | lo mismo | lo mismo, solo con lo propio y mientras se pueda |

**Rutas que cambian:** `GET /` y `GET /overview` suman `pending?` (`null` sin `formapprovals.decide`), el filtro `approval` y el orden; `recordCount` cuenta las vigentes sin anular. `POST …/records/search`, `facets`, `navigation`, `export` y `dashboard` suman el filtro `state`, con `Voided`, y `includeVoided` en el Excel, y cada fila de `search` suma `state`, `corrected` y `hasPdf`, la marca de la copia PDF confirmada, también en lo propio. **Por resolver** (228): las mismas suman el filtro `unresolvedActions` (`true`, solo esas; `false`, las otras), cada fila suma `hasUnresolvedActions`, omitido en falso, también en lo propio, y `GET /{id}/records/columns` suma `unresolvedActionCount?`, solo para quien tiene `formrules.manage` y ve todas las respuestas, y solo si alguna ya corrió una acción. El Excel lleva «Estado» entre sus columnas fijas. El detalle suma `state`, `corrected`, `correctedBy?{name, atUtc}`, `supersededBy?`, `voided?`, `canCorrect`, `canVoid` y `version`, y el de lo propio, `rejection?{reason, atUtc}`, sin quién decidió, porque lo propio no tiene `workflow`. `GET /{id}/records/columns` suma `approvals`.

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

**Errores** (con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Rules.Invalid` | Validation (`rules[i]…`) | un evento, condición, acción, rol, persona, plantilla o pregunta fuera de regla, o el orden de las acciones |
| `Forms.Rules.TooMany` | Validation (`rules`) | más de 20 reglas, 10 condiciones o 10 acciones |
| `Forms.Rules.OptionUnavailable` | Validation (`rules[i].conditions[j]`) | una condición nombra una opción que ya no está en la versión ni en su catálogo |
| `Forms.Rules.RecordsReadRequired` | Forbidden | guardar una regla cuyo aviso cita preguntas, adjunta el PDF o manda el PDF a un cliente, sin «Ver respuestas» |
| `Forms.Rules.PlaceholderInvalid` | Validation (`rules[i].actions[j]…`) | un dato del mensaje o de una variable que no es el número, el motivo al rechazar, una pregunta de la versión o el bloque: una pregunta que ya no está o una del bloque |
| `Forms.PdfDesign.Invalid` | Validation | nombre, pie, firmas o preguntas fuera de regla |
| `Forms.Approval.NotFound` | NotFound | la tarea no existe o no se puede ver |
| `Forms.Approval.AlreadyDecided` | Conflict | otro decidió antes |
| `Forms.Approval.OwnRecord` | Forbidden | la cargó o la corrigió quien decide |
| `Forms.Approval.RoleRequired` | Forbidden | no tiene el rol de la tarea |
| `Forms.Approval.ReasonRequired` | Validation (`reason`) | rechazar sin motivo |
| `Forms.Record.NotCorrectable` | Conflict | ya aprobada, para quien solo la cargó, o anulada |
| `Forms.Record.Superseded` | Conflict | corregir o anular una que ya se reemplazó |
| `Forms.Record.AlreadyVoided` | Conflict | ya está anulada |
| `Forms.Record.ReasonRequired` | Validation (`reason`) | corregir o anular sin motivo |
| `Forms.Record.ReasonTooLong` | Validation (`reason`) | un motivo de más de 500 caracteres |
| `Forms.Record.PdfNotFound` | NotFound | no hay copia, o falta el archivo |
| `Forms.Execution.NotRetryable` | Conflict | una persona sin la falla confirmada, o una acción que todavía no se puede reintentar |

**Fallas de una ejecución,** que se ven en Actividad: `WindowClosed`, `TemplateUnavailable`, `NoConversation`, `NoContact`, `NotMember`, `RoleMissing`, `RoleWithoutMembers`, `NoEmail`, `NoAnswer` y `NoPdf`; y el motivo de «No aplica» de quien dejó el rol, `NoLongerInRole`.

**Permisos nuevos:** `formrules.manage`, `formapprovals.decide` y `formrecords.correct`, con sus textos y la migración de los roles de sistema.

## Datos

Tres migraciones: `<fecha>_FormRules.cs`, `<fecha>_FormRecordCorrections.cs` y `<fecha>_SystemRoleFormRulesPermissions.cs`. Todo es `ITenantOwned`, esquema `tenant`, con RLS, salvo el registro de trabajo.

| Tabla | Qué guarda | Regla |
| --- | --- | --- |
| **FormRuleSets** | `DefinitionId`, `Revision`, `Rules` jsonb, quién y cuándo | inmutable; único por los dos |
| **FormPdfDesigns** | `DefinitionId`, `Revision`, `Design` jsonb, quién y cuándo | inmutable; único por los dos |
| **FormApprovalTasks** (`IVersioned`) | `RecordId`, `DefinitionId`, `RoleId`, `State`, `RequestedAtUtc`, decisión, motivo | una abierta por respuesta, con índice único filtrado; índice por definición, rol, fecha e id |
| **FormActionExecutions** | `RecordId`, `DefinitionId`, `RuleSetId`, `Event`, `RuleId`, `ActionId`, `Position`, `Kind`, `State`, `FailureCode?`, `Fence`, `LeaseExpiresAtUtc`, `NextAttemptAtUtc`, `ConsultAttempts`, `StallNoticedAtUtc?`, `LastRequestKind?`, `LastRequestedByUserId?`, `LastRequestedAtUtc?` y `TraceParent?` | único por respuesta, evento, regla y acción, que sirve al `EXISTS` de la marca; índice `(TenantId, DefinitionId, RecordId)` para la cuenta de «Por resolver» |
| **FormActionRecipients** | `ExecutionId`, `Kind`, `UserId?`, `ContactId?`, `Address?` cifrada, `State` (con «Sin confirmar»), `FailureCode?`, `RenderedText?` | uno por destinatario; el error de uno no toca a los otros; quién revisó lo «Sin confirmar» lo guarda WhatsApp, por el id del envío |
| **FormRecordDocuments** | `RecordId`, `ExecutionId`, `DesignId`, `StorageKey`, `FileName`, `ByteLength`, `Sha256`, `Culture`, `TimeZoneId` | único por ejecución e idioma; cada uno se escribe una vez, con la zona de quien cargó (236) |
| **platform.FormActionWorks** | `TenantId`, `NextAtUtc`, `LeaseExpiresAtUtc` | registro técnico global, sin datos de negocio |

**Columnas nuevas:**
- `FormDefinitionStates`: `ActiveRuleSetId` y `ActivePdfDesignId`, por T7;
- `FormRecords`: `RuleSetId?`, `State`, `Version` (`IVersioned`), `CorrectionOfRecordId?`, `CorrectionReason?`, `CorrectedByUserId?`, `CorrectedAtUtc?`, `SupersededAtUtc?`, y `VoidedAtUtc?`, `VoidedByUserId?` y `VoidReason?`. **El índice único `(DefinitionId, Number)` pasa a filtrar `SupersededAtUtc IS NULL`,** y la planilla, los conteos, la navegación, el Excel y el tablero filtran las vigentes;
- en WhatsApp, las plantillas suman `HeaderFormat`, y el mensaje de chat, el origen «acción de formulario».

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Un rol borrado colgaba una aprobación:** se valida al reservar.
2. **Chrome bloqueó el visor del PDF** en un iframe con sandbox: va con `<object>`.
3. **El contrato de `origin` del recibo:** la web rechazó una respuesta correcta; cada campo nuevo se exporta con su ejemplo.
4. **Un aviso a un rol se repetía** por una reserva vencida: la clave va por ejecución y destinatario.
5. **Una copia entera de la respuesta por ejecución:** se lee por id.
6. **Lo que va en la versión y lo que se guarda aparte.** En la prueba de allá, la versión salió sin filtros, y las reglas y el PDF no se habían guardado. Las reglas y el diseño tienen su propio Guardar y valen sin publicar, y salir con cambios pregunta antes.
7. **Un error dice qué hacer en cada caso.** «Agregá Armar el PDF…» se leía igual si faltaba la acción o si estaba más abajo.
8. **Nada se desmarca ni se borra sin avisar:** ni al cambiar el tipo, ni al mover o quitar una acción.
9. **Una decisión que no se deshace pide confirmar:** allá se aprobó la siguiente sin querer, porque ↑ ↓ cambian de respuesta con el foco en el panel.
10. **Un aviso que no puede llegar se descubría recién en la actividad.** Allá sale solo por WhatsApp. Acá va con plantilla fuera de las 24 horas (61) o por correo (62), y lo que no salió se ve por persona en Actividad y en la campana.
11. **El detalle no depende de lo que la persona no tiene.** Allá, con el permiso de aprobar y sin el rol de la tarea, el detalle fallaba al pedir la tarea.
12. **Nada vacío a la vista.** Un historial sin revisiones ocupaba una columna fija: va detrás de su botón, apagado (140).
13. **Un dato guardado por nombre se rompía al renombrar.** El mensaje guarda `{FieldId}`, como las condiciones (149).
14. **Una corrección usa los filtros por persona de quien la cargó** (traspaso 7): con los de quien corrige, otra persona vería otras opciones y podría dejar una que el autor no tenía.
15. **El conteo de respuestas no sale del último número:** con anuladas, se cuentan las vigentes.
16. **Una migración que pierde datos al volver se niega a volver:** el `Down` de las correcciones frena si ya hay correcciones, anuladas o tareas cerradas.
17. **Cerrar una corrección con cambios pregunta** («¿Descartar la corrección?», con «Seguir corrigiendo»); sin cambios, cierra.
18. **Un texto de otro espacio de traducciones se declara en `useTranslation`.** Allá, anular desde la pantalla completa mostraba la clave del motivo, y lo encontró el recorrido, no los tests.
19. **Corregir una respuesta con ítems los perdía:** allá la corrección abría solo las preguntas sueltas. Abre y guarda los ítems, que vuelven si el bloque sigue.
20. **Un rol sin personas abría una tarea que nadie podía decidir** (B3 de allá), y un aviso a un rol le llegaba a quien ya se había ido: los dos se miran al reservar y al mandar.
21. **Una acción frenada se descubría abriendo la respuesta** (F2 de allá): la marca «Por resolver», su vista y el aviso a quien cargó salen de la misma regla.
