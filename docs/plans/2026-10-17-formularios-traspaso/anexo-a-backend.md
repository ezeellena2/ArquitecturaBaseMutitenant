> Anexo del traspaso. Las rutas de archivo son del repo `ArquitecturaBase` (relativas a `src/ArquitecturaBase.<Capa>/` salvo que digan otra cosa); los nombres de clases, tablas, rutas, errores y configuración son exactos al commit indicado.

# Formularios por WhatsApp: inventario del backend para replicarlo

**Fuente:** repositorio `ArquitecturaBase`, commit `b43edf8fb2027fc9aa60761dd4becdb8454686ee` («fix(formularios): conservar los textos históricos por idioma», 2026-10-17 12:06 -0300). Todo lo de este documento se leyó con `git show HEAD:<archivo>`, no del árbol de trabajo.

**Cambios sin commitear al momento de la lectura (no incluidos acá):** había 22 archivos modificados (Codex trabajando) que agregan la **revisión por destinatario** de un aviso incierto (`ReviewFormActionHttpRequest` suma `RecipientUserId`), evidencia de entrega separada (`StructuredRecordActionReceipt` con `DeliveredAtUtc`/`ReadAtUtc`), resultados por destinatario en el run (`FormActionRecipientOutcome`) y que un `NotifyRole` sin destinatarios termine `Completed`. Donde este inventario describe la revisión de avisos, describe la versión commiteada (una revisión por run).

**Cómo leerlo:** los nombres de clases, tablas, columnas, rutas, códigos de error y claves de configuración son exactos. Las rutas de archivo son relativas a `src/ArquitecturaBase.<Capa>/`. Cuando algo no está en el código, se dice.

---

## 0. Piezas transversales (valen para todas las etapas)

### 0.1 Capas y módulos

| Proyecto | Qué pone para formularios |
|---|---|
| `Domain` | entidades y `<Entidad>Errors` en `Domain/Modules/BusinessForms`, `Domain/Modules/WhatsApp`, `Domain/Files` (núcleo) |
| `Application` | servicios, políticas, guards, recorders, modelos, validadores (FluentValidation), puertos (`Interfaces/...`) |
| `Infrastructure` | EF Core (PostgreSQL), repositorios y lectores, Data Protection, workers, clientes HTTP de Meta, Azure Blob, SkiaSharp, ClosedXML |
| `Api` | controllers MVC, contratos `*HttpRequest`/`*Query`, rate limits, `Program.cs` compone |

- **BusinessForms**, **WhatsApp** e **IA** (`ArtificialIntelligence`) son módulos opcionales en `Modules/<Módulo>` de cada capa. El núcleo no los nombra y **BusinessForms y WhatsApp no se referencian entre sí**: se comunican sólo por puertos neutrales del núcleo (`Application/Interfaces/Channels`) con DTOs de `Application/Models/StructuredData`. Sin FK ni joins entre tablas de módulos distintos; los IDs cruzados (versión, sesión, run) son opacos.
- Composición en `Api/Program.cs`: `AddWhatsAppApplication().AddWhatsAppInfrastructure(cfg).AddWhatsAppApi()`, luego IA, luego `AddBusinessFormsApplication().AddBusinessFormsInfrastructure().AddBusinessFormsApi()`.
- Un único `ApplicationDbContext`; los módulos usan `dbContext.Set<T>()`. Migraciones en una sola cadena (`Infrastructure/Persistence/Migrations`).

### 0.2 Guardado, errores y logs

- **Única forma de guardar:** `IUnitOfWork.ExecuteInTransactionAsync(trabajo, CommitPolicy, ct)`, una vez por método público que escribe (READ COMMITTED, sin reintentos automáticos de EF). `CommitPolicy.OnSuccess` en todos los casos de formularios. Repositorios exigen la transacción (`dbContext.RequireTransaction()`); helpers (`*Policy`, `*Guard`, `*Recorder`, `*Issuer`, `*Verifier`) nunca reciben `IUnitOfWork`.
- **Reglas de negocio** devuelven `Result`/`Result<T>`; los errores se declaran en clases `<Entidad>Errors` de Domain con código `Area.Entidad.Motivo`. Mapeo a HTTP (`Api/ErrorHandling/ProblemDetailsMapper.cs`): Validation→400, Unauthorized→401, Forbidden→403, NotFound→404, Gone→410, Conflict→409, TooManyRequests→429, cualquier otro (incluido `Failure`)→500. Respuesta ProblemDetails con `title`, `detail`, `code`, `traceId` y `errors` (por campo, sólo de validadores o `FieldErrors.On(error, "ruta")`).
- **Logs:** `OperationLog.RunAsync(logger, "Operación", …)` envuelve cada método público que devuelve `Result`; nunca se loguea contenido, tokens, nombres de archivos ni números completos. Todos los records sensibles sobrescriben `ToString()` con `[REDACTED]`.
- **Fechas:** UTC desde `TimeProvider` inyectado.

### 0.3 Locks (PostgreSQL)

| Clave (`pg_advisory_xact_lock`) | Dónde (`*LockKeys`) | Qué serializa |
|---|---|---|
| `forms:definitions` | `FormAdvisoryLockKeys.Definitions` | **global**: guardar borrador, aceptar/reservar/completar publicación, capturar (numeración), reglas, diseño PDF, decisiones de aprobación, reservar/completar runs, `LockSelectionAsync` al mandar |
| `forms:request:{actor:N}:{requestId:N}` | `FormAdvisoryLockKeys.Request` | idempotencia de borrador y de aceptación de publicación |
| `forms:catalog:{catalogId:N}` | `FormAdvisoryLockKeys.Catalog` | edición de catálogo; preparación para envío; validación de ítem Manual activo en la captura |
| `forms:capture:{sourceReceiptId:N}` | `FormAdvisoryLockKeys.Capture` | una captura por sesión de origen |
| `whatsapp-flow:{neutralOperationId:N}` | `WhatsAppLockKeys.Flow` | operación de publicación del Flow |
| `whatsapp-form-action:{runId:N}:{recipientUserId:N}` | `WhatsAppLockKeys.FormAction` | salida durable de un aviso de regla |
| `whatsapp-contact:user:…` / `whatsapp-contact:wa:…` / `whatsapp-message:{wamid}` | `WhatsAppLockKeys` | webhook, mensajes |

Locks de fila: `FOR NO KEY UPDATE` sobre `WhatsAppContacts` y `WhatsAppOutgoingMessages` (envíos y avisos; siempre **contacto antes que salida**), `FOR NO KEY UPDATE SKIP LOCKED` para tomar la próxima salida `Pending`. Además Redis: lease por destinatario `whatsapp:dispatch:{hash}` de 60 s, renovada cada 10 s, con enfriamiento de 6 s al liberar (`Infrastructure/Modules/WhatsApp/WhatsAppDispatchCoordinator.cs`).

### 0.4 Data Protection (contenido cifrado en columnas `bytea`)

| Propósito | Datos que liga (subpropósitos) | Contenido |
|---|---|---|
| `ArquitecturaBase.BusinessForms.DraftChanges.v1` | id del cambio, actor, requestId, definitionId, kind | resultado recuperable del guardado + huella SHA-256 del pedido |
| `ArquitecturaBase.BusinessForms.PublicationChecks.v1` | — | ticket del chequeo (`checkToken`, base64) |
| `ArquitecturaBase.BusinessForms.PublicationManifests.v1` | operationId, actor, requestId, definitionId, versionId | manifiesto de publicación |
| `ArquitecturaBase.WhatsApp.FlowManifests.v1` | operationId, neutralOperationId, versionId | manifiesto del canal (cuenta, huellas, permisos) |
| `ArquitecturaBase.WhatsApp.FlowProjections.v1` | operationId, bindingId, versionId, culture | Flow JSON + mapping sellado por cultura |
| `ArquitecturaBase.WhatsApp.FlowSessions.v1` | sessionId, contactId, userId, versionId | payload privado de la sesión (token, oferta de catálogos…) |
| `ArquitecturaBase.WhatsApp.FlowReplies.v1` | waMessageId, contactId | `response_json` crudo del `nfm_reply` |

Todos serializan JSON con `JsonSerializerDefaults.Web`, `UnmappedMemberHandling.Disallow` y profundidad acotada, y ponen en cero el texto plano después de usarlo.

### 0.5 Rate limits y límites de cuerpo (Api)

`Api/Modules/BusinessForms/BusinessFormsApiRegistration.cs`, partición por `UserId` (o IP si no hay sesión):

| Política | Límite |
|---|---|
| `forms-read` | 120 por minuto, ventana fija, sin cola |
| `forms-write` | 30 por minuto |
| `forms-files-read` | 600 por minuto |
| `forms-export` | concurrencia 1 por usuario, sin cola (la segunda exportación simultánea recibe 429) |

Chat de WhatsApp: `RateLimiting:WhatsAppChatReadPermitLimit` (600/min) y `WhatsAppChatWritePermitLimit` (120/min); webhook por IP `RateLimiting:WhatsAppWebhookPermitLimit` (600) por `WhatsAppWebhookWindowMinutes` (1). `RequestSizeLimit`: 512 KiB (definiciones, chequeo, publicación, catálogos, respuestas, export), 4 MiB (reglas), 128 KiB (diseño PDF), 8 KiB (aprobaciones). Webhook: 5 MB (413).

### 0.6 Puertos neutrales y quién los implementa

| Puerto (`Application/Interfaces/Channels`) | Para qué | Versión del núcleo (sin módulo) | Implementación | Registro |
|---|---|---|---|---|
| `IStructuredFormChannel` | chequeo local, aceptar publicación, estado, preview oficial | ninguna (colección vacía → `ChannelUnavailable`) | `WhatsAppStructuredFormChannel` (`ChannelId = "phone"`) | `TryAddEnumerable` scoped (WhatsApp) |
| `IStructuredFormSource` | listar/buscar formularios enviables a un destinatario, preparar catálogos, lock de selección | `DisabledStructuredFormSource` (página vacía) | `BusinessFormSource` | núcleo `TryAddSingleton`; BusinessForms `Replace` scoped |
| `IStructuredRecordSink` | capturar una respuesta (idempotente) y buscar recibo previo | `UnavailableStructuredRecordSink` (devuelve estado `"Unavailable"`) | `BusinessFormRecordCaptureSink` → `IBusinessFormRecordCaptureService` (`BusinessFormRecordRecorder`) | núcleo `TryAddScoped`; BusinessForms `Replace` |
| `IStructuredRecordSource` | leer valores de respuestas por lote (chat) | `UnavailableStructuredRecordSource` (`CanReadRecords=false`) | `BusinessFormRecordSource` | núcleo `TryAddScoped`; BusinessForms `Replace` |
| `IStructuredRecordOriginSource` | origen de una respuesta (canal, quién mandó, cuándo, conversación, número enmascarado) | ninguna | `WhatsAppStructuredRecordOriginSource` | `TryAddEnumerable` (WhatsApp) |
| `IStructuredRecordActionChannel` | encolar/consultar/revisar/reintentar avisos de reglas | ninguna (colección vacía → run `Failed` `Forms.Action.ChannelUnavailable`) | `WhatsAppStructuredRecordActionChannel` (`ChannelId = "phone"`) | `TryAddEnumerable` (WhatsApp) |
| `IPrivateFileRetentionSource` | claves de blobs que la limpieza no debe borrar aunque falte metadata | ninguna | `FormPrivateFileRetentionSource` (PDF) | `TryAddEnumerable` (BusinessForms) |
| `IClientCapabilitySource` | features instaladas para el front | — | `FormClientCapabilitySource`: `["forms"]` o `["forms","form-files"]` si hay almacenamiento | `TryAddEnumerable` singleton |
| `IPermissionCatalogSource` | permisos del módulo | — | `FormPermissionCatalogSource`, `WhatsAppPermissionCatalogSource` | `TryAddEnumerable` singleton |

Integraciones neutrales del núcleo para archivos: `IPrivateFileStore` y `IFileContentProcessor` (`Application/Interfaces/Integrations/Files`), implementadas por `AzureBlobFileStore` y `FileContentProcessor` (`Infrastructure/Files`).

DTOs neutrales (`Application/Models/StructuredData`): `StructuredFormCheckRequest/Question/Choice/FileSettings/IssueResponse/CultureResponse/CheckResponse`, `StructuredFormPublicationRequest/Query/Response/Culture`, `StructuredFormPreviewQuery/Response`, `ListStructuredFormsRequest`, `StructuredFormSelectionRow`, `StructuredFormCatalogOffer/Option/OptionText`, `StructuredFormPreparationIssue/Response`, `StructuredRecordCaptureRequest/Response`, `StructuredRecordFileInput`, `StructuredRecordReadResult/Reply/Value`, `StructuredRecordChoiceValue`, `StructuredRecordFileValue`, `StructuredRecordOrigin`, `StructuredRecordActionMessage/Document/Response/Review`, `StructuredRecordActionState` (`NotFound, Pending, Sending, Accepted, Failed, Uncertain`).

---

## 1. Etapa 1: crear, publicar, mandar y recibir

### 1.1 Entidades y tablas de BusinessForms

Todas heredan `Entity` (Id Guid v7 generado en el servidor). Las marcadas *IAuditable* tienen `CreatedAtUtc`, `CreatedBy`, `ModifiedAtUtc`, `ModifiedBy`, completados sólo por interceptores.

#### `BusinessFormDefinition` → tabla `FormDefinitions` (IAuditable)

| Campo | Tipo | Significado |
|---|---|---|
| `Name` | varchar(120) | nombre en la cultura original, derivado de `NameJson[Culture]` en el mismo commit; no editable aparte |
| `NameJson` | jsonb | mapa autoritativo `{"en":…,"es":…}` |
| `DisplayNameEs` / `DisplayNameEn` | varchar(120), **columnas calculadas almacenadas** | `COALESCE(NULLIF("NameJson"->>'es',''), "Name")` (ídem en); se usan para buscar/ordenar en SQL según la cultura de UI |
| `Prefix` | varchar(12), **único** | prefijo del número de respuesta (`^[A-Z][A-Z0-9]{0,11}$`) |
| `PrefixLocked` | bool | se fija en `true` al aceptar la primera publicación; después cambiar el prefijo da 409 |
| `Culture` | varchar(2) | idioma original (`es`/`en`), inmutable desde la creación |
| `SubmitLabelJson` | jsonb | mapa del botón de envío |
| `SchemaJson` | jsonb | esquema del **borrador** (`{"format":1,"fields":[…]}`, ver 1.3) |
| `QuestionCount` | int | cantidad de preguntas del borrador |
| `Revision` | long, **concurrency token** | revisión del borrador; empieza en 1, `+1` en cada guardado; tope 2^53−1 |
| `ActiveVersionId` | Guid?, token | versión publicada activa; FK compuesta `(Id, ActiveVersionId)` → `BusinessFormVersions(DefinitionId, Id)` |
| `ActiveRuleSetId` | Guid?, token | revisión de reglas vigente (Etapa 4) |
| `ActivePdfDesignId` | Guid?, token | diseño PDF vigente (Etapa 4) |
| `LastRecordNumber` | long, token | último número asignado a una respuesta (`checked +1`) |

Métodos de dominio: `SetDraft(expectedRevision, …, creating)` (409 `Forms.Draft.RevisionConflict`; si `PrefixLocked` y cambia el prefijo, 409 `Forms.Definition.PrefixLocked`), `LockPrefix()`, `AllocateRecordNumber()`, `ActivateVersion(baseVersionId, versionId)` (CAS: sólo si `ActiveVersionId == baseVersionId`, si no `Forms.Publication.StateConflict`).

#### `BusinessFormDraftChange` → `FormDraftChanges` (IAuditable)

Comprobante idempotente de cada guardado. Campos: `OperatorUserId`, `RequestId`, `DefinitionId` (FK Restrict), `Kind` (varchar(6): `Create`/`Update`), `ExpectedRevision` (long?), `ConfirmedRevision`, `ResultFormat` (=1), `ProtectedResult` (bytea, nullable), `ResultExpiresAtUtc`. Índice **único** `(OperatorUserId, RequestId)`; índice filtrado `(ResultExpiresAtUtc, Id) WHERE "ProtectedResult" IS NOT NULL`. `ExpireResult(now)` pone `ProtectedResult = null` pero **conserva la fila** (tombstone que impide reutilizar el UUID; no hay purga de filas).

#### `BusinessFormRetiredIdentity` → `BusinessFormRetiredIdentities` (IAuditable)

Ledger de identidades retiradas: `DefinitionId` (FK Cascade), `IdentityId`, `Kind` (`Field`/`Choice`, varchar(8)). Único `(DefinitionId, Kind, IdentityId)`. Reemplazó una columna JSON vieja (`RetiredIdentityLedgerJson`) con migración de datos en `20261011030121_AddFormRetiredIdentities`.

#### `BusinessFormField` → `BusinessFormFields`

Identidad canónica de pregunta registrada **al aceptar una publicación** (nunca al guardar). Id = `FieldId`; `DefinitionId` (FK Restrict), `StableKey` (varchar(38), `field_{FieldId:N}`), `Type` (varchar(16)). Clave alterna `(DefinitionId, Id)`; único `(DefinitionId, StableKey)`. La usan como FK compuesta `BusinessFormRecordValues`, `BusinessFormRecordReferences` y `BusinessFormRecordFiles` (un valor sólo puede apuntar a un campo del mismo formulario).

#### `BusinessFormVersion` → `BusinessFormVersions` (IAuditable) — inmutable

| Campo | Tipo | Significado |
|---|---|---|
| `DefinitionId` | Guid (FK Restrict) | |
| `Number` | long | número de versión; único `(DefinitionId, Number)`; = cantidad de versiones aceptadas + 1 (las fallidas también consumen número) |
| `SchemaFormatVersion` | int (=1) | |
| `SchemaJson` | jsonb | esquema **sellado** `{"format":1,"questions":[…]}`; `SealSchema` lanza si ya tiene contenido |
| `ContentHash` | varchar(64) | SHA-256 hex del `SchemaJson` serializado |
| `NameJson`, `SubmitLabelJson` | jsonb | presentación sellada (nullable sólo por versiones viejas; `SealPresentation` lanza si ya estaba) |
| `Culture` | varchar(2) | idioma original sellado |
| `QuestionCount` | int? (0–40) | |
| `DisplayNameEs/En` | calculadas: `"NameJson"->>'es'` / `'en'` | búsqueda/orden del selector, **sin** fallback al borrador |

Clave alterna `(DefinitionId, Id)`.

#### `BusinessFormPublicationOperation` → `BusinessFormPublicationOperations` (IAuditable)

| Campo | Significado |
|---|---|
| `DefinitionId`, `VersionId` | FK compuesta → `BusinessFormVersions(DefinitionId, Id)`; `VersionId` **único** |
| `OperatorUserId`, `RequestId` | único `(OperatorUserId, RequestId)` (idempotencia de la aceptación) |
| `DraftRevision`, `VersionNumber` | revisión del borrador publicada y número de versión |
| `BaseActiveVersionId` | versión activa al aceptar (para el CAS de activación); FK compuesta |
| `ProtectedManifest` | bytea, sellado una vez |
| `State` | varchar(24): `Accepted, Preparing, Publishing, Uncertain, ActivationPending, Published, Failed` |
| `Stage` | varchar(32): texto libre de etapa (`Accepted`, `Create`, `Verify`, `Activation`, `Complete`, `ActivationConflict`, `ManifestUnreadable`, `AuthorizationChanged`, `ChannelUnavailable`, códigos de error del canal…) |
| `Fence` | long, **concurrency token** |
| `LeaseExpiresAtUtc`, `NextCheckAtUtc`, `AcceptedAtUtc`, `UpdatedAtUtc` | |

Índice **único filtrado** `(DefinitionId) WHERE "State" NOT IN ('Published','Failed')`: una sola operación abierta por definición. `IsOpen` = no Published ni Failed (ignorado por EF).

