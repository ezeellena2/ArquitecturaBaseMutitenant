# E21 · Reglas, aprobación, PDF y avisos: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E21](21-reglas-y-aprobacion.md), que dice qué leer y cómo se cierra.

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /{id}/rules` | `formrules.manage` | — | `{revision, rules[], questions[]}` |
| `PUT /{id}/rules` | `formrules.manage` | `{revision, rules[]}` | 200 `{revision}`, 409 si otro guardó |
| `GET /{id}/rules/revisions` y `…/{revision}` | `formrules.manage` | `page`, `pageSize` | la lista paginada y cada revisión |
| `GET /{id}/rules/options/{roles\|members\|templates}` | `formrules.manage` | `page`, `pageSize`, `search` | `PagedResult` de cada una, sin correos |
| `GET` y `PUT /{id}/pdf-design`, `POST /{id}/pdf-design/preview` | `formrules.manage` | `{design, revision}` | el diseño, su revisión, `organization` y un PDF de ejemplo, que no guarda |
| `GET /pending` | `formapprovals.decide` | `page`, `pageSize`, `search` | `PagedResult<{id, name, pending}>` |
| `GET /{id}/approvals`, `…/{taskId}` y `…/{taskId}/continuation` | `formapprovals.decide` | `page`, `pageSize`; `direction` y `limit` | la cola, sin valores |
| `POST /{id}/records/{recordId}/decision` | `formapprovals.decide` | `{decision, reason?, version}`, con `[Idempotent]` | 200 `{version, state}` |
| `GET /{id}/records/{recordId}/workflow` | `formrecords.read` | — | `{ruleRevision?, approval?, activity[], pdf?, canCorrect, canVoid}` |
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
