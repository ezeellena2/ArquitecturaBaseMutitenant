# E25 · Documentos por WhatsApp: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E25](25-documentos.md), que dice qué leer y cómo se cierra.

## Contrato

Son rutas de `/api/organization/forms`, con `[FeatureGate("forms")]`.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `POST /documents/search` | `formrecords.read` | `{form?, senders?, received?, states?, companies?, search?, page, pageSize, sort}` | `PagedResult<{id, formName?, sender, receivedAtUtc, state, summary, deleteAtUtc?}>` |
| `POST /documents/facets` | `formrecords.read` | los filtros | conteos por estado, formulario y empresa |
| `GET /documents/{id}` y `…/content` | `formrecords.read` | — | los datos con su seguridad, las validaciones, el porqué y el original, `no-store` |
| `PUT /documents/{id}` | `formrecords.read` y atender esa bandeja | `{values, version}` | 200 `{version}` |
| `POST /documents/{id}/reading` | `formrecords.read` y atender esa bandeja | `{formId, version}` | 200 `{version}`; la lectura sigue por su trabajo |
| `POST /documents/{id}/discard` | `formrecords.read` y atender esa bandeja | `{version}` | 200 |
| `POST /documents/{id}/record` | `formrecords.read` y atender esa bandeja | `{version}`, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]` y `[IdempotencyDurable(typeof(FormEntryDurableAdapter))]`, porque el número es privado | 200 `{recordId, displayNumber, version}`, con `[ResponseVersion("Version")]` |
| `POST /documents/{id}/form-proposal` | `forms.manage` y `formrecords.read` | — | `{name, prefix, submitLabel, reading, questions[], similarTemplate?}` |
| `POST /documents/{id}/form-drafts` | `forms.manage` y `formrecords.read` | la propuesta, con `[Idempotent]` | 201 `FormDetail` |
| `GET /{id}/reading/stats` | `forms.read` | — | `{confirmedRun, precision[]}` |

Atender esa bandeja se revisa en el servicio, con `IStructuredInboxAccess`, porque depende de dónde llegó el documento, y Forms no puede nombrar un permiso de WhatsApp: retirar ese módulo lo saca del catálogo.

**Rutas que cambian:** `GET /{id}` y `PUT /{id}/draft` suman `reading?`, con `{enabled, howToRecognize, fields[], validations[], catalogCrossChecks[], duplicates, autoLoad}`. Las de la plantilla, en Plataforma, suman lo mismo. El detalle de una respuesta suma `sourceDocument?` y `reviewedBy?`. `GET /api/reference-data` suma las entidades del BCRA.

**Puertos neutrales:**

```csharp
// WhatsApp → Forms (implementa Forms)
public interface IStructuredDocumentSink
{
    Task<Result<StructuredDocumentReceipt>> ReceiveAsync(StructuredIncomingDocument document, CancellationToken ct);
}
// Forms → Asistente (implementa el módulo Assistant)
public interface IStructuredDocumentReader
{
    Task<Result<StructuredDocumentClassification>> ClassifyAsync(StructuredDocumentInput input, CancellationToken ct);
    Task<Result<StructuredDocumentExtraction>> ExtractAsync(StructuredDocumentInput input, CancellationToken ct);
}
// Forms → WhatsApp (implementa WhatsApp)
public interface IStructuredInboxAccess
{
    Task<bool> CanAttendAsync(StructuredInbox inbox, CancellationToken ct);
}
// Asistente → Forms (implementa Forms)
public interface IStructuredFormDrafts
{
    Task<Result<StructuredFormDraftCreated>> CreateAsync(StructuredFormDraftRequest request, CancellationToken ct);
}
```

`IStructuredFormChannel` suma mandar un mensaje con botones o una lista y un formulario prellenado. **Las propuestas** (154): dos lecturas de lo que existe, `read_forms` y `read_catalogs`, con `forms.read` y `formcatalogs.read`, y dos propuestas, `propose_form` y `propose_catalog`, que validan con las reglas del editor sin recortar: un dato inválido vuelve al modelo con la ruta de cada argumento, para que lo corrija en el mismo turno. Ninguna escribe. Crear lo pide la persona: `POST /api/forms/definitions/assistant-proposals` y `POST /api/forms/catalogs/assistant-proposals` (`[Idempotent]`, con `forms.manage` o `formcatalogs.manage`) releen la propuesta del turno por un puerto del asistente, rechazan una que otra posterior ya reemplazó y crean una sola vez; `GET …/assistant-proposals/{proposalId}` dice si ya se creó. Por WhatsApp, `IStructuredAuthoringProposalChannel` resume y crea desde la conversación (155). El audio del Inicio va por `POST /api/assistant/audio` y sus dos `GET` (156).

**La campana:** suma `documentsToReview` y `documentsSuggestion`, a quien tiene `formrecords.read`, por `INotificationSource` (base de la 12).

**Errores:**

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Document.NotFound` | NotFound | ajeno o inexistente |
| `Forms.Document.AlreadyResolved` | Conflict | ya está confirmado o descartado |
| `Forms.Document.ValidationFailed` | Validation (`values.<id>`) | no pasa una validación fija al confirmar |
| `Forms.Document.Duplicate` | Conflict | dice en qué respuesta |
| `Forms.Document.BudgetExhausted` | Conflict | tope de IA o por persona |
| `Forms.Draft.ReadingInvalid` | Validation (`reading…`) | un dato sin pregunta de ese tipo, o una validación que no existe |

## Datos

Dos migraciones. Todo es `ITenantOwned`, con RLS, salvo lo global. **Sin permisos nuevos.**

| Tabla | Qué guarda | Regla |
| --- | --- | --- |
| **FormDocuments** (`IVersioned`) | `Source`, quién lo mandó, `StorageKey`, `MediaType`, `ByteLength`, `Sha256`, `State`, `DefinitionId?`, `VersionId?`, `Extracted` jsonb, `Validation` jsonb, `DuplicateOfId?`, `RecordId?`, `ReviewedByUserId?`, `DeleteAtUtc?`, `Attempts`, `NextAttemptAtUtc` | GIN `jsonb_path_ops` en `Extracted`; índice por `Sha256` |
| **FormDocumentCorrections** | `DocumentId`, `FieldId`, propuesto, confirmado, quién y cuándo | de ahí salen los ejemplos y la precisión |
| **FormDocumentCounters** | quién, día y cantidad | clave por persona o contacto y día |
| **platform.BankEntities** | prefijo y nombre | dato de referencia, con su seed |
| **platform.FormDocumentWorks** | `TenantId`, `NextAtUtc` y reserva | registro técnico, sin datos de negocio |

**Columnas nuevas:** `FormVersions` suma `Reading` jsonb. `FormRecords` suma `SourceDocumentId?`. `WhatsAppFlowSessions` suma `SourceRef?`, opaco: el id del documento, que se propuso con ese Flow, y `StructuredRecordCapture` suma `SourceDocumentId?`, así la respuesta del Flow vuelve a su documento. **El `SourceKey` es `document:<id>` en los tres caminos** (el Flow, «Pasar al formulario» y «Cargar solo si es seguro»), y por eso un documento no se carga dos veces. `FormTemplateDraft` suma `reading`. **Estados:** `Received`, `Proposed`, `Unclassified`, `Confirmed`, `AutoLoaded` y `Discarded`. **Topes:** `Forms:DocumentReadsPerPersonPerDay`, con `ValidateOnStart`.