Transiciones (dominio): `Reserve(now, lease)` exige abierta, sin lease vigente ni `NextCheckAtUtc` futuro; `Fence++`, lease, `State=Preparing` (salvo si estaba `ActivationPending`). `RecordChannelResult(fence, now, state, stage)` exige fence y lease vigentes y mapea `"Published"→ActivationPending`, `"Failed"→Failed`, `"Uncertain"→Uncertain`, `"Publishing"/"Accepted"/"Preparing"→Publishing`; libera lease; `NextCheckAtUtc = now+30 s` si sigue abierta y no es ActivationPending. `DeferActivation` → `Uncertain`/`ActivationConflict`, próxima consulta +5 min. `Complete` → `Published`/`Complete`.

### 1.2 Entidades y tablas de WhatsApp para Etapa 1

#### `WhatsAppFlowOperation` → `WhatsAppFlowOperations` (IAuditable)

Operación del canal por versión: `NeutralOperationId` (= Id de la operación de BusinessForms; **único**), `DefinitionId`, `VersionId` (**único**), `ActorUserId`, `ProtectedManifest`, `Fence` (token), `LeaseExpiresAtUtc`, `NextCheckAtUtc`, `Completed`. Clave alterna `(Id, VersionId)`; índice `(NextCheckAtUtc, Id) WHERE "Completed" = FALSE`. `Release(now, completed, requiresReview)`: próxima consulta +15 s, o +300 s si hay incertidumbre.

#### `WhatsAppFlowBinding` → `WhatsAppFlowBindings` (IAuditable)

Un recurso físico de Meta **por versión y cultura**: `OperationId`+`VersionId` (FK compuesta a la operación), `Culture` (varchar(2)), `TechnicalName` (varchar(80), `form_{operationId:N}_{culture}`), `ProtectedProjection` (bytea), `MetaFlowId` (varchar(128), único filtrado si no es null), `State` (`Accepted, Publishing, Uncertain, Published, Failed`), `Step` (`Create, Upload, Validate, Publish, Verify`), `InFlight`, `Fence` (token), `VerifiedAtUtc`, `FailureCode` (varchar(80)), `DiagnosticsJson` (jsonb). Único `(VersionId, Culture)`.

#### `WhatsAppFlowSession` → `WhatsAppFlowSessions` (IAuditable)

Sesión creada al encolar un formulario: `ContactId` (FK), `LinkRevision` (revisión del vínculo contacto↔cuenta en ese momento), `UserId` (destinatario), `OperatorUserId`, `DefinitionId`, `VersionId`, `VersionNumber`, `BindingId` (FK compuesta `(BindingId, VersionId)`), `Culture`, `NameJson` (jsonb, nombre sellado), `Prefix` (≤12), `TokenHash` (64 hex, **único**), `RequestFingerprint` (64 hex), `ProtectedPayload` (bytea, sellado una vez), `ExpiresAtUtc`, `CaptureState` (`Pending, Completed, Rejected, Unrecognized`), `CompletedRecordId`, `CompletedNumber`, `CaptureErrorCode` (≤128), `RejectedQuestion` (≤80), `ResponseWaMessageId` (único filtrado). Índice `(ExpiresAtUtc, Id)`. Clave alterna `(Id, ContactId, OperatorUserId)`. `CompleteCapture` es terminal: una segunda llamada con otros datos devuelve `false` sin cambiar nada.

#### Columnas de formularios en tablas existentes

- `WhatsAppContacts.LinkRevision` (long): sube con cada vinculación/desvinculación efectiva (`LinkUser`/`UnlinkUser`), distingue A → sin vínculo → A.
- `WhatsAppMessages`: `Origin` (`Conversation`/`FormAction`, default Conversation), `Kind` incluye `Flow=8`, `FlowReply=9`, `Document=10`; `FlowSessionId`, `FlowCaptureState`, `FlowRecordId`, `FlowRecordNumber` (≤64, ej. `PRB1010-000001`), `FlowCaptureErrorCode` (≤128), `FlowRejectedQuestion` (≤80), `ProtectedFlowResponse` (≤96 KiB), `HasFlowFiles`, `FlowFileCount`, `FlowReplyFailureCount` (saturado en 5), `FlowReplyFirstFailureAtUtc`. `WaMessageId` único. Índice filtrado `IX_WhatsAppMessages_PendingFlowReplies` `(OccurredAtUtc, Id)` para entrantes `FlowReply` sin procesar; el índice de entrantes pendientes de texto **excluye** `FlowReply`.
- `WhatsAppOutgoingMessages`: `Kind` (`Text, Flow, Document`), `FlowSessionId` (FK a la sesión), más las columnas de avisos de la Etapa 4. Único `(ContactId, OperatorUserId, RequestId)`; índice `(State, RequestedAtUtc, Id)`. Estados: `Pending, Sending, Accepted, Failed, Uncertain, Reviewed`.
- `WhatsAppDeliveryReceipts`: estados de Meta por `wamid` (único) que llegan antes de registrar el saliente; al registrarlo se aplica el último (orden: timestamp, desempate `sent<delivered<read<failed`, `failed` final).

### 1.3 Esquema de preguntas (SchemaJson)

Serialización con `JsonSerializerDefaults.Web` (camelCase). Los mapas de textos se canonizan como diccionarios ordenados por clave (`en` antes que `es`).

**Borrador** (`FormDefinitions.SchemaJson`, record `FormQuestionSchema`):

```json
{
  "format": 1,
  "fields": [
    { "fieldId": "0199f…", "key": "field_0199f…", "type": "shortText",
      "label": { "en": "Name", "es": "Nombre" }, "help": null,
      "required": true, "maxLength": 80, "options": null },
    { "fieldId": "…", "key": "field_…", "type": "choice",
      "label": { "es": "Entrega" }, "help": null, "required": true, "maxLength": null,
      "options": [ { "choiceId": "…", "label": { "en": "Pickup", "es": "Retiro" } } ] },
    { "fieldId": "…", "key": "field_…", "type": "choice", "label": { "es": "Supervisor" },
      "help": null, "required": false, "maxLength": null, "options": null,
      "source": { "catalog": "<catalogId>" } },
    { "fieldId": "…", "key": "field_…", "type": "photo", "label": { "es": "Foto" },
      "help": null, "required": true, "maxLength": null, "options": null,
      "files": { "minFiles": 1, "maxFiles": 3, "photoSource": "camera" } }
  ]
}
```

**Versión sellada** (`BusinessFormVersions.SchemaJson`, record `FormVersionSchema`): idéntica pero la lista se llama **`questions`**: `{"format":1,"questions":[…]}`. Cultura, nombre y botón **no** van en el esquema (van en columnas de la definición/versión). `source` y `files` se omiten cuando son null; `help`, `maxLength` y `options` se escriben como `null`.

**Tipos** (string en JSON → enum `FormQuestionType` de Domain):

| `type` | Enum | Propiedades / reglas (validador `SaveFormDraftRequestValidator` + `FormDraftPolicy`) | Valor capturado |
|---|---|---|---|
| `shortText` | `ShortText` | `maxLength` 1–10000, default **80** | `TextValue` (≤10000) |
| `longText` | `LongText` | `maxLength` 1–10000, default **600** | `TextValue` |
| `decimal` | `Number` | sin `maxLength` | `DecimalValue` exacto (decimal .NET, 96 bits, escala ≤28) |
| `date` | `Date` | — | `DateValue` (`DateOnly`, `yyyy-MM-dd`, sin zona) |
| `boolean` | `Boolean` | — | `BooleanValue`; ausencia opcional ≠ `false` |
| `choice` | `Choice` | **o** `options` (1–50, cada una `{choiceId,label}`) **o** `source.catalog` (Guid), nunca ambos | `ChoiceId` (opción manual o ítem del catálogo) |
| `photo` | `Photo` | `files: {minFiles 0–10, maxFiles 1–10, min≤max, photoSource "cameraOrGallery"\|"camera"}`; `required == (minFiles>0)` | filas en `BusinessFormRecordFiles` |
| `document` | `Document` | `files` igual pero `photoSource` debe ser null | ídem |
| (reservado) | `Group` | rechazado al guardar (`type` inválido) | — |

Otras reglas del validador (todas devuelven `Forms.Draft.ContentInvalid` en la ruta exacta, ej. `draft.fields[2].options[0].label.en`):
- `requestId` no vacío; `prefix` tras `Trim().ToUpperInvariant()` debe cumplir `^[A-Z][A-Z0-9]{0,11}\z`; `expectedRevision` 1..2^53−1 (obligatorio en PUT, prohibido en POST).
- `draft.format == 1`; `draft.culture` ∈ {es, en}; máximo **40** preguntas.
- `localId` (de pregunta y de opción) `^[A-Za-z0-9_-]{1,64}\z`, único en su lista; sólo correlaciona el intento, no se guarda en `SchemaJson`.
- `fieldId` y `replacesFieldId` no vacíos y mutuamente excluyentes.
- Textos: claves sólo `es`/`en`; ningún valor `null`; el texto de la cultura original no puede estar en blanco; la secundaria puede faltar o ser `""`. Límites: **nombre 120 unidades UTF-16**; label, opción y `submitLabel` **160 elementos de texto** (grafemas, `StringInfo`); ayuda **1000** grafemas. Sin caracteres de control (la ayuda admite `\r \n \t`), sin caracteres de formato salvo ZWNJ/ZWJ, y si no está en blanco debe tener algún carácter visible.
- `help`: mapa de strings nullable; `null` o `""` retira esa cultura.
- Opciones duplicadas: se rechazan sólo en la cultura original, comparando `Normalize()` (NFC) + `Trim()` sin distinguir mayúsculas; marca la segunda fila.
- Cuerpo ≤ 512 KiB; resultado protegido ≤ 1 MiB (`Forms.Draft.ResultTooLarge`).

**Mapas es/en y preservación de traducciones** (`FormDraftPolicy`):
- Nombre: se recorta en ambos idiomas. Botón omitido al **crear** → default `Enviar` (es) o `Submit` (en) sólo en la cultura original; al **editar** omitirlo → `Forms.Draft.TranslationsWouldBeLost`.
- `TranslationsWouldBeLost` (400) si se omite una clave que antes tenía texto no vacío (nombre, botón, label, opción) o cualquier clave de ayuda previa. Enviar `""` la retira deliberadamente. Whitespace en una secundaria se canoniza a `""`.
- Cambiar `draft.culture` de una definición existente → `ContentInvalid` en `draft.culture`.

**Identidad (FieldId / ChoiceId)** — `FormDraftPolicy.Resolve`:
- Sólo el servidor genera IDs (`Guid.CreateVersion7()`, fábricas `FormQuestionIdentity`/`FormChoiceIdentity` de Domain). `key = "field_" + FieldId:N`, permanece con la identidad (nunca el label).
- Pregunta con `fieldId`: debe existir en el borrador guardado de esa definición y no repetirse; el `type` y el `source` deben ser iguales (si no, `Forms.Draft.QuestionConfigurationInvalid`).
- Cambiar tipo u origen exige `replacesFieldId` (identidad nueva); usar `replacesFieldId` con mismo tipo y origen → `QuestionConfigurationInvalid`.
- Opciones: un `choiceId` debe pertenecer a las opciones anteriores de **la misma** pregunta (no se mueven entre preguntas; con `replacesFieldId` no se conserva ninguna).
- Omitir una pregunta u opción la **retira**: se agrega al ledger. Reusar un ID retirado → 409 `Forms.Draft.IdentityRetired`; ID ajeno → 400 `Forms.Draft.IdentityInvalid`.
- Renombrar, traducir o reordenar conserva IDs. La respuesta incluye `identityMap` (`fields[{localId, fieldId, key, replacesFieldId}]`, `choices[{fieldLocalId, localId, fieldId, choiceId}]`).

**Versiones y publicación:** el borrador guardado no crea campos ni versiones. Al **aceptar** una publicación se registran los `BusinessFormFields` que falten (si uno existe con otro tipo/clave → `Forms.Publication.StateConflict`), se crea `BusinessFormVersion` con `SchemaJson` = `{"format":1,"questions": <fields del borrador>}`, `ContentHash`, presentación sellada, y la operación con su manifiesto. La versión queda activa recién cuando el canal confirma `Published` (1.6).

### 1.4 Servicios de Application (BusinessForms) de la Etapa 1

| Servicio / helper | Interfaz | Responsabilidad |
|---|---|---|
| `FormDraftService` | `IFormDraftService` | Create/Update del borrador (una UoW), Get detalle, GetChange (recuperación), List, Facets |
| `FormDraftPolicy` (estático) | — | normalización, defaults, `Resolve` de identidades, serialización |
| `FormDraftAccessGuard` | — | sesión + `formtemplates.read` (+ `formtemplates.manage` al escribir) |
| `FormDraftRetentionService` | `IFormDraftRetentionService` | purga técnica (UoW, lock global, 100 vencidos) |
| `BusinessFormPublicationService` | `IBusinessFormPublicationService` | chequeo local de publicación (sin UoW ni locks), emite `checkToken` |
| `FormPublicationPolicy` (estático) | — | `MissingTranslations` (es y en completos) y `ValidateFiles` |
| `FormPublicationSnapshotPolicy` (estático) | — | `Matches` (lo enviado = lo guardado), `ChannelRequest`, `Fingerprint`, `Hash` |
| `FormPublicationGuard`, `FormPublicationAccessGuard` | — | Read + Publish; validación de archivos y catálogos para el chequeo |
| `FormPublicationAcceptanceGuard` | — | permisos + validación estructural del pedido de aceptación |
| `FormPublicationChannelVerifier` | — | re-chequea en todos los canales instalados dentro de la aceptación y arma los perfiles |
| `FormPublicationAcceptanceService` | `IFormPublicationAcceptanceService` | aceptación durable (UoW `OnSuccess`) |
| `FormPublicationDispatchService` | `IFormPublicationDispatchService` | listar vencidas, reservar y completar (cada una con su UoW); decide estado y activación |
| `FormPublicationChannelDispatchService` | `IFormPublicationChannelDispatchService` | llama `AcceptAsync`/`GetPublicationAsync` del canal **fuera** de transacciones |
| `FormPublicationStatusService` | `IFormPublicationStatusService` | estado de la operación propia, con issues del canal |
| `FormPreviewService` | `IFormPreviewService` | URL oficial de preview de una versión publicada |
| `BusinessFormSource` | `IStructuredFormSource` | selección y preparación para un destinatario (ver 1.7 y 2.4) |
| `BusinessFormRecordRecorder` | `IBusinessFormRecordCaptureService` | captura idempotente (ver 1.9) |
| `BusinessFormRecordSource` | `IStructuredRecordSource` | lectura histórica por lote con `formrecords.read` revalidado después de leer |
| `FormRecordPresentationPolicy` (estático) | — | proyecta valores de un registro con su versión sellada y cultura |

**Guardado de borrador** (`FormDraftService.SaveAsync`, dentro de una UoW):
1. Antes: guard (Read+Manage), normalizar nombre/prefijo, validar; en Update `expectedRevision` obligatorio, en Create prohibido; `FormDraftPolicy.Normalize`.
2. Lock `forms:request:{actor}:{requestId}`; buscar `FormDraftChanges` del actor/UUID. Si existe: otro `kind` o definición → 409 `Forms.Draft.RequestConflict`; vencido → **410** `Forms.Draft.ChangeExpired`; misma huella (SHA-256 del JSON canónico `{kind, definitionId, request}`, comparada en tiempo constante) → devuelve el resultado original (aunque la revisión actual sea mayor); otra huella → 409.
3. Lock `forms:definitions`; cargar definición (404 `Forms.Definition.NotFound`); revisión distinta → 409 `Forms.Draft.RevisionConflict`; prefijo bloqueado distinto → 409 `Forms.Definition.PrefixLocked`.
4. Leer identidades retiradas pedidas; `Resolve`; prefijo usado por otra definición → 400 `Forms.Definition.PrefixTaken` (`errors.prefix`).
5. `SetDraft` (Revision++ en Update; en Create queda 1); crear `FormDraftChanges` con vencimiento **30 días**; proteger el resultado; agregar ledger; purgar hasta 100 resultados vencidos.

Respuesta: `{requestId, definition{id, name, prefix, prefixLocked, revision, draft{format, culture, submitLabel, fields}}, identityMap, recovery{path, resultExpiresAtUtc}}`. `GET draft-changes/{requestId}` devuelve `{requestId, kind, status:"Committed", committedAtUtc, currentRevision, result}` sólo al mismo actor; 404 si no existe (no prueba rollback), 410 si venció.

**Detalle** (`GET definitions/{id}`): `definition`, `capabilities{canSave (Manage), canPublish (Publish), canPreview (hay activa)}`, `audit{createdAtUtc, modifiedAtUtc, lastChangedAtUtc, lastChangedBy}`, `publication{activeVersion{versionId, versionNumber, draftRevision}|null, currentOperation{versionId, versionNumber, draftRevision, state, stage, requiresReview, canRetry:false, requestId, recoveryPath}|null, displayState}`. `currentOperation` es la de mayor número de versión; `requestId`/`recoveryPath` sólo si el lector es el actor de esa operación y tiene Publish. `displayState` = estado de la última operación, o `Published`/`Draft` si no hay.

**Listado** (`GET definitions`): `PublicationFilter` `All|Published|Drafts` (Published = `ActiveVersionId != null`), orden `name`/`questionCount`/`lastChangedAtUtc` (`-campo` descendente; default `name`), búsqueda ILIKE sobre `DisplayNameEs/En` según la cultura de UI (resuelta por el servidor con `Accept-Language`) **o** sobre el prefijo, paginado SQL (página 1–…, tamaño 1–100, default 20). Fila: `id, name (mapa completo), prefix, questionCount, activeVersionNumber, displayState, lastChangedAtUtc, lastChangedBy, canEdit`. Facetas: `{all, published, drafts}`.

**Chequeo de publicación** (`BusinessFormPublicationService.CheckAsync`, sin UoW ni locks):
1. Read+Publish; validar el cuerpo con el validador del borrador; definición (404); `expectedRevision` ≠ revisión → 409 `Forms.Draft.RevisionConflict`.
2. `Matches`: nombre, prefijo, cultura y todas las preguntas/opciones con IDs definitivos (sin `replacesFieldId`, sin IDs nuevos) deben coincidir byte a byte con lo guardado; si no → 409 `Forms.Publication.DraftMustBeSaved`.
3. Issues (respuesta 200, no error): `Forms.Publication.MissingTranslation` por cada texto es/en faltante (nombre, botón, labels, ayuda si hay alguna, opciones) con `path`, `culture`, `fieldId`, `choiceId`; `Forms.Publication.FileStorageUnavailable`; `Forms.Publication.TooManyFiles` (suma de `maxFiles` > 10, `limit:10`); `Forms.Publication.CatalogInvalid` (catálogo inexistente o inactivo, en `draft.fields[i].source.catalog`); `Forms.Publication.ChannelUnavailable` si no hay canal.
4. Sólo si no hubo issues, `CheckAsync` de cada canal (orden por `ChannelId`; IDs vacíos o duplicados son error de composición) con el request neutral (cultura objetivo `["es","en"]`, `MaxFileSizeMiB` sólo si hay preguntas de archivo).
5. `compatibility` = `Unavailable` (sin canales) / `LocallyValidated` / `Blocked`. `canAccept` = LocallyValidated ∧ todos los canales `PublicationAvailable` ∧ sin operación abierta. `acceptanceAvailability` = `InProgress` / `Available` / `Unavailable`. `nextVersionNumber` = versiones + 1 (orientativo).
6. Si `canAccept`: `checkToken` = ticket protegido `{actor, definitionId, draftRevision, publicationFingerprint, expiresAtUtc = now+10 min}`.
7. `diff`: sin activa → `baseVersionId=null`, `added` = todas; con activa → compara por `FieldId` contra las preguntas selladas (`changed` = JSON distinto).

