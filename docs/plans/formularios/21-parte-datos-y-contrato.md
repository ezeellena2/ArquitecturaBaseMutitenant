# E21 · Reglas, aprobación, PDF y avisos: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E21](21-reglas-y-aprobacion.md), que dice qué leer y cómo se cierra.

## El motor de acciones

- cada respuesta conserva la revisión con la que llegó, y la captura crea sus ejecuciones en el mismo commit;
- cada acción es una ejecución, con clave respuesta + evento + regla + acción, reserva y número de turno. El efecto va fuera de la transacción;
- lo confirmado nunca se repite. Cada acción espera solo a la anterior de su regla. Si la condición no se cumple, queda «No aplica», que no es un error;
- la configuración se valida al reservar: un rol borrado o una persona que se fue dan «No se pudo», y no cuelgan nada;
- un destinatario inalcanzable no tumba a los demás. Lo sellado manda: no se copia la respuesta en cada ejecución;
- «Reintentar» retoma solo lo que no salió. Lo que la cola de WhatsApp no sabe si salió lo reintenta ella sola (80).

## Los avisos y los correos

- Forms los pide por un puerto neutral, y WhatsApp los encola en su misma cola, sin duplicados por ejecución y destinatario;
- la ventana de 24 horas se mira al aceptar y al despachar. **Fuera, todos van con una plantilla aprobada de la organización,** que elige la regla (61). Sin plantilla, queda «No se pudo», a la vista;
- las plantillas suman **un encabezado con documento,** para mandar el PDF, y la regla llena cada variable con `{número}`, el nombre del formulario o una pregunta;
- **el PDF a un cliente** (47 y 55) va al chat del contacto elegido en una pregunta de un catálogo «De los contactos». Sin contacto, «No se pudo»;
- **avisar a una persona:** la regla elige un miembro por id, y se valida al reservar;
- **quien cargó puede ser un cliente:** sigue la misma regla. **Quien cargó desde la web puede no tener dónde recibir** (89): queda «No aplica», y lo decide el canal.

**Mandar por correo** (62): a quien cargó, a un rol o a una persona, al correo de su cuenta; a una dirección fija, o al correo de una pregunta. Lleva el texto y, si se elige, el PDF. Sale por la cola de correos del núcleo, que suma los adjuntos. Un cliente sin correo queda «No se pudo». Las direcciones nunca van completas a los logs.

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /{id}/rules` | `formrules.manage` | — | `{revision, rules[], questions[]}`: un mensaje guarda cada dato como `{FieldId}`, `{number}` o `{reason}`, y `questions[]` trae los nombres de hoy para las etiquetas |
| `PUT /{id}/rules` | `formrules.manage` | `{revision, rules[]}` | 200 `{revision}`, 409 si otro guardó |
| `GET /{id}/rules/revisions` y `…/{revision}` | `formrules.manage` | `page`, `pageSize` | la lista paginada y cada revisión |
| `GET /{id}/rules/options/{roles\|members\|templates}` | `formrules.manage` | `page`, `pageSize`, `search` | `PagedResult` de cada una, sin correos |
| `GET` y `PUT /{id}/pdf-design`, `POST /{id}/pdf-design/preview` | `formrules.manage` | `{design, revision}` | el diseño, su revisión, `organization` y un PDF de ejemplo, que no guarda |
| `GET /pending` | `formapprovals.decide` | `page`, `pageSize`, `search` | `PagedResult<{id, name, pending}>` |
| `GET /{id}/approvals`, `…/{taskId}` y `…/{taskId}/continuation` | `formapprovals.decide` | `page`, `pageSize`; `direction` y `limit` | la cola, sin valores: `{taskId, recordId, displayNumber, submittedAtUtc, roleName, respondentName?}`, de la más vieja a la más nueva; `respondentName` es `null` sin `formrecords.read` |
| `POST /{id}/records/{recordId}/decision` | `formapprovals.decide` | `{decision, reason?, version}`, con `[Idempotent]` | 200 `{version, state}` |
| `GET /{id}/records/{recordId}/workflow` | `formrecords.read` | — | `{ruleRevision?, approval?, activity[], pdf?, canCorrect, canVoid}`; `approval.canDecide` dice si quien mira tiene el permiso y el rol de la tarea, y solo entonces el detalle la pide |
| `POST …/executions/{executionId}/retry` | `formrules.manage` | —, con `[Idempotent]` | 200 |
| `GET /{id}/records/{recordId}/pdf` | `formrecords.read` | — | 200 PDF, `no-store` |
| `GET /{id}/records/{recordId}/history` | `formrecords.read` | `page`, `pageSize` | `PagedResult<{id, correctedAtUtc?, correctedBy?, reason?, superseded}>`, de la más nueva a la más vieja |
| `GET /{id}/records/{recordId}/correction` | `formrecords.correct` | — | la versión activa, con lo cargado |
| `POST /{id}/records/{recordId}/corrections` | `formrecords.correct` | multipart: el de la E20, más `reason`, `version` y `keepFileIds[]`, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]` y `[IdempotencyDurable(typeof(FormEntryDurableAdapter))]` | 200 `{recordId, displayNumber, submittedAtUtc, calculations[], version}`, con `[ResponseVersion("Version")]` |
| `POST /{id}/records/{recordId}/void` | `formrecords.correct` | `{reason, version}` | 200 `{version}` |
| `…/own-records/{recordId}/pdf`, `history`, `correction`, `corrections` y `void` | `formrecords.create` | lo mismo | lo mismo, solo con lo propio y mientras se pueda |