**Aceptación** (`FormPublicationAcceptanceService.AcceptCoreAsync`, una UoW):
1. Guard + validación. Lock `forms:request:{actor}:{requestId}`. Huella del intento = SHA-256 de `{definitionId, input}`. Si ya existe la operación del actor/UUID: otra definición o huella distinta → 409 `Forms.Publication.PayloadMismatch`; igual → mismo recibo (**el replay se resuelve antes** de vencimiento, revisión o perfil).
2. Ticket: ilegible, de otro actor/definición/revisión o vencido → 409 `Forms.Publication.CheckExpired`.
3. Lock `forms:definitions`; definición (404); revisión → 409; `Matches` → 409 `DraftMustBeSaved`; traducciones faltantes → 400 `Forms.Publication.MissingTranslation`; operación abierta → 409 `Forms.Publication.InProgress`.
4. `FormPublicationChannelVerifier`: almacenamiento si hay archivos (400 `FileStorageUnavailable`), suma ≤10 (400 `TooManyFiles`), al menos un canal (400 `ChannelUnavailable`), cada canal `PublicationAvailable` (400 `Forms.Publication.AccountUnavailable`) y `LocallyValidated` sin issues (409 `CheckExpired`). La huella de publicación recalculada debe ser la del ticket (si no, 409 `CheckExpired`).
5. Registrar campos, crear versión (número = cantidad + 1), sellar esquema y presentación, crear operación `Accepted` con `BaseActiveVersionId = ActiveVersionId` actual y manifiesto protegido `FormPublicationManifest{format:1, attemptFingerprint, publicationFingerprint, definitionId, versionId, draftRevision, contentHash, name, prefix, originalCulture, submitLabel, profiles[{channelId, profileRevision, cultures[{culture, compilationFingerprint}]}], maxFileSizeMiB?}`; `LockPrefix()`. **No activa ni llama al proveedor.**

Recibo 202: `{requestId, operationId, definitionId, versionId, versionNumber, draftRevision, state:"Accepted", acceptedAtUtc, recoveryPath}` (la ruta va en el cuerpo; `ToAcceptedResult` nunca pone `Location`).

**Estado** (`GET publications/{requestId}`): sólo el actor de la operación con Read+Publish actuales (ajena o inexistente → 404 `Forms.Publication.NotFound`). Devuelve `{requestId, operationId, definitionId, versionId, versionNumber, draftRevision, state, stage, acceptedAtUtc, updatedAtUtc, nextCheckAtUtc, canRetry:false, requiresReview (= state Uncertain), issues}`; si está `Failed` o `Uncertain` consulta al canal y traduce sus issues por campo/cultura (`Forms.Publication.ChannelUnavailable` si el canal ya no está, `AccountUnavailable` si la consulta falla). Nunca devuelve JSON del Flow, IDs de Meta, WABA ni hashes.

**Preview** (`FormPreviewService`): Read; `culture` obligatoria `es`|`en` (si no, 400 `Forms.Preview.CultureInvalid` con `errors.culture`); versión de la definición (404) en estado `Published` (si no, 400 `Forms.Preview.NotAvailable`); perfil del manifiesto con esa cultura; `GetPreviewAsync` del canal; si el vencimiento ya pasó → 400 `Forms.Preview.Expired`. Respuesta `{url, expiresAtUtc}` con `Cache-Control: no-store` (también en errores). No crea sesión ni token de captura.

### 1.5 Servicios del canal WhatsApp de la Etapa 1

| Pieza | Responsabilidad |
|---|---|
| `WhatsAppStructuredFormChannel` | implementa `IStructuredFormChannel` (`"phone"`). `CheckAsync` es puro (compila); `AcceptAsync` delega en `WhatsAppFlowPublicationService` y traduce `UnsupportedConfiguration`/`PayloadMismatch` a respuesta `Failed` (no se sondea); `GetPublicationAsync`/`GetPreviewAsync` delegan en `WhatsAppFlowStatusService`. `PublicationAvailable = IWhatsAppFlowEnvironment.IsConfigured` |
| `WhatsAppFlowCompiler` (estático) | compila el Flow JSON por cultura, presupuestos, mapping, hash (ver 1.11) |
| `WhatsAppFlowScreenBuilder` (estático) | pantallas múltiples cuando hay preguntas de archivo (Etapa 2B) |
| `WhatsAppFlowValueCodec` (estático) | decodifica valores de `nfm_reply` según el mapping sellado |
| `WhatsAppFlowPublicationPolicy` (estático) | huellas (`RequestFingerprint` formato 2, compatibilidad con formato 1), `CompilationFingerprint`, estado agregado, mapeo de errores guardados |
| `WhatsAppFlowPublicationService` | `IWhatsAppFlowPublicationService`: aceptación idempotente del canal (UoW propia) |
| `WhatsAppFlowDispatchService` | `IWhatsAppFlowDispatchService`: reservar, marcar inicio de efecto, completar efecto/verificación (cada uno su UoW) |
| `WhatsAppFlowStatusService` | `IWhatsAppFlowStatusService`: estado y preview, re-verificando contra Graph cuando está `Published` |
| `WhatsAppFormService` | `IWhatsAppFormService`: selector de formularios y preparación (lectura, sin UoW) |
| `WhatsAppFormSendPolicy` | resuelve y revalida contexto de envío (destinatario, revisión, binding, cuenta, ventana) |
| `WhatsAppFlowSessionIssuer` | crea la sesión y su payload protegido |
| `WhatsAppFormSendService` | `IWhatsAppFormSendService`: encola el Flow (UoW: sesión + salida) |
| `WhatsAppFormRecipientRevision` (estático) | `recipientContextRevision` = `"v1." + SHA-256` de `{Format, Actor, ContactId, UserId, LinkRevision, WaId, UserIdentifier, IsActive, Culture, AccountStamp(ConcurrencyStamp), RoleFingerprint}` (roles ordenados por Id con su `ConcurrencyStamp`) |
| `WhatsAppFlowSessionVerifier` | al despachar: estado Sending, modo Manual, ventana 24 h, sesión vigente, payload legible y coherente, token ↔ hash en tiempo constante |
| `WhatsAppFormMessageDispatchService` | `IWhatsAppFormMessageDispatchService`: arma el mensaje Flow revalidando cuenta, permisos del operador, destinatario y versión |
| `WhatsAppChatDispatchService` | `IWhatsAppChatDispatchService`: reservar/completar/recuperar salidas durables |
| `WhatsAppManualSendGuard` | operador activo con `whatsapp.read` y `whatsapp.send` |
| `WhatsAppFlowReplyService` | `IWhatsAppFlowReplyService`: procesa `nfm_reply` pendientes (ver 1.9) |
| `WhatsAppFlowReplyStateService` | `IWhatsAppFlowReplyStateService`: marca procesado/descargando/falla (cada uno su UoW) |
| `WhatsAppChatQueryService` | muestra en el chat sesiones, respuestas y valores vía `IStructuredRecordSource` |

### 1.6 Publicación del Flow en Meta (de punta a punta)

1. **BusinessForms** acepta (1.4) y deja la operación `Accepted`.
2. `FormPublicationBackgroundService` (Infrastructure) toma hasta 20 operaciones vencidas, reserva la primera disponible (lease 2 min) en un scope, despacha en otro y completa en otro. Si el actor ya no tiene Read+Publish: si estaba `Accepted` → `Failed`/`AuthorizationChanged`; si no, `ReconciliationOnly` (sólo consulta). Manifiesto ilegible o hash del esquema distinto → `Uncertain`/`ManifestUnreadable`.
3. `FormPublicationChannelDispatchService`: si no es reconciliación, `AcceptAsync` del canal (un `Result` de error → progreso `Uncertain`/`ChannelUnavailable` con `RequiresReview`); respuesta `Failed` → se registra tal cual; luego siempre `GetPublicationAsync` (en reconciliación, un `NotFound` → `Failed`/`AuthorizationChanged` sin efecto).
4. **WhatsApp acepta** (`WhatsAppFlowPublicationService.AcceptAsync`): replay por `OperationId` + huella; validaciones: perfil = `flow-static-7.3-r7` vigente, culturas objetivo exactamente `en`/`es`, 1–4 permisos, recompila y compara cada `CompilationFingerprint` (distinto → `PayloadMismatch`); verifica la cuenta fuera de la UoW (`VerifyPhoneAsync`: el `PhoneNumberId` debe estar en `GET {waba}/phone_numbers?fields=id&limit=100`, paginado hasta 10 páginas); no pertenece → respuesta `Failed`/`WhatsApp.Flow.AccountMismatch`. En la UoW con lock `whatsapp-flow:{id}` crea la operación, su manifiesto (formato 2: `requestFingerprint`, `publicationFingerprint`, cuenta `{WabaId, PhoneNumberId, GraphApiVersion, ConfigurationFingerprint}`, permisos requeridos) y un binding por cultura con su proyección protegida `{format:1, json, hash, profileRevision, codecVersion:1, culture, mapping[{fieldId, key, type, required, maxLength, choices, files}]}`.
5. `WhatsAppFlowBackgroundService` (cada 2 s, hasta 20 operaciones, prende con `WhatsApp:ProcessFlowsInBackground`): reserva (lease 2 min, `Fence++`; si el actor perdió un permiso del manifiesto, `SuspendAuthorization` en los bindings no publicados), elige el binding (primero Uncertain/InFlight, luego por cultura) y `Begin(fence)` devuelve el paso. Verifica de nuevo la cuenta (identidad WABA+teléfono+Graph igual al manifiesto, si no `AccountMismatch` → binding `Failed`).
6. Antes de Create/Upload/Publish marca `InFlight` en su propia UoW (`MarkEffectStartedAsync`) y recién después llama a Graph, con un token que vence con el lease. La confirmación usa un token propio de 10 s.

| Paso | Llamada a Graph (`v25.0`, bearer) | Resultado |
|---|---|---|
| Create | `POST {waba}/flows` `{"name":"form_{opId:N}_{culture}","categories":["OTHER"]}` | `id` numérico (1–128 dígitos) → `MetaFlowId`, paso Upload. Éxito sin id → `Uncertain` |
| Upload | `POST {flowId}/assets` multipart: `file`=flow.json (`application/json`), `name`=`flow.json`, `asset_type`=`FLOW_JSON` | `success:true` → Validate; `validation_errors` → `Failed` `WhatsApp.Flow.ValidationFailed` con diagnósticos por identidad |
| Validate / Verify | `GET {flowId}?fields=id,name,status,validation_errors,json_version,endpoint_uri,data_api_version,whatsapp_business_account,health_status`; `GET {phoneNumberId}?fields=id,health_status`; `GET {flowId}/assets` y descarga del asset | si `status=DRAFT` y paso Upload/Validate → Publish; si `PUBLISHED` con todo verificado → binding `Published` |
| Publish | `POST {flowId}/publish` | `success:true` → Verify |

Verificación exige: id, nombre técnico y WABA dueño coinciden; `json_version = "7.3"`; sin `endpoint_uri` ni `data_api_version`; `validation_errors` vacío; `health_status.can_send_message = AVAILABLE` del Flow y del teléfono exacto; un único asset `flow.json`/`FLOW_JSON` cuyo `download_url` sea HTTPS:443, sin userinfo, host `*.fbcdn.net` o exactamente `mmg.whatsapp.net`, descargado **sin bearer ni redirects**, ≤1 MiB, con SHA-256 igual al hash sellado.

Clasificación de efectos (`WhatsAppFlowClient.EffectAsync`): 5xx o 429 → incierto; otro no-2xx → rechazo (`WhatsApp.Flow.ProviderRejected`, `Failed`); circuito abierto/limitador de Polly → `AccountUnavailable` (se libera sin efecto); red/timeout → incierto; cancelación después de marcar el efecto → incierto; antes → `AccountUnavailable`. Un Create incierto **nunca** se repite: queda `Uncertain` y la verificación sin `MetaFlowId` sólo cuenta candidatos por nombre (`InspectUnknownCreationAsync`, `GET {waba}/flows?fields=id,name`, cero/uno/varios) y deja un diagnóstico `channel.creationCandidates.{none|one|multiple}`, sin adoptar por nombre. Un Publish incierto tampoco se repite.

7. Estado neutral (`WhatsAppFlowPublicationPolicy.Status`): algún binding Uncertain → `Uncertain`; alguno Failed → `Failed`; todos Published → `Published`; todos Accepted → `Accepted`; si no `Publishing`. Si dice Published, `WhatsAppFlowStatusService` re-verifica en vivo cuenta y cada binding; si algo no cierra → `Uncertain`/`Verify`/`RequiresReview`.
8. **BusinessForms completa** (`CompleteCoreAsync`, lock global): valida fence/lease/IDs/huella y que haya una respuesta por perfil; decide: sin permisos → `Failed` si todos fallaron sin revisión, si no `Uncertain`; algún Failed → Failed; algún Uncertain o `RequiresReview` → Uncertain; todos Published → Published; si no Publishing. Con Published: `ActivateVersion(BaseActiveVersionId, VersionId)`; si la activa cambió mientras tanto → `DeferActivation` (Uncertain/ActivationConflict, +5 min), si no `Complete` → `Published`.

`canRetry` es siempre `false`: no hay ruta HTTP para repetir un efecto incierto; la recuperación técnica reutiliza la misma operación.

### 1.7 Mandar un formulario desde el chat

**Selector** (`GET /api/whatsapp/chats/{id}/forms`): exige `whatsapp.read` + `whatsapp.send`. Sólo ofrece formularios si el contacto tiene cuenta vinculada (`Contact.UserId`) activa, con `Culture` es/en y con su propio `formrecords.create` (lo verifica `BusinessFormSource`). Elegible = versión **activa** con operación `Published` y presentación sellada completa (`FormSelectionReader.Active()`); búsqueda ILIKE y orden `name` sobre `DisplayNameEs/En` según el idioma **del destinatario**. Respuesta `{page{items[{definitionId, versionId, versionNumber, prefix, name, questionCount, submitLabel, hasCatalogQuestions}], …}, recipientCulture, recipientContextRevision}`. En el hilo, `formEligibility.status` ∈ `NoLinkedAccount | InactiveAccount | NoEligibleForms | Eligible`; sin `whatsapp.send`, `linkedAccount` y `formEligibility` son null; el correo exige además `users.read`.

**Envío** (`POST /api/whatsapp/chats/{id}/form-messages`, cuerpo `{requestId, definitionId, versionId, expectedModeRevision, expectedRecipientContextRevision, message}`):
- Validador: IDs no vacíos, `expectedModeRevision ≥ 0`, revisión de contexto no vacía ≤67, **`message` obligatorio**, ≤1024 caracteres y visible.
- Huella del pedido = SHA-256 de `{Format:1, Actor, ContactId, RequestId, DefinitionId, VersionId, ExpectedModeRevision, ExpectedRecipientContextRevision, Message.Trim()}`. Si ya existe una salida `(contacto, operador, requestId)`: debe ser `Kind=Flow` con sesión de igual huella → mismo resultado; si no → 409 `WhatsApp.Chat.RequestConflict`.
- `ResolveAsync` (fuera de la UoW): contacto (404 `WhatsApp.Chat.NotFound`); destinatario vinculado/activo/es-en y revisión de contexto igual → si no 409 `WhatsApp.Flow.RecipientContextChanged`; operador con read+send; formulario elegible con botón en la cultura del destinatario y binding `Published` con `MetaFlowId` para esa cultura → si no 409 `WhatsApp.Flow.FormUnavailable`; pantalla inicial = `screens[0].id` de la proyección; entorno configurado y cuenta verificada (`AccountUnavailable`/`AccountMismatch` vs. el manifiesto).
- UoW: contacto `FOR NO KEY UPDATE`; `ModeRevision` distinta → 409 `WhatsApp.Chat.RevisionConflict`; modo ≠ Manual → 409 `WhatsApp.Chat.ManualRequired`; ventana de 24 h cerrada (último entrante hace ≥24 h o más de 5 min en el futuro) → 409 `WhatsApp.Chat.WindowClosed`; `wa_id` inválido (8–15 dígitos) → 400 `Users.Phone.Invalid` (en el despacho, en cambio, da `WhatsApp.Chat.DestinationMissing`); `RevalidateAsync` (lock `forms:definitions` vía `LockSelectionAsync`, mismo destinatario/revisión/permisos, mismo binding); replay concurrente; otra salida de conversación `Pending/Sending/Uncertain` → 409 `WhatsApp.Chat.SendPending`; `PrepareAsync(forSending:true)` de catálogos (bloqueo → 409 `WhatsApp.Flow.CatalogPreparationBlocked`); token aleatorio de 32 bytes en hex (64 caracteres); sesión (`ExpiresAtUtc = now + WhatsApp:FlowSessionLifetimeHours`, default 24 h, 1–168) con payload protegido `{format:1, contactId, userId, operatorUserId, definitionId, versionId, bindingId, culture, recipientRevision, modeRevision, metaFlowId, technicalName, expectedHash, token, buttonText, startScreen, account, catalogs}` y salida `WhatsAppOutgoingMessage.CreateFlow` (`Kind=Flow`), **en el mismo commit**.
- 202 con `{id, requestId, operatorUserId, body, requestedAtUtc, state, waMessageId, form{definitionId, versionId, versionNumber, name, culture}, recoveryPath, kind:"Flow", origin}`; `recoveryPath` = `GET /api/whatsapp/chats/{id}/outgoing/{requestId}`. 202 = guardado en cola, no entregado.

**Despacho** (`WhatsAppOutgoingBackgroundService`, cada 2 s, `WhatsApp:ProcessOutgoingInBackground`):
1. `RecoverExpiredAsync`: salidas `Sending` con más de 2 min → `Uncertain` (no se reintentan).
2. `ReserveAsync`: próxima `Pending` (contacto primero, `SKIP LOCKED`); revalida modo Manual, ventana 24 h, operador activo con read+send, número y cuerpo; si algo falla → `Failed` (sin código). Si no, `Start` → `Sending`.
3. Para `Kind=Flow`: `WhatsAppFormMessageDispatchService.PrepareAsync` (sesión/token, cuenta igual a la del payload, permisos del operador, destinatario y revisión, versión **todavía publicada aunque ya no sea la activa** — `FindAcceptedSessionAsync`). Si falla, no se manda nada y la salida termina `Failed`.
4. Lease Redis por destinatario y `POST {phoneNumberId}/messages` (cliente `WhatsAppCloud` con resiliencia estándar **sin reintentos de POST**).
5. `CompleteAsync`: con `wamid` → `Accepted` y crea el `WhatsAppMessage` saliente `Kind=Flow` con `FlowSessionId` y operador, aplicando un receipt previo si existía; sin `wamid`: incierto → `Uncertain`, si no `Failed`. Un envío `Uncertain` nunca se repite: el operador lo revisa en su teléfono y lo marca `Reviewed` (`POST …/outgoing/{requestId}/review`), lo que habilita otro envío; si en realidad no salió, manda uno nuevo con otro `requestId` (y otra sesión).

Mensaje enviado (`Infrastructure/Modules/WhatsApp/WhatsAppMessagePayload.cs`):

```json
{
  "messaging_product": "whatsapp", "recipient_type": "individual", "to": "+549…",
  "type": "interactive",
  "interactive": {
    "type": "flow",
    "body": { "text": "<mensaje del operador>" },
    "action": { "name": "flow", "parameters": {
      "flow_message_version": "3", "flow_id": "<MetaFlowId>", "flow_token": "<64 hex>",
      "flow_cta": "Abrir formulario", "flow_action": "navigate",
      "flow_action_payload": { "screen": "FORM",
        "data": { "field_<N>_options": [ { "id": "<itemId>", "title": "<texto>", "description": "<detalle>" } ] } } } }
  }
}
```

`flow_cta` sale de `FlowTexts.resx` (`OpenForm`: «Abrir formulario» / «Open form»), separado del `submitLabel`. `data` sólo existe si hay preguntas de catálogo (Etapa 2); `description` se omite si no hay detalle. Con `WhatsApp:SendArgentineMobilesWithoutNine` (sólo para el número de prueba de Meta) `+549…` se manda como `+54…`.

### 1.8 Webhook y almacenamiento del `nfm_reply`

`WhatsAppWebhookController` (`/webhooks/whatsapp`, `[OwnProtocol]`): GET de verificación con `hub.*`; POST valida HMAC-SHA256 del cuerpo crudo con `AppSecret` (`X-Hub-Signature-256`, comparación de tiempo constante) → 401 si falla; >5 MB → 413; siempre 200 si está firmado (lo ilegible se saltea).

`WhatsAppWebhookReader` reconoce `type=interactive` + `interactive.type=nfm_reply` + `nfm_reply.name == "flow"` + `response_json` string:
- Tamaño UTF-8 > 16 KiB (o > 64 KiB si trae arrays de archivos) → se guarda igual un **marcador** `{"flow_token":"…","_flow_error":"ResponseTooLarge"}` para poder rechazar con motivo.
- Si no, forma acotada: objeto raíz, profundidad ≤4, nombres únicos de 1–64 caracteres, `flow_token` string no vacío ≤128 obligatorio, el resto sólo `field_{32 hex}` con valor string/número/bool/array, ≤50 campos. Si no cumple, el mensaje **se descarta** (cuenta como ítem salteado, no se guarda).
- `WhatsAppWebhookPersistence` guarda `WhatsAppMessage.Inbound(Kind=FlowReply, Body=null, ReplyId=null, ProtectedFlowResponse=<cifrado por wamid+contacto>)`; si hay arrays de archivos, `MarkFlowFiles(cantidad)`. Duplicados por `wamid` único + locks + reintento único ante 23505.

### 1.9 Captura de la respuesta (WhatsApp → BusinessForms)

`WhatsAppFlowReplyBackgroundService` corre cada `WhatsApp:InboundPollSeconds` (30 s), independiente del procesador de texto, con `ProcessFlowsInBackground`. `WhatsAppFlowReplyService.ProcessPendingAsync` toma hasta **50** `FlowReply` sin procesar, ordenados por fecha, **excluyendo** los que fallaron hace poco (backoff desde la primera falla: 30 s con 1 falla, 90 s con 2, 210 s con 3, 450 s con 4, 930 s con 5). Cada uno corre con un plazo total de **90 s**.

`ProcessOneAsync`:
1. Descifra el JSON; si no se puede → mensaje `Unrecognized`.
2. Parseo: marcador `_flow_error` → `responseTooLarge`; si no, `flow_token` obligatorio, claves únicas; arrays van al diccionario de medios, el resto deben ser `field_*` escalares (strings tal cual; números/bools como texto crudo). Malformado → `Rejected` `WhatsApp.Flow.InvalidValue`.
3. Sesión por `(ContactId, SHA-256(token))`. Sin sesión → `Unrecognized` (o `Rejected` `WhatsApp.Flow.ResponseTooLarge` si era el marcador). Con sesión y marcador → `Rejected` `ResponseTooLarge`.
4. Sesión ya `Rejected`/`Unrecognized` → el mensaje hereda ese estado (no reintenta).
5. Descifra payload de sesión y proyección del binding; deben coincidir contacto, usuario, definición, versión, binding, cultura, **token exacto**, `ExpectedHash == projection.Hash`, `CodecVersion == 1` y mapping bien formado (≤40 campos, claves `field_{FieldId:N}`, tipos conocidos); si no → `Unrecognized`.
6. Decodifica con el **mapping sellado** (no con el JSON de Meta) y `WhatsAppFlowValueCodec`; para preguntas de catálogo, las opciones válidas son los IDs de la oferta sellada en la sesión. Claves que no están en el mapping se pasan tal cual (el sink las rechaza). Error → `Rejected` `WhatsApp.Flow.InvalidValue`.
7. Archivos (Etapa 2B): `WhatsAppFlowMediaPolicy.Decode` (ver 3.3).
8. Si la sesión ya no corresponde al destinatario activo (`LinkRevision` del contacto distinta, cuenta inactiva o distinta) o la fecha firmada por Meta (`OccurredAtUtc`) es ≥ `ExpiresAtUtc`: sólo se busca un **recibo idéntico previo** (`FindExistingReceiptAsync`); si no hay → `Unrecognized`.
9. Con archivos: si ya hay recibo idéntico, se reutiliza sin descargar; si no, `MarkDownloadingAsync` y descarga (3.3).
10. `records.CaptureAsync`. `"Unavailable"` (sin módulo BusinessForms) → registra falla y reintenta. Si no, marca mensaje y sesión: `Completed` (número `PREFIJO-NNNNNN`, 6 dígitos) o `Rejected` con código y, si aplica, la pregunta rechazada (label de la oferta de catálogo o, si no, el label del Flow sellado).
11. Excepciones → `RegisterFailureAsync`: contador saturado en 5 y primera falla; sólo cuando hay **5 fallas y pasaron 15 min** desde la primera se guarda `Rejected` `WhatsApp.Flow.CaptureFailed` (o el código de la falla) y `ProcessedAtUtc`. Una cancelación del worker no cuenta.

**Codec** (`WhatsAppFlowValueCodec.Decode`): vacío o ausente → error si es obligatorio, ausencia si es opcional. `boolean` sólo `"true"`/`"false"`. `decimal`: ≤60 caracteres, patrón `^-?(0|[1-9][0-9]*)([.,][0-9]+)?$` (acepta punto **y** coma, sin miles, sin exponente), coeficiente exacto ≤ 79228162514264337593543950335 y escala ≤28 sin redondear, normalizado a invariante con punto. `date`: `yyyy-MM-dd` exacto. `choice`: GUID formato `D` dentro de las opciones permitidas. `shortText`/`longText`: largo ≤ `maxLength` y texto almacenable (sin NUL ni surrogates sueltos).

**Sink** (`BusinessFormRecordRecorder.CaptureAsync`):
1. Fuera de la UoW: `SourceReceiptId` (= Id de la sesión), actor no vacíos y fecha UTC (si no, `Rejected` `Forms.Record.AuthorizationChanged`). **Recibo previo primero**: si existe con misma definición, versión y huella → se devuelve su resultado (número/estado), aunque el permiso, el vínculo o la sesión ya no estén vigentes.
2. Sin recibo: el destinatario debe tener `formrecords.create` (si no, `Rejected` `AuthorizationChanged`, **sin escribir**). Se preparan y suben los archivos (3.3) fuera de la UoW.
3. UoW: lock `forms:capture:{sourceReceiptId}`; recibo previo con huella distinta → `Rejected` `Forms.Record.ReplayConflict` (sin escribir); con igual huella → mismo resultado. Revalida `formrecords.create` (si no, **recibo rechazado**). Lock global; definición y versión deben existir y pertenecer, y la versión debe ser la activa **o** cualquier versión con operación `Published` (permite responder una sesión de una versión anterior) → si no `InvalidResponse`.
4. Valida archivos contra el esquema, ofertas de catálogo (únicas por pregunta, revisión >0, `Kind` Manual/Filters, pregunta/clave/catálogo coinciden, opciones válidas para la cultura original, nombres válidos) y normaliza respuestas: claves desconocidas, obligatorias ausentes o vacías, texto largo o no almacenable, decimal que no sea exacto en invariante, fecha inválida, bool inválido, choice fuera de opciones/oferta → `Rejected` `Forms.Record.InvalidResponse`.
5. Catálogos Manual: lock de cada catálogo (ordenado) y verificación de ítem activo (si no, `Rejected` `Forms.Record.CatalogItemInactive` con `RejectedFieldId`).
6. `AllocateRecordNumber()`; `BusinessFormRecord` (con `RuleSetId = ActiveRuleSetId` de la definición), un `BusinessFormRecordValue` por valor, referencias de catálogo, archivos, runs de reglas del evento `Received` (Etapa 4) y recibo `Completed` — **todo en el mismo commit**. Un rechazo graba sólo el recibo `Rejected` (con `ErrorCode` y `RejectedFieldId`) y **no consume número**.

Huella del recibo: SHA-256 de `{SourceReceiptId, DefinitionId, VersionId, ActorUserId, Answers ordenadas}` + ofertas de catálogo (ordenadas por FieldId) + metadata de archivos (ordenada por campo/posición; los bytes se ignoran al serializar).

### 1.10 Tablas de respuestas (creadas en Etapa 1)

| Entidad → tabla | Campos clave | Restricciones |
|---|---|---|
| `BusinessFormRecord` → `BusinessFormRecords` (IAuditable) | `DefinitionId`, `VersionId`, `RuleSetId?`, `State` (`Received, PendingApproval, Approved, Rejected`; varchar(32), default Received, **token**), `Number`, `ActorUserId` (quien respondió), `SourceReceiptId` (sesión), `SubmittedAtUtc` (fecha de Meta), `ValuesJson` (jsonb: `[{"fieldId":…,"value":…}]` con el valor tipado) | único `(DefinitionId, Number)`, único `SourceReceiptId`, alterna `(DefinitionId, Id)`, FK a definición, FK compuesta a versión y a revisión de reglas |
| `BusinessFormRecordValue` → `BusinessFormRecordValues` | `RecordId`, `DefinitionId`, `FieldId`, `Type` (≤32), `TextValue` (≤10000), `DecimalValue`, `DateValue`, `BooleanValue`, `ChoiceId` | **CHECK `CK_BusinessFormRecordValues_ExactlyOneValue`** (exactamente una columna de valor no nula); único `(RecordId, FieldId)`; FK compuesta a registro (Cascade) y a `BusinessFormFields` (Restrict) |
| `BusinessFormCaptureReceipt` → `BusinessFormCaptureReceipts` | `SourceReceiptId` (único), `DefinitionId`, `VersionId`, `Fingerprint` (64), `RecordId?`, `Number?`, `State` (`Completed`/`Rejected`, ≤16), `ErrorCode` (≤128), `RejectedFieldId?`, `CreatedAtUtc` | índice `(DefinitionId, VersionId)`; **sin FK** |

Una respuesta confirmada no se modifica: no hay ruta ni método para editarla.

### 1.11 Flow JSON (compilador) — perfil vigente `flow-static-7.3-r7`

Constantes (`Application/Modules/WhatsApp/Configuration/WhatsAppFlowProfile.cs`): `Revision = "flow-static-7.3-r7"`, `GraphApiVersion = "v25.0"`, `CodecVersion = 1`, `MaxResponseJsonBytes = 16 KiB`, `MaxMediaResponseJsonBytes = 64 KiB`, `DecimalPattern`, `DecimalMaxChars = 60`. El código sólo contiene la revisión vigente; r3, r4, r5 y r6 existen **sólo** como bytes sellados en proyecciones de versiones ya publicadas (siguen enviándose y capturándose con su mapping; no se recompilan). Historia según la documentación: r3 piloto inicial; r4 cambió la ayuda decimal; r5 agregó Dropdown dinámico de catálogo; r7 las pantallas de archivos con IDs de pantalla válidos para Meta. El código no permite reconstruir r3–r6.

Sin archivos (una pantalla):

```json
{ "version": "7.3", "screens": [ {
  "id": "FORM", "title": "<nombre>", "terminal": true,
  "data": { "field_<N>_options": { "type": "array", "items": { "type": "object",
      "properties": { "id": {"type":"string"}, "title": {"type":"string"}, "description": {"type":"string"} } },
      "__example__": [] } },
  "layout": { "type": "SingleColumnLayout", "children": [ { "type": "Form", "name": "form", "children": [
      { …control… },
      { "type": "Footer", "label": "<submitLabel>",
        "on-click-action": { "name": "complete", "payload": { "field_<N>": "${form.field_<N>}" } } } ] } ] } } ] }
```

(`data` sólo si hay preguntas de catálogo.) Controles por tipo (`name` = `field_{FieldId:N}`, `label`, `required`):

| Tipo | Control y propiedades | Tope de label | Ayuda |
|---|---|---|---|
| shortText | `TextInput`, `max-chars = maxLength` | 20 | `helper-text` ≤80 |
| longText | `TextArea`, `max-length = maxLength` | 20 | `helper-text` ≤80 |
| decimal | `TextInput`, `input-type:"text"`, `pattern`, `max-chars:60`; si no hay ayuda, `helper-text` = `DecimalHelp` («Ingresá un número; usá punto para los decimales.») | 20 | `helper-text` ≤80 |
| date | `CalendarPicker`, `mode:"single"` | 40 | `helper-text` ≤80 |
| boolean | `RadioButtonsGroup`, `data-source:[{id:"true",title:"Sí"},{id:"false",title:"No"}]` (sin valor inicial) | 30 | `description` ≤300 |
| choice manual | `Dropdown`, `data-source:[{id:"<ChoiceId D>", title:"<label>"}]`, título ≤30 | 20 | **no representable** → `UnsupportedConfiguration` |
| choice catálogo | `Dropdown`, `data-source:"${data.field_<N>_options}"` | 20 | ídem |
| photo / document | ver 3.2 | 80 | `description` ≤300 |

Presupuestos (por cultura; violarlos produce issues, nunca JSON parcial ni recortes): nombre ≤80, `submitLabel` ≤35 (medidos en UTF-16), ≤40 preguntas, componentes + 1 (Form) ≤50 (`WhatsApp.Flow.ComponentBudgetExceeded`), JSON ≤1 MiB. Peor caso de respuesta: `2+12+1+2+128×6 + Σ(42 + valor)` con valor = `2 + maxLength×6` (textos), 62 (decimal), 12 (fecha), 7 (bool), 38 (choice), `2 + maxFiles×2048` (archivos); si supera 16 KiB (64 KiB con archivos) → `WhatsApp.Flow.ResponseTooLarge` en el `maxLength` de cada pregunta de texto. Cada texto sale de su cultura exacta, sin fallback: si falta → `WhatsApp.Flow.MissingTranslation`; si excede → `WhatsApp.Flow.TextTooLong` con `limit`. Configuración imposible (formato, culturas, IDs repetidos, clave ≠ `field_{N}`, tipo, `maxLength`, choice sin opciones/catálogo, archivos inválidos, suma >10) → `WhatsApp.Flow.UnsupportedConfiguration`.

Hash wire = SHA-256 de los bytes UTF-8 exactos del JSON. `CompilationFingerprint` = SHA-256 de `"{profile}\n{graph}\n{codec}\n{wireHash}"`. Cambiar un texto de presentación cambia JSON/hash/huella pero no el perfil.

Diagnósticos de Meta (`validation_errors[].pointers[].path`, hasta 100) se mapean por regex `screens[i].layout.children[0].children[j](.data-source[k])?.<propiedad>` (con puntos o corchetes) a rutas del borrador por identidad (`draft.fields[i].label.es`, `…options[k].label.en`, `…files.minFiles`, `…help.en`, `draft.submitLabel.es`, `name.es`), sin copiar mensajes de Meta.

---

## 2. Etapa 2: catálogos Manual y Filters

### 2.1 Entidades y tablas

#### `BusinessFormCatalog` → `BusinessFormCatalogs` (IAuditable)

| Campo | Tipo | Significado |
|---|---|---|
| `Key` | varchar(32), **único** | `Id:N` |
| `NameJson`, `DescriptionJson` | jsonb | mapas `{es,en}` (ambos idiomas obligatorios en el validador) |
| `DisplayNameEs/En` (≤120), `DisplayDescriptionEs/En` (≤1000) | calculadas `COALESCE(NULLIF("NameJson"->>'es',''),'')` | búsqueda/orden por cultura |
| `FiltersJson` | jsonb? | definición Filters |
| `Kind` | varchar(16): `Manual`/`Filters` | |
| `Revision` | long, **token** | +1 por cambio; tope 2^53−1 |
| `IsActive` | bool | baja lógica (conserva ítems y referencias) |

**CHECK `CK_BusinessFormCatalogs_KindFilters`:** `("Kind"='Manual' AND "FiltersJson" IS NULL) OR ("Kind"='Filters' AND "FiltersJson" IS NOT NULL)`.

#### `BusinessFormCatalogItem` → `BusinessFormCatalogItems` (IAuditable) — sólo Manual

`CatalogId` (FK Restrict), `Code` (≤80, único por catálogo sin distinguir mayúsculas en la lógica; índice único `(CatalogId, Code)`), `TextEs` (obligatorio), `TextEn?`, `DetailEs?`, `DetailEn?` (columnas ≤200/≤500 pero la lógica limita **texto 30 y detalle 300**: `MaxTextLength`, `MaxDetailLength`), `Position` (≥0), `IsActive`. Índice `(CatalogId, IsActive, Position, Id)`. **Restricción única diferida** `AK_BusinessFormCatalogItems_CatalogId_Position UNIQUE ("CatalogId","Position") DEFERRABLE INITIALLY DEFERRED`, creada con SQL a mano en `20261012015620_AddUniqueManualCatalogItemPositions` (EF no modela diferibilidad; permite swaps de posiciones en una transacción). `TextFor/DetailFor(culture)`: inglés si existe, si no español.

#### Definición Filters (`FiltersJson`)

```json
{ "textColumn": "name", "detailColumn": "role" | "active" | "name" | null,
  "conditions": [ { "column": "name", "operator": "contains"|"equals", "value": "…" },
                  { "column": "role", "operator": "has", "value": "<RoleId>" },
                  { "column": "active", "operator": "equals", "value": "true"|"false" } ],
  "excludeRecipient": true, "sortColumn": "name"|"role"|"active", "sortDescending": false }
```

`textColumn` debe ser `name`. Fuente única «users» (`Source = "users"`).

#### `BusinessFormRecordReference` → `BusinessFormRecordReferences`

Hecho histórico por respuesta y pregunta de catálogo: `RecordId`, `DefinitionId`, `FieldId`, `CatalogId` (FK Restrict), `CatalogRevision` (>0), `ItemId` (ítem Manual o Id de usuario en Filters; **sin FK**), `Text` (≤30), `Detail` (≤300), `CatalogName` (≤200), `TranslationsJson` (jsonb?, agregado el 2026-10-17: `{ "es": {text, detail, catalogName}, "en": {…} }`). Único `(RecordId, FieldId)`; FK compuestas a registro (Cascade) y campo (Restrict).

Preguntas de catálogo en el esquema: `type:"choice"`, `options:null`, `source:{catalog:<id>}`. La versión publicada guarda **sólo** el `catalogId`: cambiar ítems no obliga a republicar.

### 2.2 Servicios

| Servicio / helper | Responsabilidad |
|---|---|
| `FormCatalogService` (`IFormCatalogService`) | listar, detalle, ítems, crear/editar Manual (una UoW con lock del catálogo) |
| `FormCatalogFiltersService` (`IFormCatalogFiltersService`) | metadata de columnas/roles y personas de ejemplo; vista previa (sin UoW); crear/editar Filters |
| `FormCatalogAccessGuard` | `formcatalogs.read` (+ `formcatalogs.manage` al escribir) |
| `FormCatalogPreparationPolicy` | resuelve la oferta por destinatario y cultura (usada por `BusinessFormSource.PrepareAsync`) |
| `FormCatalogOfferPolicy` (estático) | valida ofertas (≤200 opciones, vacía si obligatoria, textos 30/300, IDs únicos, cultura original presente) |
| `FormRecordReferencePolicy` (estático) | sella traducciones en la referencia y elige el texto por cultura al leer |

**Manual — crear** (`POST /api/forms/catalogs`): nombre/descripción es+en obligatorios (≤120/≤1000); `items` ≤500, cada uno `{localId, itemId (prohibido al crear), code (≤80), text{es obligatorio ≤30, en opcional}, detail{es,en ≤300}, isActive}`. El cliente **no envía `Position`**: el servidor numera 1..n en orden. Códigos duplicados (sin mayúsculas) → 409 `Forms.Catalog.DuplicateItemCode`. Respuesta 201 `{catalog, itemIdentityMap{localId→itemId}}`.

**Manual — editar** (`PUT /api/forms/catalogs/{id}`): `expectedRevision` obligatorio (atrasado → 409 `Forms.Catalog.RevisionConflict`); `isActive` opcional (omitido conserva); sólo se envían filas modificadas (con `itemId`) y nuevas (sin `itemId`, se agregan al final); total ≤500; `moves` opcional ≤500 de `{itemId, direction:"up"|"down"}` aplicados en orden sobre el orden global `Position+Id` (incluidas altas) y normalizados a posiciones consecutivas; un movimiento fuera de rango → 400 en `moves[i].direction`, ítem inexistente → 404 `Forms.Catalog.ItemNotFound`. Todo bajo un lock y una UoW; un error revierte todo. Catálogo de otro tipo → 409 `Forms.Catalog.ManualItemsUnavailable`.

**Filters**: `GET /api/forms/catalog-filter-options` devuelve `{source:"users", columns[{key, dataType, canDisplay, canFilter, canSort, operators, choices}], sampleUsers}` (`role` con la lista de roles `{key: RoleId, label: nombre actual}`, incluidos roles sin miembros; `active` con Activo/Inactivo); `sampleUsers` (paginado, búsqueda, orden `name`) **sólo** con `formcatalogs.manage`. `POST /api/forms/catalog-previews` `{sampleUserId, filters}` exige Manage; la persona de ejemplo debe ser activa, no borrada y con nombre (si no, 404 `Forms.Catalog.NotFound`); no guarda nada. `POST /api/forms/catalogs/filters` y `PUT /api/forms/catalogs/{id}/filters` (con `expectedRevision`, `isActive` opcional) guardan `FiltersJson`.