**Rutas que cambian:** `GET /` y `GET /overview` suman `pending?` (`null` sin `formapprovals.decide`), el filtro `approval` y el orden; `recordCount` cuenta las vigentes. `POST …/records/search`, `facets`, `navigation`, `export` y `dashboard` suman `state`, con `Voided`, y `includeVoided` en el Excel. El detalle suma `state`, `corrected`, `correctedBy?{name, atUtc}`, `supersededBy?`, `voided?` y `version`. `GET /{id}/records/columns` suma `approvals`.

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

`StructuredActionMessage(Key, Recipient, Text, Template?, Document?)`. `Key` es el id de ejecución y destinatario. Sin WhatsApp, `GetReachAsync` dice que no hay canal y `ListTemplatesAsync` devuelve una lista vacía. El correo usa `IOutbox`, con el canal de correos. `IStructuredRecordSource.ReadAsync` lee por id, también las reemplazadas, y `StructuredRecordCard` suma `Corrected` y `Voided`.

**La campana:** Forms aporta `formApprovals`, a quien puede decidir, y `ruleNoticeFailed`, a quien tiene `formrules.manage`, por `INotificationSource` (base de la 12).

**Errores** (con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Rules.Invalid` | Validation (`rules[i]…`) | un evento, condición, acción, rol, persona, plantilla o pregunta fuera de regla, o el orden de las acciones |
| `Forms.Rules.TooMany` | Validation (`rules`) | más de 20 reglas, 10 condiciones o 10 acciones |
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
| `Forms.Record.PdfNotFound` | NotFound | no hay copia, o falta el archivo |
| `Forms.Execution.NotRetryable` | Conflict | lo que no quedó en «No se pudo» |

**Fallas de una ejecución,** que se ven en Actividad: `WindowClosed`, `TemplateUnavailable`, `NoConversation`, `NoContact`, `NotMember`, `RoleMissing`, `NoEmail`, `NoAnswer` y `NoPdf`.

**Permisos nuevos:** `formrules.manage`, `formapprovals.decide` y `formrecords.correct`, con sus textos y la migración de los roles de sistema.

## Datos

Tres migraciones: `<fecha>_FormRules.cs`, `<fecha>_FormRecordCorrections.cs` y `<fecha>_SystemRoleFormRulesPermissions.cs`. Todo es `ITenantOwned`, esquema `tenant`, con RLS, salvo el registro de trabajo.

| Tabla | Qué guarda | Regla |
| --- | --- | --- |
| **FormRuleSets** | `DefinitionId`, `Revision`, `Rules` jsonb, quién y cuándo | inmutable; único por los dos |
| **FormPdfDesigns** | `DefinitionId`, `Revision`, `Design` jsonb, quién y cuándo | inmutable; único por los dos |
| **FormApprovalTasks** (`IVersioned`) | `RecordId`, `DefinitionId`, `RoleId`, `State`, `RequestedAtUtc`, decisión, motivo | una abierta por respuesta, con índice único filtrado; índice por definición, rol, fecha e id |
| **FormActionExecutions** | `RecordId`, `RuleSetId`, `Event`, `RuleId`, `ActionId`, `Position`, `Kind`, `State`, `FailureCode?`, `Fence`, `LeaseExpiresAtUtc`, `NextAttemptAtUtc` | único por respuesta, evento, regla y acción |
| **FormActionRecipients** | `ExecutionId`, `Kind`, `UserId?`, `ContactId?`, `Address?` cifrada, `State`, `FailureCode?`, `RenderedText?` | uno por destinatario; el error de uno no toca a los otros |
| **FormRecordDocuments** | `RecordId`, `ExecutionId`, `DesignId`, `StorageKey`, `FileName`, `ByteLength`, `Sha256`, `Culture` | único por ejecución; se escribe una vez |
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