SELECT fijo (`FormCatalogFilterReader`, conexión normal, timeout de comando 3 s, valores siempre parametrizados, **no hay SQL configurable** — ADR 0019):

```sql
SELECT users."Id", COALESCE(users."DisplayName",'') AS "Name",
       ARRAY(SELECT roles."Name" FROM "AspNetUserRoles" ur JOIN "AspNetRoles" roles ON roles."Id"=ur."RoleId"
             WHERE ur."UserId"=users."Id" ORDER BY roles."Name", roles."Id")::text[] AS "Roles",
       ARRAY(SELECT ur."RoleId" FROM "AspNetUserRoles" ur WHERE ur."UserId"=users."Id" ORDER BY ur."RoleId")::uuid[] AS "RoleIds",
       users."IsActive" AND NOT users."IsDeleted" AS "Active"
FROM "AspNetUsers" users
WHERE NOT users."IsDeleted" AND NULLIF(BTRIM(COALESCE(users."DisplayName",'')),'') IS NOT NULL
```

Condiciones: `name contains` → `ILIKE` con escape; `name equals` → `=`; `role has` → `@value = ANY("RoleIds")`; `active equals` → `= bool`; `excludeRecipient` → `Id <> @sampleUserId` (en el envío real, el destinatario). Orden por la columna elegida y desempate `Id`; lee hasta 201 filas: más de **200** → `ExceedsLimit` (sin ítems). La vista persistente que existió (`BusinessFormCatalogUsers`) se borró en `20261012215448_DropBusinessFormCatalogUsersView`.

**Listado** (`GET /api/forms/catalogs`): filtros `kind` (`Manual`/`Filters`), `state` (`Active`/`Inactive`/`Unavailable`; `Unavailable` hoy no devuelve nada), búsqueda en nombre/descripción/clave, orden `name|kind|revision|optionsCount|usageCount`. Fila `{id, name, description, origin, optionsCount (ítems activos, sólo Manual), usageCount, state, stateReason, canEdit}`; `filterCounts.byKind{all, manual, filters}` sobre el conjunto filtrado por búsqueda/estado, sin el filtro de origen. `usageCount` = definiciones distintas cuyo **borrador actual** referencia el catálogo (SQL sobre `SchemaJson->'fields'`), `null` sin `formtemplates.read`. Detalle `{id, key, name, description, kind, revision, isActive, itemsCount, usageCount, unavailableReasonCode, filters}`. Ítems paginados con `isActive`, búsqueda en código/texto, orden `position|code|text|isActive`.

**Uso en formularios (K6):** el chequeo de publicación bloquea catálogos inexistentes o inactivos (`Forms.Publication.CatalogInvalid`). Cambiar el origen de una pregunta guardada exige reemplazar la identidad.

### 2.3 Preparación y oferta por destinatario (K7)

`GET /api/whatsapp/chats/{id}/forms/{definitionId}/versions/{versionId}/preparation` (read+send): el destinatario y su cultura salen del vínculo del servidor, nunca del navegador; devuelve `{culture, recipientContextRevision, preparation{catalogs[{fieldId, label, count}], issues[{fieldId, label, code, catalogName, invalidOptionCount}], canSend}}` — **nunca** las opciones.

`FormCatalogPreparationPolicy.PrepareAsync(recipient, culture, definitionId, versionId, forSending)`: lee la versión sellada; si es para enviar toma el lock de cada catálogo (ordenado). Por pregunta de catálogo: inactivo → issue `Forms.Record.CatalogUnavailable`; Manual → ítems activos por `Position, Id` con texto/detalle en la cultura (fallback a es) y traducciones `{es, en?}`; Filters → vista previa con el destinatario como muestra (`ExceedsLimit` → `Forms.Record.CatalogTooLarge`), con `active` localizado y traducciones es/en. Validación: >200 → `CatalogTooLarge`; obligatoria sin opciones → `Forms.Record.CatalogEmpty`; texto vacío/>30, detalle >300, no almacenable, IDs repetidos o falta la cultura original de la versión → `Forms.Record.CatalogOptionInvalid` con cantidad inválida. Sin truncar. Oferta: `{fieldId, key, label, catalogId, revision, kind, catalogName, options[{id, text, detail, translations}], catalogNames{es,en}}`.

Al encolar (1.7) se vuelve a preparar con `forSending:true` dentro de la UoW y la oferta queda **sellada en el payload protegido de la sesión**; el despacho la manda en `flow_action_payload.data` sin volver a consultar el catálogo. La captura sólo admite IDs de esa oferta; Manual re-verifica actividad bajo el lock del catálogo (baja → toda la respuesta `Rejected` `Forms.Record.CatalogItemInactive` sin número; el chat muestra la pregunta, nunca el valor); Filters no vuelve a consultar Usuarios. La referencia guarda catálogo, revisión, ítem, texto, detalle, nombre y traducciones. «Mandar de nuevo» es un envío manual nuevo (no hay reenvío automático).

---

## 3. Etapa 2B: fotos y documentos

### 3.1 Tablas

| Entidad → tabla | Dueño | Campos | Restricciones |
|---|---|---|---|
| `StoredFile` → `StoredFiles` (IAuditable) | **núcleo** | `OwnerUserId`, `Name` (≤255, nombre de descarga saneado), `ContentType` (≤128), `Size` (>0), `Sha256` (64 hex), `StorageKey` (≤512, **única**), `ThumbnailKey` (≤512?) | `CK_StoredFiles_Size` `"Size" > 0`; índice `OwnerUserId` |
| `BusinessFormRecordFile` → `BusinessFormRecordFiles` | BusinessForms | `DefinitionId`, `RecordId`, `FieldId`, `FileId`, `Position` | **CHECK `CK_BusinessFormRecordFiles_Position`** `0 ≤ Position < 10`; único `(RecordId, FieldId, Position)`; único `FileId`; FK compuesta a registro (Cascade) y campo (Restrict); FK a `StoredFiles` (Restrict) |

Columnas en `WhatsAppMessages`: `HasFlowFiles`, `FlowFileCount` (para mostrar «bajando/reintentando» sin valores).

### 3.2 Preguntas de archivo en el Flow

Configuración `files: {minFiles, maxFiles, photoSource}`; `minFiles > 0` ⇔ obligatoria. Guardar admite sumas de `maxFiles` > 10; publicar exige suma ≤10 y almacenamiento disponible. `BusinessForms:MaxFileSizeMiB` (1–25, default 25) se **sella en el manifiesto** al aceptar (fuera del esquema), así un cambio de configuración no altera publicaciones en curso.

`WhatsAppFlowScreenBuilder`: cada pregunta de archivo abre una pantalla nueva; las preguntas siguientes comparten esa pantalla. IDs `FORM`, `FORM_A`, `FORM_B`… (sólo letras y guion bajo, exigencia de Meta). Pantallas intermedias: Footer «Continuar»/«Continue» con `{"name":"navigate","next":{"type":"screen","name":"FORM_A"},"payload":{}}` (nunca archivos en navigate ni init-values). Última pantalla `terminal:true` con `complete` cuyo payload referencia cada campo por su pantalla: `${form.key}` si está en la última, `${screen.FORM_A.form.key}` si no. `data` (catálogos) sólo en la primera pantalla; en pantallas posteriores el Dropdown usa `${screen.FORM.data.<key>_options}`.

Pickers (se quita `required`): `PhotoPicker` con `min-uploaded-photos`, `max-uploaded-photos`, `max-file-size-kb = MiB×1024`, `photo-source` `camera` o `camera_gallery`; `DocumentPicker` con `min/max-uploaded-documents`, `max-file-size-kb`, `allowed-mime-types` = PDF, DOC, DOCX, XLS, XLSX, JPEG, PNG. Label ≤80, `description` ≤300.

### 3.3 Recepción, descarga y verificación

`response_json` con archivos: `field_<N>: [ {"id": <mediaId>, "file_name": "…", "mime_type": "…", "sha256": "…"} ]` (presupuesto 64 KiB).

`WhatsAppFlowMediaPolicy.Decode`: toda clave con array debe ser una pregunta de archivo del mapping; una pregunta de archivo no puede venir como escalar; cantidad entre `minFiles` y `maxFiles`, total ≤10; omitida sólo si `minFiles = 0` (Meta omite la propiedad de la opcional vacía); `id` como **cadena de dígitos o número JSON entero, preservando el lexema** (sin fracciones, exponentes ni signo, ≤128), `file_name` ≤255, `mime_type` ≤128, `sha256` ≤128. Error → `Files.Content.InvalidMetadata` con `fieldId` en metadata → rechazo con nombre de pregunta.

`WhatsAppMediaClient.DownloadAsync` (por archivo, plazo 30 s, sin redirects, sin resiliencia de HttpClient): `GET {graph}/{mediaId}?phone_number_id=…` (≤8 KiB) → `id` igual, `mime_type` igual al declarado (si no `InvalidMetadata`), `sha256` igual (hex o base64; si no `Files.Content.HashMismatch`), `file_size` >0 y ≤25 MiB (si no `TooLarge`), `url` HTTPS:443 sin userinfo ni fragmento con host `lookaside.fbsbx.com` o `mmg.whatsapp.net` (si no `Unavailable`); recién entonces `GET url` con bearer; largo exacto = `file_size`. Errores de red → `Files.Content.Unavailable` (falla temporal: usa el contador de 5 fallas/15 min); cualquier otro error de contenido → `Rejected` definitivo. Total de la respuesta >100 MiB → `Rejected` `Files.Content.TooLarge`.

`BusinessFormFileRecorder.PrepareAsync` (fuera de la UoW; se llama sólo si no hay recibo previo):
- Almacenamiento no disponible → `Files.Content.Unavailable`; >10 archivos o >100 MiB → `TooLarge`; posiciones por pregunta deben ser 0..n−1.
- Por archivo: nombre no vacío ≤255 sin control, `/`, `\`; `IFileContentProcessor.Process(bytes, mime, sha256, MaxFileSizeMiB, imageOnly = foto)`.
- Nombre de descarga: NFC, sin caracteres de formato Unicode ni `<>:"/\|?*`, sin extensión original, recorte de espacios/puntos, `file` si queda vacío, extensión **del MIME verificado** (`.jpg .png .pdf .doc .xls .docx .xlsx`), sin truncar.
- Clave determinística: `responses/{sourceReceiptId:N}/{fieldId:N}/{position}-{sha256hex}`; miniatura: misma clave + `.thumb.jpg`. `IPrivateFileStore.WriteAsync` **sobrescribe** (reintentar re-sube el mismo blob; un replay cambiado tiene otro hash y otra clave).
- Devuelve `StoredFile.Create(owner = quien respondió, …)`; el registro de `StoredFiles` y `BusinessFormRecordFiles` se hace en la UoW de la captura (1.9). Un rechazo no deja filas ni consume número (los blobs subidos quedan huérfanos hasta la limpieza).

`FileContentProcessor` (SkiaSharp, OpenMcdf, BCL):
- Vacío o > máximo → `TooLarge`; SHA-256 (hex o base64) distinto → `HashMismatch` (comparación de tiempo constante).
- JPEG/PNG: el códec debe coincidir con el MIME; si es **foto**: ancho/alto ≤16.384 px y área ≤32 Mi px **antes** de decodificar; decodifica escalado y genera miniatura de lado máximo **320 px**, JPEG calidad 75, fondo blanco, respetando la orientación EXIF. Una imagen subida **como documento** se inspecciona sin rasterizar ni generar miniatura.
- PDF: empieza con `%PDF-` y tiene `%%EOF` en los últimos 1024 bytes. DOC: compuesto CFB con stream `WordDocument` no vacío; XLS: `Workbook` o `Book`. DOCX/XLSX: ZIP con `[Content_Types].xml` (≤64 KiB, XML sin DTD) con `Override` del part principal (`word/document.xml` / `xl/workbook.xml`) y su content-type exacto. Otro tipo o documento dañado → `Files.Content.InvalidType`.

### 3.4 Almacenamiento privado (núcleo)

`AzureBlobFileStore` (`Infrastructure/Files`): un contenedor privado; claves generadas por el servidor; SDK con 2 reintentos (250 ms–2 s, timeout de red 30 s), sin logging ni tracing del SDK. `WriteAsync` (sobrescribe), `WriteOnceAsync` (`IfNoneMatch=*`; `409 BlobAlreadyExists`/`412 ConditionNotMet` → `false`, conserva la primera copia), `OpenReadAsync` (404 → `null`), `DeleteAsync` (`DeleteIfExists`), `ListAsync(olderThan)`. Nunca se generan SAS ni URLs públicas.

Configuración `FileStorage` (`FileStorageOptions`, validada al arrancar): `Enabled` (sin él, `IsAvailable=false`), `ContentContainer` (default `files-content`, 3–63 `[a-z0-9-]` sin guiones dobles ni en los extremos), `CredentialMode` explícito: `ConnectionString` (con `ConnectionString`) o `AzureCli` / `VisualStudio` / `ManagedIdentity` (con `ServiceUri` HTTPS, puerto default, sin userinfo/query/fragmento, path `/`, y `ConnectionString` vacío), `TenantId?`, `ManagedIdentityClientId?`. Sin fallback de credenciales. La API no crea contenedores: el AppHost de Aspire (`AppHost.cs`) levanta Azurite (`AddAzureStorage("storage").RunAsEmulator(...)`, volumen `arquitecturabase-files`, contenedor `files-content`) y pasa `FileStorage__Enabled=true`, `FileStorage__CredentialMode=ConnectionString`, `FileStorage__ContentContainer`, `FileStorage__ConnectionString` (se desactiva con `Azurite:Enabled=false`).

**Limpieza de huérfanos** (`PrivateFileCleanupBackgroundService` cada 1 h → `PrivateFileCleanupService.PurgeAsync`): lista blobs con más de **1 día**, en lotes de **128**; una clave se conserva si `IStoredFileReader.ListReferencedKeysAsync` la encuentra (consulta sólo por el índice de `StorageKey`, derivando la original al quitar `.thumb.jpg`) o si alguna `IPrivateFileRetentionSource` la reclama (`FormPrivateFileRetentionSource`: claves canónicas `form-documents/{runId:N}/{es|en}.pdf` de runs `GeneratePdf` cuya respuesta existe); si una fuente falla, ese lote no se borra. Las respuestas con archivos no tienen vencimiento; la retención del chat no las toca.

### 3.5 Lectura y descarga

`GET /api/forms/records/{recordId}/files/{fileId}/thumbnail` y `/content?download=true|false` (`FormFilesController` → `FormFileService`): `formrecords.read` (antes **y después** de abrir el stream); el archivo debe pertenecer al registro (si no, 404 `Files.Content.NotFound`); miniatura sólo para fotos con `ThumbnailKey`; blob faltante con metadata existente → 404. Respuesta `no-store` + `nosniff`; miniatura `image/jpeg` en línea; foto en línea salvo `download=true`; documentos **siempre** como adjunto con el nombre saneado. Cupo `forms-files-read` (600/min). La lectura de valores (chat, planilla, detalle) devuelve sólo `{fileId, name, contentType, size, hasThumbnail}` por lote.

---

## 4. Etapa 3: planilla, filtros, detalle y Excel

No agrega tablas ni migraciones: lee `BusinessFormRecords`, `BusinessFormRecordValues`, `BusinessFormRecordReferences`, `BusinessFormRecordFiles` y `StoredFiles`.

### 4.1 Servicios

| Servicio / helper | Responsabilidad |
|---|---|
| `FormResponseService` (`IFormResponseService`) | navegación, planilla (GET y POST de búsqueda), facetas, detalle; revalida `formrecords.read` después de leer |
| `FormExportService` (`IFormExportService`) | Excel con presupuesto de 30 s |
| `FormRecordAccessGuard` | `formrecords.read` (+ `formrecords.export`); `StillAllowedAsync` |
| `FormRecordFilterPolicy` (estático) | valida filtros contra la unión de preguntas |
| `FormResponsePresentationPolicy` (estático) | columnas (unión por FieldId, orden de la activa y después retiradas, `introducedInVersion`) y celdas `Answered/Unanswered/NotAsked` |
| `FormRecordPresentationPolicy` | proyección con la versión sellada de cada respuesta |
| `FormRecordOriginPolicy` | une los `IStructuredRecordOriginSource` (canal, remitente, fecha de envío, conversación) |
| `FormExportPolicy` (estático) | celda tipada de Excel |
| `IFormWorkbookWriter` → `ClosedXmlFormWorkbookWriter` (Infrastructure, ClosedXML) | escribe el XLSX |

### 4.2 Reglas de la planilla

- **Navegación** (`GET /api/forms/navigation`): definiciones con versión activa, nombre de la activa en la cultura de UI, orden `name`, `{id, name, prefix, versionNumber}`.
- **Versiones visibles**: la activa, cualquier versión con operación `Published` y cualquier versión que tenga respuestas. Esquemas deserializados una vez por definición.
- **Columnas**: unión por `FieldId`; primero el orden de la versión activa, después las preguntas retiradas (versiones más nuevas primero); `introducedInVersion` = menor número de versión que la tiene; opciones = unión histórica por `ChoiceId` con su label. Cambiar el tipo crea otra identidad (otra columna).
- **Celda**: `NotAsked` (la versión de la respuesta no tenía la pregunta; se muestra «—»), `Unanswered` (estaba y quedó vacía, o lista de archivos vacía), `Answered` (cero y `false` se conservan).
- **Filtros** (AND, por FieldId, hasta 100 condiciones, 200 valores de ≤600): texto `contains`/`equals` (1 valor); decimal y fecha `equals`/`gte`/`lte` (1 valor, invariante / `yyyy-MM-dd`); boolean `in` (`true`/`false`, ≤2); choice `in` (GUIDs, ≤200); para cualquier tipo `answered`/`unanswered`/`notAsked` sin valores. Además: `versionIds` (≤100), `actorIds` (≤100), `receivedFromUtc` inclusivo y `receivedToUtc` **exclusivo** (ambos `DateTimeKind.Utc`), `search` (ILIKE **sólo en valores `longText`**). Orden: `number` (default descendente), `submittedAtUtc`, `actor`, `field` (requiere `sortFieldId`; choice manual se ordena con un `CASE` sobre `ChoiceId` con las etiquetas de la última versión; choice de catálogo por el texto capturado; archivos por cantidad). Filtro inválido → 400 `Forms.Record.FilterInvalid`. Todo se filtra y ordena en SQL antes de contar y paginar, acotado a las versiones del conjunto de columnas.
- Los filtros con valores privados van **por POST** (`records/search`, `filter-counts/search`, `export`); página, tamaño, orden, versiones y columnas pueden ir en la query (`GET records`).
- **Facetas** (`POST definitions/{id}/records/filter-counts/search`, cuerpo `{filter, dimension, fieldId, page, pageSize, sort, search}`): `dimension` ∈ `actor | version | choice | presence`; cada faceta ignora sólo su propia dimensión; conserva opciones históricas con cero; versiones ordenadas por número; catálogo con el último texto capturado por ítem. Respuesta paginada `{id, label, count}`.
- **Detalle** (`GET /api/forms/records/{id}`): `{id, definitionId, versionId, versionNumber, number, name, submittedAtUtc, actor, channel, origin{sourceReceiptId, channelId, senderUserId, senderName, sentAtUtc, conversationId, maskedAddress}, values[{fieldId, key, type, label, value}], history[{kind: Sent|Submitted|Saved, atUtc, actor}]}`. `conversationId` sólo si el lector tiene `whatsapp.read` (antes y después). El valor de choice es `{choiceId, label, catalogId?, catalogName?, detail?}`; archivos `[{fileId, name, contentType, size, hasThumbnail}]`; decimal como string invariante; fecha `yyyy-MM-dd`.
- Planilla (`FormResponseSheetResponse`): `{definitionId, name, prefix, activeVersionNumber, columns, versions, records{items[{id, versionId, versionNumber, number, submittedAtUtc, actor, channel, cells, state}]…}, hasApproval}`. `hasApproval` = la revisión de reglas activa o alguna usada por respuestas pide aprobación.

### 4.3 Exportar a Excel

`POST /api/forms/definitions/{id}/records/export` `{filter, columns?}` (`formrecords.read` + `formrecords.export`, política de concurrencia 1 por usuario):
- Congela el número máximo (`LastRecordNumber`) al empezar; una consulta de IDs ordenados con tope 10.001 (más de **10.000** → 400 `Forms.Record.ExportTooLarge`); hidrata por lotes de **100** revalidando permisos en cada lote; presupuesto **30 s** (→ 400 `Forms.Record.ExportBudgetExceeded`).
- Columnas fijas: Número, Recibida (UTC), Persona, Canal (textos de `Responses.resx` en el idioma del usuario); luego las preguntas elegidas (todas si `columns` es null; columna desconocida → `FilterInvalid`), con «(desde la versión N)» si no existía en la 1.
- Celdas: decimal numérico (formato `0.############################`), fecha `yyyy-mm-dd`, fecha-hora `yyyy-mm-dd hh:mm:ss`, booleano nativo, choice «etiqueta · detalle», archivos «cantidad · nombre; nombre», vacío opcional = celda vacía, inexistente = «—», texto siempre literal.
- Encabezado en negrita, ancho 26, fila 1 fija y autofiltro. Archivo `PREFIJO-yyyyMMdd.xlsx`, `no-store`/`nosniff`. Sin cola, trabajos de fondo ni tablas.

---

## 5. Etapa 4: reglas, aprobación, PDF y avisos

### 5.1 Entidades y tablas

| Entidad → tabla | Campos clave | Restricciones / inmutabilidad |
|---|---|---|
| `BusinessFormRuleSet` → `BusinessFormRuleSets` (IAuditable) | `DefinitionId`, `Revision` (>0), `OriginalCulture`, `RulesJson` (jsonb), `QuestionSchemaJson` (jsonb, preguntas fijadas para resolver placeholders), `HasApproval` | **inmutable**; único `(DefinitionId, Revision)`; alterna `(DefinitionId, Id)`; puntero `FormDefinitions.ActiveRuleSetId` |
| `BusinessFormPdfDesign` → `BusinessFormPdfDesigns` (IAuditable) | `DefinitionId` (FK Cascade), `Revision`, `OriginalCulture`, `DesignJson` | inmutable; único `(DefinitionId, Revision)`; puntero `ActivePdfDesignId` |
| `BusinessFormActionRun` → `BusinessFormActionRuns` (IAuditable) | `DefinitionId`, `RecordId`, `RuleSetId`, `RuleId`, `ActionId`, `Event` (≤24: `Received/Approved/Rejected`), `Kind` (≤32: `RequestApproval/GeneratePdf/NotifyAuthor/NotifyRole`), `Order`, `DependsOnRunId?`, `SnapshotJson` (jsonb, sellado), `OutcomeJson` (jsonb), `State` (`Waiting, Running, Completed, Failed, Uncertain, NotApplicable`), `FailureCode` (≤200), `CanRetry`, `RetryRequested`, `DeliveryReviewRequested`, `DeliveryReviewWasSent?`, `DeliveryReviewExpectedRevision?`, `DeliveryReviewedByUserId?`, `DeliveryReviewedAtUtc?`, `Fence` (**token**), `LeaseExpiresAtUtc`, `NextCheckAtUtc`, `AcceptedAtUtc`, `UpdatedAtUtc`, `CompletedAtUtc` | **único `(RecordId, Event, RuleId, ActionId)`** (idempotencia); índice `(State, NextCheckAtUtc, AcceptedAtUtc)`; alterna `(RecordId, Id)`; FK compuestas a registro, revisión de reglas y run del que depende (misma respuesta) |
| `BusinessFormTask` → `BusinessFormTasks` (IAuditable) | `DefinitionId`, `RecordId`, `RuleSetId`, `RunId`, `RoleId`, `AuthorUserId` (quien respondió), `Revision` (**token**, empieza en 1), `State` (`Open/Approved/Rejected`), `RequestedAtUtc`, `DecidedByUserId?`, `DecidedAtUtc?`, `Reason` (≤500) | único `RunId`; **único filtrado `RecordId WHERE "State" = 'Open'`**; índice `(RoleId, State, RequestedAtUtc, Id)` |
| `BusinessFormDocument` → `BusinessFormDocuments` (IAuditable) | `DefinitionId`, `RecordId`, `RunId`, `FileId` (→ `StoredFiles`), `Culture`, `DesignId?`, `DesignRevision` | **único `(RunId, Culture)`**; `DesignId` presente ⇔ `DesignRevision > 0` |
| `BusinessFormRecord.State` / `RuleSetId` | ver 1.10 | `Received → PendingApproval` una vez; `PendingApproval → Approved/Rejected` una vez |

`RulesJson` (lista de `FormRule`):

```json
[ { "id": "<ruleId>", "event": "Received",
    "conditions": [ { "fieldId": "…", "operator": "in", "values": ["<choiceId>"] } ],
    "actions": [ { "id": "<actionId>", "kind": "NotifyRole", "roleId": "<roleId>",
                   "text": { "es": "Llegó {número}: {Cliente}" }, "includePdf": false },
                 { "id": "…", "kind": "RequestApproval", "roleId": "<roleId>", "text": null, "includePdf": false } ] } ]
```

`DesignJson` (`FormPdfDesign`): `{ "name": {"es": "…", "en": "…"?}, "questionIds": ["…"], "includeApproval": true, "footer": {"es": "…"}|null, "includePhotos": true }`.

### 5.2 Reglas (G1)

`FormRuleService` (`IFormRuleService`), permiso `formrules.manage` (`FormRuleAccessGuard`, revalidado después de leer y dentro de la UoW):
- `GET definitions/{id}/rules`: revisión actual o una vacía con revisión 0 `{id:null, definitionId, revision:0, originalCulture, rules:[], createdAtUtc, author}`.
- `GET …/rules/revisions` (paginado, orden `revision`), `GET …/rules/revisions/{revision}` (404 `Forms.Rule.RevisionNotFound`).
- `GET …/rules/options`: `{definitionId, name, originalCulture, questions (borrador actual + preguntas de versiones históricas, por FieldId), roles[{id,name}], conditionChoices{fieldId → [{id,label}]}}`; las opciones de catálogo son metadata de configuración (Filters con el actor como muestra; Manual hasta 500 ítems), sin conteos ni respuestas.
- `POST …/rules` `{expectedRevision, rules}`: `expectedRevision` 0..2^53−2; ≤20 reglas. En la UoW: lock global; `expectedRevision` ≠ revisión actual (0 si no hay) → 409 `Forms.Rule.RevisionConflict`; normaliza (`FormRulePolicy`); crea revisión +1 con `QuestionSchemaJson` = preguntas de las opciones, `HasApproval` si alguna acción es `RequestApproval`; mueve `ActiveRuleSetId`. Lista vacía = revisión nueva que deshabilita. No toca borrador, versión ni WhatsApp. Respuesta 201.

`FormRulePolicy.Normalize` (errores `Forms.Rule.ContentInvalid` en la ruta, ej. `rules[0].actions[1].kind`):
- IDs de regla y de acción no vacíos y únicos; `event` ∈ `Received|Approved|Rejected`; ≤10 condiciones con la gramática de la planilla (`FormRecordFilterPolicy`), valores ≤200, ≤600 caracteres, almacenables.
- 1–10 acciones. `RequestApproval` y `NotifyRole` exigen `roleId` de un rol existente (si no, 400 `Forms.Rule.RoleUnavailable`); las demás no lo admiten. `RequestApproval` sólo en `Received` y **como última acción** de su regla. `GeneratePdf` sólo en `Approved`. `includePdf` sólo en `NotifyAuthor` y sólo si antes, en la misma regla, hay un `GeneratePdf`.
- Textos (sólo avisos): mapa es/en; el idioma **original** del formulario obligatorio, el otro opcional; ≤1024 caracteres con PDF, ≤4096 sin PDF; placeholders `{número}`/`{number}`, `{motivo}`/`{reason}` o `{<label exacto de una pregunta en esa cultura>}` que debe coincidir con **exactamente una** pregunta (si no, 400 `Forms.Rule.PlaceholderInvalid`); llaves sueltas inválidas.

### 5.3 Motor de acciones (G5)

**Creación de runs** (`FormActionRunRecorder.RecordAsync`, helper sin UoW, se llama **dentro** de la UoW de la captura con evento `Received` y de la decisión de aprobación con `Approved`/`Rejected`):
- Usa la revisión de reglas fijada en la respuesta (`record.RuleSetId`; sin reglas no hace nada). Una decisión tardía conserva la revisión de la captura.
- Por regla del evento: evalúa las condiciones contra la respuesta (`FormRecordConditionEvaluator`); si son falsas, todos sus runs nacen `NotApplicable` («No aplica»). Cada acción depende de la anterior **de su regla** (`DependsOnRunId`). Runs ya existentes para `(regla, acción)` no se duplican.
- Snapshot sellado (`FormActionSnapshot`): `approvalRoleId` (RequestApproval), `pdf` (diseño, culturas, nombre del sistema `IAppName`, autor, decisión) para GeneratePdf, `pdfRunId` (para NotifyAuthor con PDF), `recipients[{userId, culture, body}]` (texto **ya expandido**), `preparationFailureCode`.
- Destinatarios: `NotifyAuthor` → quien respondió (si la cuenta no está activa, igual se usa con la cultura original); `NotifyRole` → usuarios activos del rol (ordenados por Id); ninguno → `Forms.Action.RecipientUnavailable`. Cuerpo más largo que el límite → `Forms.Action.MessageTooLong` (no recorta).
- **Idioma**: el de la cuenta del destinatario sólo si está completo en todos los avisos de la revisión, en el diseño PDF (si la revisión genera PDF: nombre, pie y labels/opciones seleccionadas) y en las traducciones de catálogo usadas; y, si el aviso adjunta PDF, si ese PDF se generó en ese idioma. Si no, **todo** en el idioma original del formulario. Queda fijado en el run.
- Render de placeholders (`FormActionMessagePolicy.Render`): número `PREFIJO-NNNNNN`, motivo de la decisión, valores de la respuesta en la cultura (booleanos Sí/No, choice con label, archivos con nombres separados por coma, decimal y fecha con formato de la cultura, ausente = vacío).

**Worker** (`FormActionBackgroundService`, cada 2 s, `BusinessForms:ActionWorker:ProcessInBackground`): toma hasta **100** runs `Waiting`/`Running` sin lease vigente, con `NextCheckAtUtc` vencido y cuya dependencia esté `Completed`/`NotApplicable`; por cada uno, tres scopes: reservar → efecto → completar; una excepción de uno no corta los demás.

`FormActionDispatchService.ReserveAsync` (UoW, lock global): dependencia terminada; `Reserve` (lease 2 min, `Fence++`, `ReconciliationOnly` si ya estaba `Running`). Si el snapshot trae `preparationFailureCode` → `Failed` sin reintento. `RequestApproval` se resuelve **en la misma reserva** (sin efecto externo): si ya hay tarea abierta → `Failed` `Forms.Action.ApprovalAlreadyPending`; `record.MarkPendingApproval()` (si falla, `Failed`); crea `BusinessFormTask` `Open` con el rol y el autor; run `Completed`. Para los demás arma `FormActionWorkResponse` (con los artefactos PDF del run del que depende).

`FormActionEffectService.ExecuteAsync` (fuera de transacciones; excepción → `Uncertain` `Forms.Action.EffectUnconfirmed`):
- **GeneratePdf**: `FormPdfActionService.ExecuteAsync` (5.5). `NotFound` en ejecución normal o con reintento pedido → genera; en reconciliación sin reintento → `Failed` reintentable.
- **NotifyAuthor/NotifyRole**: sin canales → `Failed` `Forms.Action.ChannelUnavailable` reintentable. Por destinatario y canal: `GetAsync`; si `Uncertain` y hay revisión humana pedida → `ReviewAsync`; si `NotFound` y (no es reconciliación o hay reintento) → `EnqueueAsync` (con el PDF de la cultura del destinatario si corresponde); si `Failed`+`CanRetry` y hay reintento → `RetryAsync(controlRevision = fence)`. Agregado: todos `Accepted` → `Completed`; alguno `Uncertain` → `Uncertain`; alguno `Pending/Sending` → `Running` (próxima consulta +5 s); si no → `Failed` (reintentable si algún estado lo es o hubo `NotFound`).

`CompleteAsync` (UoW, lock global): idempotente si ya está `Completed` con ese fence; exige lease vigente; con PDF completo agrega metadata (`FormPdfDocumentRecorder`: un `StoredFile` y un `BusinessFormDocument` por cultura); `RecordOutcome` (`CanRetry` sólo en `Failed`; `CompletedAtUtc` en `Completed`/`Failed`).

Un run `Uncertain` **nunca** se reserva solo. Controles explícitos (`FormWorkflowService`, `formrecords.read` + `formrules.manage`, revalidados en la UoW con lock global; 202 con el workflow y header `Location: /api/forms/records/{recordId}/workflow`):
- `POST records/{recordId}/actions/{runId}/consult`: `Uncertain`, o `Running` con lease vencido → `Running` sin lease (el worker lo toma en modo reconciliación: sólo consulta, no reenvía). `Completed`/`NotApplicable` → no-op. Otro estado → 409 `Forms.Action.StateConflict`. Run de otra respuesta → 404 `Forms.Action.NotFound`.
- `POST …/retry`: sólo `Failed` con `CanRetry` → `Waiting` con `RetryRequested`. `Completed`/`NotApplicable` o reintento ya pedido → no-op.
- `POST …/review` `{expectedRevision (=Fence), wasSent}`: sólo `Uncertain` de `NotifyAuthor`/`NotifyRole` con fence igual; guarda decisión, actor y fecha del servidor; replay idéntico → no-op; revisión obsoleta → 409. El worker aplica la decisión en el canal: «salió» → salida `Reviewed` (cuenta como aceptada); «no salió» → salida `Failed` `WhatsApp.FormAction.ReviewedNotSent`, reintentable explícitamente.

`GET records/{recordId}/workflow` (`formrecords.read`): `{recordId, recordState, ruleRevision, task{id, roleId, revision, state, roleName, decidedAtUtc, decidedBy, reason}, actions[{id, ruleId, kind, event, order, state, reasonCode, reason (traducido), canConsult, canRetry, createdAtUtc, updatedAtUtc, completedAtUtc, canReview, review{wasSent, reviewedAtUtc, reviewedBy}, revision (=Fence)}], documents[{id, culture, name, size, createdAtUtc}]}`. `canConsult/canRetry/canReview` sólo con `formrules.manage`.

### 5.4 Aprobación (G3/G4)

`FormApprovalService` (`IFormApprovalService`), `FormApprovalAccessGuard`: sesión, `formapprovals.decide` y cuenta activa (revalidado después de leer).
- Bandeja `GET /api/forms/approvals?page&pageSize&definitionId&onlyMine=true`: tareas `Open` de los roles **actuales** del actor; «Para mí» (`onlyMine=true`) excluye las respuestas propias; «Todas» las muestra sin habilitar la decisión. Orden: más vieja primero (`requestedAtUtc`, desempate Id). Fila `{id, definitionId, recordId, number, formName, author, role{id,name}, requestedAtUtc, revision, state, canDecide, cannotDecideReason (AlreadyDecided|OwnResponse), decidedAtUtc, decidedBy, reason}`. `GET …/count`, `GET …/options` (formularios con tareas), `GET …/{id}` (404 `Forms.Approval.NotFound`).
- Decisión `POST /api/forms/approvals/{id}/decisions` `{expectedRevision, decision: "Approved"|"Rejected", reason}`: rechazar exige motivo (1–500, almacenable); aprobar no admite motivo. UoW con lock global: tarea; el actor debe estar **activo y en el rol asignado** (si no, 403 `Forms.Approval.RoleChanged`); la respuesta debe seguir con la misma revisión de reglas (si no, 409 `Forms.Approval.RevisionConflict`); `Decide` (tarea no abierta o revisión distinta → 409 `RevisionConflict`; autor = actor → 403 `Forms.Approval.OwnResponse`; motivo faltante → 400 `ReasonRequired`; otros → 400 `ContentInvalid`); `record.CompleteApproval`; crea los runs del evento `Approved`/`Rejected` con la decisión en el snapshot — **todo en un commit**. Respuesta `{taskId, recordId, revision, state, decidedAtUtc, decidedBy, reason}`.
- `formapprovals.decide` permite ver metadata de la tarea; valores y PDF siguen exigiendo `formrecords.read`.

### 5.5 PDF (G2)

`FormPdfDesignService` (`IFormPdfDesignService`, `formrules.manage`):
- `GET definitions/{id}/pdf-design`: diseño vigente o uno inicial **no guardado** con revisión 0: nombre del formulario, todas las preguntas del borrador, `includeApproval:true`, sin pie, `includePhotos:true`.
- `POST …/pdf-design` `{expectedRevision, design}`: UoW con lock global; revisión atrasada → 409 `Forms.Pdf.RevisionConflict`; normaliza: nombre con el idioma original obligatorio (≤160), traducción opcional, pie opcional (≤1000, si hay debe tener el original), `questionIds` únicos entre las preguntas conocidas (borrador + históricas + las del diseño anterior; una retirada ya seleccionada puede seguir); crea revisión +1 y mueve `ActivePdfDesignId`. 201.
- `POST …/pdf-design/preview` `{design, culture}`: datos de ejemplo (textos «respuesta», 12500.5, `true`, 2026-10-16, primera opción; sin archivos), no guarda nada; si falta nombre, pie o label seleccionado en el idioma pedido, toda la copia sale en el original. `application/pdf`, `no-store`, `nosniff`, `Content-Language` con el idioma usado.

Generación (`FormPdfActionService`):
- `PrepareAsync` (al crear el run): diseño vigente o el inicial; culturas = las completas del diseño ∩ las completas de las traducciones de catálogo usadas.
- `ExecuteAsync`: almacenamiento no disponible → `Failed` `Files.Content.Unavailable`. Por cultura: primero **lee** el blob `form-documents/{runId:N}/{culture}.pdf` (si existe, calcula tamaño y SHA-256 de esos bytes); si no existe y no es reconciliación, renderiza y `WriteOnceAsync` (creación condicional: si otro host ya lo subió, se conserva el primero) y vuelve a leer para confirmar. Todas las culturas presentes → `Completed`; si no `NotFound`. Nombre `"{nombre del diseño}-{PREFIJO}-{NNNNNN}.pdf"` (sin `<>:"/\|?*`).
- `SkiaFormPdfRenderer` (Infrastructure): fuera del preview **lanza** si la decisión del snapshot no es `Approved` o si la cultura no está entre las completas. A4 (595,276 × 841,89 pt), márgenes 42, fuentes Public Sans Regular/Bold OFL embebidas (`Pdf/Fonts`), texto ajustado y paginado sin truncar; encabezado con nombre del sistema y del diseño, número, nombre, «Recibida …» con fecha (formato `d MMM yyyy HH:mm` de es-AR/en-US sobre el valor UTC) y autor; fila por pregunta seleccionada (sólo si existe en la versión capturada; decimal `#,0.###…` y fecha `d MMM yyyy` en es-AR/en-US, Sí/No, texto capturado del catálogo, «sin respuesta» si falta); con «Incluir las fotos», miniaturas privadas (3 por fila, verificando que pertenezcan a la respuesta) debajo de su pregunta, si no, la cantidad; documentos sólo nombrados; bloque de aprobación (quién y cuándo) si `includeApproval`; pie. Textos propios en `Pdf.resx`/`PdfPreview.resx`.
- Ningún PDF de respuesta se emite antes de aprobar (`GeneratePdf` sólo en `Approved`).

Lectura: `GET /api/forms/records/{recordId}/documents/{documentId}/content?download=` (`FormDocumentsController` → `FormPdfDocumentService`): `formrecords.read` antes y después de abrir; pertenencia al registro (404 `Forms.Pdf.NotFound`); `application/pdf`, adjunto sólo con `download=true`, `Content-Language`, `no-store`, `nosniff`, cupo de archivos.

### 5.6 Avisos por WhatsApp (G6)

Puerto `IStructuredRecordActionChannel` → `WhatsAppStructuredRecordActionChannel` (`"phone"`) → `WhatsAppFormActionService`, reutilizando `WhatsAppOutgoingMessages`:

Columnas agregadas a `WhatsAppOutgoingMessages`: `Origin` (`Conversation`/`FormAction`), `RecipientUserId`, `RecipientLinkRevision`, `RecipientWaId` (≤32), `DispatchReservationId`, `MessageEffectStartedAtUtc`, `FailureCode` (≤128), `DocumentStorageKey` (≤512), `DocumentName` (≤255), `DocumentContentType` (≤128, siempre `application/pdf`), `DocumentSize` (≤100 MiB), `DocumentSha256` (64), `DocumentMediaId` (≤128 dígitos), `DeliveryReviewWasSent`, `DeliveryReviewedByUserId`, `DeliveryReviewedAtUtc`, `LastFormActionControlRevision`. Índice **único filtrado `(RequestId, RecipientUserId) WHERE "Origin" = 'FormAction'`** (`RequestId` = `RunId`; operador null).

- **Encolar** (`EnqueueAsync`, UoW, lock `whatsapp-form-action:{run}:{recipient}`): cuerpo visible y ≤4096 (≤1024 con documento, que va como caption), si no `Failed` `WhatsApp.FormAction.InvalidMessage`. Si ya existe la salida del run/destinatario: mismo cuerpo y documento → su estado; distinto → `Failed` `WhatsApp.FormAction.StateConflict`. Contacto del destinatario `FOR NO KEY UPDATE`; `WhatsAppFormActionPolicy.ValidateAsync`: WhatsApp habilitado (`ChannelUnavailable`), contacto vinculado al destinatario (`RecipientUnavailable`), cuenta activa con teléfono confirmado igual al del contacto (`RecipientUnavailable`), ventana de 24 h abierta (`WindowClosed`); en una salida aceptada, mismo contacto, `LinkRevision` y `WaId` (`RecipientChanged`). Error → `Failed` reintentable (salvo `RecipientChanged`). Si no, `CreateFormAction` fija contacto, revisión del vínculo y número. **No exige modo Manual** ni cambia el modo; no bloquea la atención manual (el chequeo de envíos pendientes del chat sólo mira `Origin=Conversation`).
- **Despacho** (mismo `WhatsAppOutgoingBackgroundService`): reserva revalidando la política (falla → `Failed` con código) y `ReserveFormAction` (token `DispatchReservationId`). Documento: lee el PDF privado, verifica tamaño y SHA-256, sube por multipart a `POST {phoneNumberId}/media` (`messaging_product=whatsapp`, `type`, `file`) y persiste el `mediaId` (si la reserva tiene más de 2 min o el token cambió, no se usa); `MarkFormActionMessageEffectAsync` (contacto antes que salida, revalida política) confirma el inicio **antes** del POST de mensaje; envía `type:document` `{id: mediaId, filename, caption}` o `type:text`. Resultado: `wamid` → `Accepted` y `WhatsAppMessage` con `Origin=FormAction` (`AttributeFormAction`, sin operador); fuera de ventana (131047) → `Failed` `WhatsApp.FormAction.WindowClosed`; otro rechazo → `SendRejected`; documento ilegible → `DocumentUnavailable`; subida fallida → `UploadFailed`. Confirmación con token propio de 10 s.
- **Recuperación** de `Sending` vencida (>2 min): sin efecto de mensaje iniciado → vuelve a `Pending` (conserva el `mediaId`); con efecto iniciado → `Uncertain` (nunca se reenvía).
- **Consultar** (`GetAsync`) sólo lee la salida durable; la falta de receipt no es fallo. Estado neutral: `Pending/Sending/Accepted/Failed/Uncertain`, y `Reviewed` con «salió» cuenta como `Accepted`.
- **Revisar** (`ReviewFormAction`): sólo `Origin=FormAction` en `Uncertain` y con `expectedRevision` > `LastFormActionControlRevision`. **Reintentar** (`RetryFormAction(controlRevision)`): sólo `Failed`, revalida política, vuelve a `Pending` y descarta el `mediaId`. Review y Retry comparten el control monotónico. Una aceptación tardía puede confirmar una salida revisada como no enviada.
- **Privacidad en el chat**: sin `formrecords.read` la API devuelve `body` null de mensajes/salidas `FormAction`; el listado de chats usa el preview genérico «Acción del formulario» / «Form action» (`FormActionPreview`).

---

## 6. Etapa 5 (asistente, sugerencias y audios): qué existe

**En el código del commit no hay nada implementado de la Etapa 5.** Verificado: no existen `BusinessFormSuggestions`, `OriginRecordId`, `OriginSuggestionId`, el permiso `whatsapp.configureassistant`, `formsuggestions.manage`, un `Origin` `Assistant` (el enum es `Conversation, FormAction`), transcripción de audios ni capacidades de IA sobre formularios. `docs/plans/formularios/etapa-5-asistente.md` la marca «Diseño elegido; sin empezar» y el ADR 0020 (motor único de IA con capacidades) está aceptado.

Lo que ya existe y la etapa piensa reutilizar:
- `WhatsAppConversationMode { Bot, Manual }`: `Bot` existe en el contrato pero no responde nada (la etapa lo convertirá en «Asistente»).
- Módulo `ArtificialIntelligence` con el asistente de Inicio: operación durable con recibo, fence, cuotas y contenido cifrado; herramientas tipadas de lectura vía `ISystemQuerySource` (`users.summary`, `roles.list`, `roles.permissions`, `business.information`, `system.help`); ninguna toca formularios.
- Salida durable de WhatsApp con `Origin`/`Kind`, descarga de medios de la 2B, planilla/detalle de la 3 y cola de avisos de la 4 (requisitos previos declarados por el plan).

---

## 7. Permisos, configuración y migraciones

### 7.1 Permisos

`BusinessFormsPermissions` (`Domain/Modules/BusinessForms`) y `WhatsAppPermissions`:

| Código | Nombre (es) | Habilita |
|---|---|---|
| `formtemplates.read` | Ver formularios | listar/ver definiciones, preview, recuperar cambio propio |
| `formtemplates.manage` | Administrar formularios | crear y editar borradores (además de read) |
| `formtemplates.publish` | Publicar formularios | chequeo, aceptación y estado de publicación (con read) |
| `formrecords.create` | Completar formularios | **del destinatario**: recibir formularios y que se capture su respuesta |
| `formrecords.read` | Ver respuestas | planilla, detalle, valores en el chat, archivos, PDF, workflow |
| `formrecords.export` | Exportar respuestas a Excel | export (con read) |
| `formcatalogs.read` | Ver catálogos de formularios | catálogos (lectura, metadata de filtros) |
| `formcatalogs.manage` | Administrar catálogos de formularios | crear/editar, vista previa, personas de ejemplo |
| `formrules.manage` | Administrar reglas y PDF | reglas, diseño PDF, consultar/reintentar/revisar acciones |
| `formapprovals.decide` | Aprobar o rechazar respuestas | bandeja y decisiones de tareas de sus roles (no las propias) |
| `whatsapp.read` / `whatsapp.send` / `whatsapp.manage` | chat | listar/leer; responder, elegir y mandar formularios (con read); cambiar modo |

Se componen con `IPermissionCatalog` (núcleo + `IPermissionCatalogSource` de cada módulo). **Seed** (`Infrastructure/Persistence/Seed/RoleSeeder.cs`): el rol `Admin` recibe **todos** los permisos del catálogo instalado (se agregan los que falten en cada arranque); el rol `User` ninguno; la cuenta de `Seed:AdminEmail` recibe Admin. Para que una persona pueda responder hace falta darle `formrecords.create` por un rol.

### 7.2 Configuración (appsettings / variables)

| Sección:clave | Default | Para qué |
|---|---|---|
| `BusinessForms:MaxFileSizeMiB` | 25 (1–25, validado al arrancar) | tope por archivo; se sella al aceptar la publicación |
| `BusinessForms:PublicationWorker:ProcessInBackground` | true | worker de publicación |
| `BusinessForms:ActionWorker:ProcessInBackground` | true | worker de acciones de reglas |
| `FileStorage:Enabled`, `ContentContainer` (`files-content`), `CredentialMode`, `ServiceUri`, `ConnectionString`, `TenantId`, `ManagedIdentityClientId` | deshabilitado | almacenamiento privado (3.4) |
| `WhatsApp:PhoneNumberId` | — | interruptor del módulo (sin él, apagado y las rutas de chat/forms no existen) |
| `WhatsApp:WabaId` | — | cuenta para Flows; sin ella no hay aceptación (`IsConfigured=false`), sí chequeo local. Exige IDs numéricos y `GraphApiVersion = v25.0` |
| `WhatsApp:AccessToken` | — | secreto |
| `WhatsApp:AppSecret` + `VerifyToken` | — | webhook (sólo juntos) |
| `WhatsApp:GraphApiVersion` | `v25.0` | debe coincidir con el perfil para publicar |
| `WhatsApp:FlowSessionLifetimeHours` | 24 (1–168) | vigencia de la sesión desde que entra en cola |
| `WhatsApp:InboundPollSeconds` | 30 (1–3600) | ciclo del worker de respuestas de Flow |
| `WhatsApp:ProcessFlowsInBackground` | true | publicación de Flows y captura de respuestas |
| `WhatsApp:ProcessOutgoingInBackground` | true | salidas durables (texto, Flow, avisos) |
| `WhatsApp:SendArgentineMobilesWithoutNine` | false | sólo número de prueba de Meta |
| `WhatsApp:MessageRetentionDays` | 90 | retención de texto (también vacía `ProtectedFlowResponse` de mensajes procesados y el cuerpo de salidas no pendientes) |
| `RateLimiting:WhatsAppChatReadPermitLimit` / `WhatsAppChatWritePermitLimit` | 600 / 120 | chat y formularios del chat |
| `Azurite:Enabled` (AppHost) | true | emulador local |

Las rutas del chat de formularios (`[WhatsAppRoute(Forms)]`) existen sólo si WhatsApp está habilitado **y** algún `IClientCapabilitySource` publica `forms` (`ConditionalWhatsAppRouteConvention`).

Nota: `appsettings.Development.json` de la Api trae `WhatsApp:BusinessAccountId`, que **ninguna clase lee** (la clave que se usa es `WabaId`, normalmente por user-secrets).

### 7.3 Migraciones relevantes (cadena única)

| Migración | Qué agrega |
|---|---|
| `20261010133250_AddBusinessFormDrafts` | `FormDefinitions`, `FormDraftChanges` |
| `20261010141855_AddBusinessFormPublications` | `BusinessFormFields`, `BusinessFormVersions`, `BusinessFormPublicationOperations`, `FormDefinitions.ActiveVersionId` |
| `20261010150042_AddWhatsAppFlowPublications` | `WhatsAppFlowOperations`, `WhatsAppFlowBindings` |
| `20261010171711_AddFormVersionPresentationAndContactLinkRevision` | presentación sellada de versión, `DisplayNameEs/En`, `WhatsAppContacts.LinkRevision` |
| `20261011030121_AddFormRetiredIdentities` | `BusinessFormRetiredIdentities` (con migración de datos desde el JSON viejo, ida y vuelta) |
| `20261011041238_AddWhatsAppFlowSending` | `WhatsAppFlowSessions`, `FlowSessionId`/`Kind` en mensajes y salidas |
| `20261011163226_AddBusinessFormRecordsAndFlowCapture` | `BusinessFormRecords`, `BusinessFormRecordValues`, `BusinessFormCaptureReceipts`, columnas de captura en mensajes y sesiones, `LastRecordNumber` |
| `20261012001515_AddBusinessFormCatalogs` / `…003554_AddCatalogLocalizedDisplayColumns` | catálogos e ítems; columnas calculadas |
| `20261012015620_AddUniqueManualCatalogItemPositions` | restricción única diferida (SQL a mano) |
| `20261012135445_AddBusinessFormCatalogFilters` / `…215448_DropBusinessFormCatalogUsersView` | `FiltersJson` + CHECK; vista creada y luego eliminada |
| `20261012211548_AddBusinessFormCatalogResponses` | `BusinessFormRecordReferences`, pregunta rechazada en mensajes/sesiones/recibos |
| `20261013164130_AddFlowReplyFailureCount` / `20261015110546_AddFlowReplyFailureAge` | contador y primera falla |
| `20261015150306_AddPrivateFormFiles` / `…153051_AddFlowReplyFilesProgress` / `…190848_AddFlowReplyFileCount` | `StoredFiles`, `BusinessFormRecordFiles`, `HasFlowFiles`, `FlowFileCount` |
| `20261016175412_AddBusinessFormRules` | `BusinessFormRuleSets`, `ActiveRuleSetId` |
| `20261016183004_AddBusinessFormActions` / `…184243_AddBusinessFormApprovalDecisions` | runs, tareas, documentos, `RuleSetId`/`State` del registro; datos de la decisión |
| `20261016190329_AddBusinessFormPdfDesigns` | diseños PDF, `ActivePdfDesignId` |
| `20261016191955_AddWhatsAppFormActions` | columnas de avisos en salidas, `Origin` en mensajes (el `Down` borra salidas `FormAction` y vacía sus mensajes) |
| `20261017143914_AddFormActionDeliveryReviews` | revisión humana en runs y salidas |
| `20261017150354_AddFormReferenceTranslations` | `BusinessFormRecordReferences.TranslationsJson` |

### 7.4 Rutas HTTP (resumen)

Todas MVC, sin versión. Permiso base del controller + adicional de la acción.

| Verbo y ruta | Permisos | Entrada | Salida | Errores propios |
|---|---|---|---|---|
| GET `/api/forms/definitions` | templates.read | query `page, pageSize, search, sort, publicationFilter` | `PagedResult<FormDefinitionListResponse>` | 400 |
| GET `/api/forms/definitions/facets` | templates.read | ídem | `{all, published, drafts}` | |
| GET `/api/forms/definitions/{id}` | templates.read | — | `FormDefinitionDetailHttpResponse` | 404 Definition.NotFound |
| POST `/api/forms/definitions` | read + manage, write | `SaveFormDraftHttpRequest` sin `expectedRevision` | 201 + Location, `FormDraftCommitHttpResponse` | 400 ContentInvalid/TranslationsWouldBeLost/PrefixTaken/IdentityInvalid/QuestionConfigurationInvalid/ResultTooLarge; 409 RequestConflict/IdentityRetired; 410 ChangeExpired |
| PUT `/api/forms/definitions/{id}/draft` | read + manage, write | ídem con `expectedRevision` | 200 ídem | + 404; 409 Draft.RevisionConflict, PrefixLocked |
| GET `/api/forms/draft-changes/{requestId}` | templates.read (actor propio) | — | `FormRecoveredChangeHttpResponse` | 404 ChangeNotFound; 410 ChangeExpired |
| POST `/api/forms/definitions/{id}/publication-checks` | read + publish, write | `{expectedRevision, name, prefix, draft}` | `FormPublicationCheckResponse` | 404; 409 RevisionConflict/DraftMustBeSaved |
| POST `/api/forms/definitions/{id}/publications` | read + publish, write | `{requestId, expectedRevision, checkToken, name, prefix, draft}` | 202 recibo con `recoveryPath` | 400 MissingTranslation/FileStorageUnavailable/TooManyFiles/ChannelUnavailable/AccountUnavailable; 404; 409 PayloadMismatch/CheckExpired/RevisionConflict/DraftMustBeSaved/InProgress/StateConflict |
| GET `/api/forms/definitions/{id}/publications/{requestId}` | read + publish (actor propio) | — | `FormPublicationStatusResponse` | 404 Publication.NotFound |
| GET `/api/forms/definitions/{id}/versions/{versionId}/preview?culture=` | templates.read | `culture` es/en | `{url, expiresAtUtc}`, no-store | 400 CultureInvalid/NotAvailable/Expired, WhatsApp.Flow.PreviewUnavailable; 404 |
| GET `/api/forms/catalogs` · `/{id}` · `/{id}/items` | catalogs.read | queries | página / detalle / ítems | 404 Catalog.NotFound |
| POST `/api/forms/catalogs` · PUT `/{id}` | read + catalogs.manage | Manual | 201 / 200 `FormCatalogSaveResponse` | 400 ContentInvalid; 404 ItemNotFound; 409 RevisionConflict/DuplicateItemCode/ManualItemsUnavailable |
| POST `/api/forms/catalogs/filters` · PUT `/{id}/filters` | read + manage | Filters | 201 / 200 | 409 FiltersUnavailable/RevisionConflict |
| GET `/api/forms/catalog-filter-options` | catalogs.read | page/search/sort | metadata + muestra | |
| POST `/api/forms/catalog-previews` | read + manage | `{sampleUserId, filters}` | `{items, totalCount, exceedsLimit}` | 404 |
| GET `/api/forms/navigation` | records.read | page/search/sort | navegación | |
| GET `/api/forms/definitions/{id}/records` · POST `…/records/search` | records.read | query / filtro completo | `FormResponseSheetResponse` | 400 FilterInvalid; 404 Record.NotFound |
| POST `/api/forms/definitions/{id}/records/filter-counts/search` | records.read | faceta | `PagedResult<{id,label,count}>` | 400; 404 |
| GET `/api/forms/records/{id}` | records.read | — | `FormResponseDetailResponse` | 404 |
| POST `/api/forms/definitions/{id}/records/export` | records.read + export, concurrencia 1 | `{filter, columns}` | XLSX | 400 ExportTooLarge/ExportBudgetExceeded/FilterInvalid; 404; 429 |
| GET `/api/forms/records/{recordId}/files/{fileId}/thumbnail` · `/content?download=` | records.read, files | — | binario | 404 Files.Content.NotFound |
| GET/POST/GET `/api/forms/definitions/{id}/rules`, `/revisions`, `/revisions/{revision}`, `/options` | rules.manage | `{expectedRevision, rules}` | `FormRuleSetResponse` (201 al guardar) | 400 Rule.ContentInvalid/RoleUnavailable/PlaceholderInvalid; 404; 409 Rule.RevisionConflict |
| GET/POST `/api/forms/definitions/{id}/pdf-design`, POST `/preview` | rules.manage | `{expectedRevision, design}` / `{design, culture}` | diseño (201) / PDF | 400 Pdf.ContentInvalid; 404; 409 Pdf.RevisionConflict |
| GET `/api/forms/records/{recordId}/workflow` | records.read | — | `FormWorkflowResponse` | 404 |
| POST `/api/forms/records/{recordId}/actions/{runId}/consult` · `/retry` · `/review` | records.read + rules.manage, write | review: `{expectedRevision, wasSent}` | 202 + Location del workflow | 404 Action.NotFound; 409 Action.StateConflict |
| GET `/api/forms/records/{recordId}/documents/{documentId}/content?download=` | records.read, files | — | PDF | 404 Pdf.NotFound |
| GET `/api/forms/approvals`, `/count`, `/options`, `/{id}` | approvals.decide | `definitionId, onlyMine, page, pageSize` | bandeja | 400; 404 |
| POST `/api/forms/approvals/{id}/decisions` | approvals.decide, write | `{expectedRevision, decision, reason}` | `FormApprovalDecisionResponse` | 400 ReasonRequired/ContentInvalid; 403 OwnResponse/RoleChanged; 404; 409 Approval.RevisionConflict |
| GET `/api/whatsapp/chats/{id}/forms` | whatsapp.read + send | `page, pageSize (default de ajustes), sort, search` | `WhatsAppFormsResponse` | 404 Chat.NotFound |
| GET `/api/whatsapp/chats/{id}/forms/{definitionId}/versions/{versionId}/preparation` | whatsapp.read + send | — | `WhatsAppFormPreparationResponse` | 404; 409 Flow.RecipientContextChanged/FormUnavailable |
| POST `/api/whatsapp/chats/{id}/form-messages` | whatsapp.read + send, chat write | `{requestId, definitionId, versionId, expectedModeRevision, expectedRecipientContextRevision, message}` | 202 `WhatsAppOutgoingMessageResponse` | 404; 409 Chat.RequestConflict/RevisionConflict/ManualRequired/WindowClosed/SendPending, Flow.RecipientContextChanged/FormUnavailable/CatalogPreparationBlocked; 400 Flow.AccountUnavailable/AccountMismatch, Users.Phone.Invalid |
| GET `/api/whatsapp/chats/{id}/outgoing/{requestId}` | whatsapp.read (sólo salidas del mismo operador) | — | estado de la salida (incluye `form` si es Flow) | 404 |
| POST `/api/whatsapp/chats/{id}/outgoing/{requestId}/review` | whatsapp.read + send | — | 204: el operador marca su salida `Uncertain` como `Reviewed` (no reenvía ni afirma entrega; destraba nuevos envíos) | 409 |
| GET/POST `/webhooks/whatsapp` | firma HMAC | Meta | 200/401/413 | |

Los contratos `*HttpRequest` de BusinessForms llevan `[JsonUnmappedMemberHandling(Disallow)]` (campos desconocidos → 400 `Request.Invalid`), se mapean a mano y redactan `ToString()`.

### 7.5 Catálogo de códigos de error

- **Forms.Draft.*:** `RevisionConflict` (409), `QuestionTypeUnavailable` (400), `IdentityInvalid` (400), `IdentityRetired` (409), `QuestionConfigurationInvalid` (400), `ChoiceLabelRequired` (400), `RequestConflict` (409), `ChangeNotFound` (404), `ChangeExpired` (410), `TranslationsWouldBeLost` (400), `ContentInvalid` (400), `ResultTooLarge` (400).
- **Forms.Definition.*:** `Unauthorized` (401), `Forbidden` (403), `NotFound` (404), `PrefixTaken` (400), `PrefixLocked` (409).
- **Forms.Publication.*:** `TooManyFiles`, `FileStorageUnavailable`, `AccountUnavailable`, `MissingTranslation`, `ChannelUnavailable`, `CatalogInvalid` (400); `CheckExpired`, `PayloadMismatch`, `InProgress`, `StateConflict`, `DraftMustBeSaved` (409); `NotFound` (404).
- **Forms.Preview.*:** `CultureInvalid`, `NotAvailable`, `Expired` (400).
- **Forms.Catalog.*:** `NotFound`, `ItemNotFound` (404); `RevisionConflict`, `Inactive`, `ManualItemsUnavailable`, `FiltersUnavailable`, `DuplicateItemCode` (409); `ContentInvalid` (400).
- **Forms.Record.*:** `StateConflict`, `CatalogUnavailable`, `CatalogTooLarge`, `CatalogEmpty`, `CatalogOptionInvalid`, `ReplayConflict` (409); `ExportTooLarge`, `ExportBudgetExceeded`, `FilterInvalid`, `CatalogItemInactive`, `InvalidResponse` (400); `NotFound` (404); `AuthorizationChanged` (403). Los de captura viajan como `errorCode` del recibo, no como HTTP.
- **Forms.Rule.*:** `ContentInvalid`, `RoleUnavailable`, `PlaceholderInvalid` (400); `RevisionConflict` (409); `RevisionNotFound` (404).
- **Forms.Approval.*:** `NotFound` (404), `RevisionConflict` (409), `OwnResponse`, `RoleChanged` (403), `ContentInvalid`, `ReasonRequired` (400).
- **Forms.Pdf.*:** `ContentInvalid` (400), `RevisionConflict` (409), `NotFound` (404).
- **Forms.Action.*** (códigos de runs): `MessageTooLong`, `StateConflict` (409 por HTTP), `NotFound` (404), `ApprovalAlreadyPending`, `ChannelUnavailable`, `RecipientUnavailable`, `EffectUnconfirmed`.
- **Files.Content.*:** `HashMismatch`, `InvalidType`, `TooLarge`, `InvalidMetadata`, `Unavailable` (400 si llegan a HTTP; en captura son códigos de rechazo), `NotFound` (404).
- **WhatsApp.Flow.*:** `CatalogPreparationBlocked`, `RecipientContextChanged`, `FormUnavailable`, `StateConflict`, `PayloadMismatch` (409); `ProviderRejected`, `ValidationFailed`, `AuthorizationChanged`, `PreviewUnavailable`, `AccountUnavailable`, `AccountMismatch`, `UnsupportedConfiguration`, `TextTooLong`, `MissingTranslation`, `ComponentBudgetExceeded`, `ResponseTooLarge`, `CaptureFailed`, `InvalidValue` (400); `NotFound` (404).
- **WhatsApp.FormAction.*:** `ChannelUnavailable`, `RecipientUnavailable`, `RecipientChanged`, `WindowClosed`, `DocumentUnavailable`, `UploadFailed`, `SendRejected`, `ReviewedNotSent`, `StateConflict`, `InvalidMessage`.
- **WhatsApp.Chat.*** usados: `NotFound`, `ManualRequired`, `RevisionConflict`, `SendPending`, `WindowClosed`, `DestinationMissing`, `RequestConflict`.

Textos traducidos en `Application/Resources/Errors.resx` / `Errors.en.resx` (español rioplatense con voseo).

---

## 8. Trampas que el código resuelve

1. **Replay antes que cualquier otra validación.** Borrador, aceptación de publicación y captura buscan primero el comprobante del mismo actor/UUID (o sesión) y devuelven el resultado original aunque la revisión, el permiso o el vínculo hayan cambiado; con otro contenido, 409. (`FormDraftService.SaveAsync`, `FormPublicationAcceptanceService.AcceptCoreAsync`, `BusinessFormRecordRecorder.CaptureAsync`)
2. **El UUID nunca se libera.** Al vencer los 30 días, `ExpireResult` borra sólo el payload cifrado; la fila queda y un reuso da 410. (`Domain/Modules/BusinessForms/BusinessFormDraftChange.cs`)
3. **Un 404 de recuperación no prueba rollback**: el front conserva UUID y cuerpo y repite el mismo pedido. (`FormDraftChangesController`, AGENTS de Api)
4. **Traducciones que se perderían en silencio**: omitir una clave con texto previo falla; vaciar exige `""` explícito. (`FormDraftPolicy.LostTranslations`)
5. **Resurrección de identidades**: el ledger impide reusar FieldId/ChoiceId retirados; las opciones no cambian de pregunta; cambiar tipo u origen exige `replacesFieldId`. (`FormDraftPolicy.Resolve`, `BusinessFormRetiredIdentities`)
6. **El cliente no inventa IDs**: todos los IDs nuevos los genera el servidor; el chequeo rechaza IDs nuevos o reemplazos (`DraftMustBeSaved`). (`FormPublicationSnapshotPolicy.Matches`)
7. **Campos y versiones sólo al aceptar**, nunca al guardar o chequear; si un `BusinessFormField` ya registrado cambió de tipo/clave, la aceptación falla. (`FormPublicationAcceptanceService`)
8. **Versión inmutable**: `SealSchema`/`SealPresentation`/`SealManifest`/`SealProjection`/`SealPayload`/`SealSnapshot` lanzan si ya tenían contenido. (Domain)
9. **Activación con CAS por versión base**: si otra publicación se activó entretanto, la operación queda `Uncertain/ActivationConflict` y no pisa la activa; un borrador más nuevo se conserva. (`BusinessFormDefinition.ActivateVersion`, `FormPublicationDispatchService.CompleteCoreAsync`)
10. **Una sola publicación abierta por definición** (índice único filtrado) y la operación incierta **bloquea nuevas publicaciones** hasta que el worker la resuelva; no hay botón de reintento. (`BusinessFormPublicationOperationConfiguration`)
11. **Fence en todo efecto durable**: un worker con lease vencido no puede completar (`OwnsLease`, `StateConflict`); el completado ya confirmado con el mismo fence es idempotente. (`BusinessFormPublicationOperation`, `BusinessFormActionRun`, `WhatsAppFlowOperation`)
12. **Nunca repetir un POST incierto a Meta**: `InFlight` se confirma en su propia transacción justo antes de llamar; Create sin id confirmado no se repite ni se adopta por nombre; Publish incierto no se repite; el cliente quita los reintentos de POST. (`WhatsAppFlowBinding`, `WhatsAppFlowBackgroundService`, `WhatsAppInfrastructureRegistration`)
13. **Distinguir rechazo seguro de incertidumbre**: circuito abierto/limitador o cancelación antes del efecto liberan el paso; 5xx/429/timeouts después del efecto quedan inciertos. (`WhatsAppFlowClient.EffectAsync`)
14. **Rechazos locales del canal no se sondean**: `UnsupportedConfiguration` y `PayloadMismatch` del Accept se traducen a `Failed`. (`WhatsAppStructuredFormChannel.AcceptAsync`)
15. **Cuenta equivocada**: antes de aceptar, de cada efecto, de mandar y de despachar se verifica que el `PhoneNumberId` pertenezca al `WabaId` (Graph paginado) y que la identidad WABA+teléfono+Graph sea la sellada; nunca se publica en otra cuenta en silencio. (`WhatsAppFlowEnvironment`, `WhatsAppFormSendPolicy`, `WhatsAppFormMessageDispatchService`, `WhatsAppMessagePayload`)
16. **Asset verificado byte a byte** desde hosts permitidos, sin bearer ni redirects, ≤1 MiB; `health_status` del teléfono se consulta aparte (Graph v25.0 rechaza `phone_number` dentro del Flow). (`WhatsAppFlowClient.VerifyAsync`)
17. **Huella con orden de culturas histórico**: el manifiesto formato 1 tenía el hash con `es,en`; el despacho actual manda `en,es` y se acepta igual. (`WhatsAppFlowPublicationPolicy.MatchesOriginalTargetCultureOrder`)
18. **Sin fallback de idioma en el Flow**: cada texto sale de su cultura exacta; si falta uno, no se genera ningún Flow. La referencia original del listado no habilita fallback. (`WhatsAppFlowCompiler.Resolve`)
19. **Sí/No sin valor inicial y ausencia ≠ false**; el decimal se valida exacto sin redondeo (BigInteger, escala ≤28) y acepta coma y punto aunque la ayuda pida punto. (`WhatsAppFlowValueCodec`, `BusinessFormRecordRecorder.Normalize`)
20. **Presupuesto de respuesta de Meta**: el compilador calcula el peor caso para no publicar formularios cuya respuesta superaría 16/64 KiB; el webhook igual guarda un marcador redactado para rechazar con motivo. (`WhatsAppFlowCompiler`, `WhatsAppWebhookReader.OversizedResponseMarker`)
21. **IDs de pantalla válidos para Meta**: sólo letras y guion bajo (`FORM`, `FORM_A`…), también en `navigate` y en las referencias de `complete`. (`WhatsAppFlowScreenBuilder.ScreenId`)
22. **El mapping sellado manda, no el JSON de Meta**: claves `field_{FieldId:N}`, tipos y opciones vienen de la proyección; claves extra se pasan al sink para que las rechace. (`WhatsAppFlowReplyService.DecodeAnswers`)
23. **Token por hash**: se guarda SHA-256 del token y se compara en tiempo constante; el token sólo vive en el payload cifrado. (`WhatsAppFlowSessionIssuer`, `WhatsAppFlowSessionVerifier`)
24. **A → sin vínculo → A**: `LinkRevision` distingue revinculaciones; una sesión de un vínculo anterior sólo recupera un recibo idéntico. (`WhatsAppContact.LinkUser/UnlinkUser`, `WhatsAppFlowSessionReader.FindReplySessionAsync`)
25. **Vencimiento con la hora de Meta**: la sesión se compara con `OccurredAtUtc` firmado por Meta, no con la hora de proceso (una respuesta enviada a tiempo y procesada tarde se captura). (`WhatsAppFlowReplyService.ProcessOneAsync`)
26. **Sesión de una versión anterior**: se puede despachar y capturar aunque ya haya otra versión activa, mientras la versión tenga operación `Published`. (`BusinessFormSource.FindAcceptedSessionAsync`, `BusinessFormRecordRecorder.CaptureCoreAsync`)
27. **Estado de mensaje y de sesión separados**: un replay alterado no se presenta como completado; `CompleteCapture` es terminal y no se sobrescribe. (`WhatsAppFlowSession.CompleteCapture`, `WhatsAppMessage.SetFlowReplyCapture`)
28. **Número sin huecos por rechazos**: el número se asigna dentro del commit, bajo el lock global, después de todas las validaciones; un rechazo o una falla al insertar referencias/archivos no consume número. (`BusinessFormRecordRecorder`)
29. **Recibo antes de permisos**: una respuesta ya confirmada se recupera aunque después se revoque `formrecords.create`, se desvincule el contacto o venza la sesión. (`BusinessFormRecordRecorder.FindExistingReceiptAsync`)
30. **Permiso revocado durante la lectura**: los servicios releen el permiso **después** de consultar (y de abrir streams) y descartan el resultado. (`BusinessFormRecordSource`, `FormResponseService`, `FormFileService`, `FormPdfDocumentService`, `FormExportService`)
31. **Fallas transitorias acotadas**: 5 intentos y al menos 15 min desde la primera falla persistida antes de rechazar; backoff 30/90/210/450/930 s filtrado en SQL antes del `Take`; las cancelaciones del worker no cuentan. (`WhatsAppMessage.RegisterFlowReplyFailure`, `WhatsAppFlowSessionReader.RetryDueAt`)
32. **Sin módulo BusinessForms**, el sink devuelve `Unavailable` y la respuesta sigue reintentándose con el mismo contador en lugar de perderse. (`UnavailableStructuredRecordSink`, `WhatsAppFlowReplyService`)
33. **Respuestas malformadas firmadas se descartan** en el webhook (no se guardan) para que Meta no reintente siete días. (`WhatsAppWebhookReader.IsBoundedFlowResponse`)
34. **Oferta de catálogo fijada por destinatario**: se resuelve con el usuario vinculado del servidor (nunca del navegador), se sella en la sesión dentro del mismo commit que la salida y la captura sólo admite esos IDs; una baja Manual rechaza sin número. (`FormCatalogPreparationPolicy`, `WhatsAppFormSendService`, `BusinessFormRecordRecorder`)
35. **Swap de posiciones**: la unicidad `(CatalogId, Position)` es diferida para poder reordenar en una transacción; EF no la modela. (`20261012015620_AddUniqueManualCatalogItemPositions`)
36. **Filters sin SQL configurable**: SELECT fijo, columnas y operadores de una lista cerrada, valores como parámetros, timeout de 3 s y tope de 200 filas detectado leyendo 201. (`FormCatalogFilterReader`)
37. **Archivo como foto vs. como documento**: sólo las fotos se decodifican (con límites de 16.384 px y 32 Mi px antes de reservar memoria) y tienen miniatura; una imagen como documento no se rasteriza. (`FileContentProcessor.Process`)
38. **ID de medio numérico**: Meta puede mandar el id como número JSON; se conserva el lexema sin convertir (evita perder precisión). (`WhatsAppFlowMediaPolicy.ReadMediaId`)
39. **Descargas seguras**: el bearer sólo se adjunta después de validar el host de Meta; sin redirects; tamaño, MIME y SHA-256 deben coincidir con los metadatos. (`WhatsAppMediaClient`)
40. **Subir antes de la transacción y confirmar adentro**: claves determinísticas por recibo/campo/posición/hash; reintentar sobrescribe el mismo blob; un replay cambiado no puede pisar un original confirmado. (`BusinessFormFileRecorder.PrepareAsync`)
41. **Blob faltante ≠ falla de infraestructura**: con metadata pero sin blob → 404; otras fallas de almacenamiento siguen siendo 500. (`FormFileService`, `AzureBlobFileStore.OpenReadAsync`)
42. **Limpieza que no borra lo pendiente**: un día de gracia, lotes de 128 y fuentes de retención (PDF subido antes de confirmar su metadata se conserva mientras exista la respuesta); si una fuente falla, el lote no se borra. (`PrivateFileCleanupService`, `FormPrivateFileRetentionSource`)
43. **PDF idempotente**: primero se lee la copia existente; la escritura condicional conserva el primer archivo; el tamaño y el hash se calculan de los bytes guardados, no de los generados. (`FormPdfActionService.ExecuteAsync`, `AzureBlobFileStore.WriteOnceAsync`)
44. **Idioma común sin mezclar**: aviso, PDF y referencias de catálogo deben estar completos en el idioma del destinatario; si falta cualquier texto, todo sale en el original y queda fijado en el run. (`FormActionRunRecorder.CompleteCulturesAsync`, `FormActionMessagePolicy.Culture`)
45. **Placeholders por identidad**: se validan contra un único label por cultura al guardar y se resuelven con las preguntas fijadas en la revisión, aunque después cambien las etiquetas. (`FormRulePolicy.ValidPlaceholders`, `BusinessFormRuleSet.QuestionSchemaJson`)
46. **Decisión tardía**: la aprobación conserva la revisión de reglas de la captura y falla si la respuesta cambió de revisión; el autor nunca decide su propia respuesta; quien perdió el rol recibe 403. (`FormApprovalService.DecideAsync`, `BusinessFormTask.Decide`)
47. **Tarea única por respuesta**: índice único filtrado de tareas abiertas y `ApprovalAlreadyPending` si una regla pide otra. (`BusinessFormTaskConfiguration`, `FormActionDispatchService`)
48. **Runs idempotentes por respuesta/evento/regla/acción** y dependencias sólo dentro de la regla; un `Uncertain` no se sondea solo; consultar nunca reenvía. (`BusinessFormActionRunConfiguration`, `BusinessFormActionRun.Reserve/RequestConsult`)
49. **Avisos del sistema sin operador**: la unicidad `(ContactId, OperatorUserId, RequestId)` no sirve con operador null, por eso existe el índice filtrado `(RequestId, RecipientUserId) WHERE Origin='FormAction'`. (`WhatsAppOutgoingMessageConfiguration`)
50. **Destinatario fijado al encolar**: contacto, `LinkRevision` y número quedan en la salida; un cambio de teléfono o vínculo da `RecipientChanged` y no se reintenta. (`WhatsAppOutgoingMessage.CreateFormAction`, `WhatsAppFormActionPolicy`)
51. **Dos fases en el envío de documentos**: vencer durante la subida vuelve a `Pending` conservando el `mediaId`; vencer después de iniciar el POST de mensaje queda `Uncertain`. (`WhatsAppOutgoingMessage.RecoverExpiredFormAction`)
52. **Control monotónico**: revisión humana y reintento comparten `LastFormActionControlRevision`; un control viejo no actúa sobre otro intento. (`WhatsAppOutgoingMessage.ReviewFormAction/RetryFormAction`)
53. **Instancias rastreadas viejas**: tras tomar el lock se recargan las salidas ya leídas en el scope, porque otro host pudo completarlas. (`WhatsAppFormActionRepository.LockAsync/GetByRunForUpdateAsync`)
54. **Retención que vacía cuerpos**: a los 90 días se borran cuerpos de salidas no pendientes; un reintento de aviso con cuerpo vacío falla `InvalidMessage` sin reintento. (`WhatsAppMessageRetentionRepository`, `WhatsAppFormActionService.RetryAsync`)
55. **Privacidad de valores en el chat**: sin `formrecords.read` se ve estado y número pero no valores, y se ocultan cuerpos de avisos; el listado usa previews fijos. (`WhatsAppChatQueryService`)
56. **Orden de locks**: actor/UUID → global de definiciones; contacto → salida; catálogos ordenados; las claves se toman ordenadas y sin repetir. (`FormAdvisoryLockKeys`, `WhatsAppConversationRepository.GetNextPendingAsync`)
57. **Concurrencia de exportación**: un límite de concurrencia por usuario (429) y número máximo congelado al inicio para que respuestas nuevas no desplacen la paginación. (`BusinessFormsApiRegistration`, `FormExportService`)
58. **Columnas y filtros acotados a un snapshot de versiones** para no mezclar una publicación concurrente en la misma consulta. (`FormResponseReader.Filter`)
59. **Texto almacenable**: se rechazan NUL y surrogates sueltos en todo lo que va a PostgreSQL (texto de respuestas, referencias, motivos, IDs). (`Domain/Common/StorableText`, usos en entidades)
60. **Presentación de versiones viejas**: un participante del seed completa `NameJson`/botón/cultura faltantes desde el manifiesto protegido de su misma operación, verificando pertenencia y hash, sin tocar el esquema. (`Infrastructure/Modules/BusinessForms/Persistence/Seed/FormVersionPresentationSeeder.cs`)
