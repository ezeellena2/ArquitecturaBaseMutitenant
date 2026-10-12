# E12 · Formularios: crear, publicar, mandar y recibir (spec)

**Estado:** spec escrita el 2026-10-12, antes de las etapas 11, 9 y 10. No es el plan de la etapa: `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E11]**, **[E9]** y **[E10]** contra el código real.

**Fuentes:**
- alcance y decisiones de producto: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E12 y §5;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- conocimiento de Meta: ArquitecturaBase, `origin/main` `202b65d` ([sección 14](#14-referencia-de-meta-tomada-de-arquitecturabase)). Es solo referencia, no código para copiar.

**Requisitos:** la E11 cerrada (números por organización, bandeja Internos, token por organización), la E9 (Redis, límites y fuzz) y la E10.

## 1. Alcance

**Entra:**
- **Editor:** formularios de la organización con seis tipos de pregunta, textos en español e inglés, prefijo de numeración y vista previa en el teléfono.
- **Publicación en WhatsApp:**
  - un Flow por versión, idioma y cuenta de WhatsApp Business (WABA);
  - operación durable, con sus estados visibles y la vista previa oficial de Meta.
- **Envío desde el chat:** «Mandar formulario» en un chat de la bandeja Internos, con la sesión de 24 horas.
- **Recepción:**
  - el `nfm_reply` se convierte en una respuesta numerada (`VIS-000123`), sin duplicados;
  - en el chat aparece una tarjeta con los valores, o solo con el número si quien mira no puede ver respuestas.
- **El módulo:**
  - `Forms` quitable, con su feature `forms` y sus permisos;
  - los cuatro puertos neutrales del núcleo con sus implementaciones apagadas;
  - el retiro probado una vez.

**No entra** (va en etapas posteriores o en «Después»):
- catálogos (E13);
- fotos y documentos (E14);
- planilla, detalle de una respuesta y Excel (E15);
- reglas y aprobación (E16);
- biblioteca de plantillas (E17);
- copiar o eliminar formularios, y deprecar Flows viejos;
- clientes B2C;
- límites por plan.

## 2. Decisiones de la etapa

Las de producto están en la hoja de ruta (§5) y no se vuelven a preguntar. Las técnicas que siguen las tomé al escribir esta spec. Siguen las reglas del repo y, cuando hay más de una forma de hacer algo, la más simple.

| # | Decisión | Por qué |
| --- | --- | --- |
| T1 | `Forms` y `WhatsApp` se hablan solo por cuatro puertos neutrales del núcleo (§7). | `ModuleIsolationTests` prohíbe que un módulo use tipos de otro. |
| T2 | **«Mandar formulario» es de WhatsApp.** La pantalla del chat, la ruta, la sesión y el mensaje van en el módulo WhatsApp, que lista los formularios con `IStructuredFormSource`. No hace falta el registro de acciones internas de la E11. | El que manda es el operador desde el chat, no el empleado con un comando. |
| T3 | El borrador usa `IVersioned` y 409 `General.ConcurrencyConflict`, sin recibos de cambios. | La edición es interna; ArquitecturaBase gastó días en recibos de 30 días que su ADR 0018 retiró. |
| T4 | **Hay un solo worker de publicación, el de Forms.** Sus pasos: <ul><li>encuentra el trabajo en un registro técnico global, `platform.FormPublicationWorks`, como `AssistantTurnWorks`;</li><li>entra con `ExecutionActor` y llama al canal;</li><li>el canal hace sus pasos con Meta adentro de esa llamada, cada uno con su propia transacción.</li></ul> | Un worker menos que en ArquitecturaBase. Es el patrón que el repo ya usa para el asistente. |
| T5 | Los workers entran con `IExecutionActorFactory` e `IExecutionActorScope`, no con `ITenantScope.Enter` solo. | Con `Enter` solo, `IFeatureService` da `false`, porque `Enter` deja `TenantKind` en null (backlog #51). |
| T6 | El Flow se ata a la versión, al idioma y a la WABA. | Un Flow vive en una WABA; una organización puede tener números en WABAs distintas **[E11]**. |
| T7 | **Numeración:** `LastRecordNumber` en la definición, sumado bajo bloqueo de fila al capturar, con índice único (definición, número). El prefijo es único **dentro de la organización**. | Sin bloqueos globales: cada organización numera aparte. |
| T8 | **Sin ticket de comprobación:** `publication-checks` solo informa, y `publications` vuelve a comprobar todo adentro de su transacción. | Un paso y un secreto menos. La comprobación final es la que vale. |
| T9 | **«Número con decimales»:** cada pregunta fija sus decimales (de 0 a 6) y admite hasta 15 dígitos enteros. Al responder se acepta punto o coma; más decimales que los permitidos se rechaza. | Evita que «125.500» se lea como 125,5: con 0 decimales, se rechaza. |
| T10 | **El idioma del formulario que se manda es el de la cuenta de quien lo recibe.** El operador no lo elige. | La hoja de ruta pide «con idioma». Se usa la cultura de la cuenta. |
| T11 | Los valores viven en filas tipadas (`FormRecordValues`), sin un JSON paralelo. | Una sola fuente. La planilla de la E15 filtra sobre las columnas. |
| T12 | **Las capturas rechazadas no crean nada en Forms.** El rechazo queda en la sesión de WhatsApp, con su código. | Sin tabla de recibos: la idempotencia la da el índice único `SourceKey`. |

## 3. Modelo de datos

**Convención para todas las tablas:**
- todas son `ITenantOwned` en el esquema `tenant`, con clave `(TenantId, Id)`, `EnableTenantRls` y el `GRANT` a `mt_app` en la migración;
- las relaciones usan FK compuestas;
- los textos usan `TextLimits`.

**Modelo de entidad y configuración:** `Domain/Assistant/AssistantOrganizationSettings.cs` e `Infrastructure/Modules/Assistant/Persistence/Configurations/AssistantOrganizationSettingsConfiguration.cs`. **Migración:** `Infrastructure/Persistence/Migrations/20261010011328_AssistantModule.cs`.

### Forms (`Domain/Forms`)

**FormDefinitions** (`IAuditable`, `IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | v7 |
| `NameEs`, `NameEn` | texto ≤80 | `NameEn` puede faltar en el borrador |
| `Prefix` | texto ≤12 | `^[A-Z][A-Z0-9]{0,11}$`; único por organización (`UniqueKeys.FormPrefix`) |
| `PrefixLockedAtUtc` | timestamptz? | se fija en la primera publicación aceptada |
| `Draft` | jsonb | `FormDraft` (abajo) |
| `ActiveVersionId` | uuid? | FK compuesta a `FormVersions` |
| `LastRecordNumber` | int | arranca en 0 |
| `CreatedAtUtc` y demás auditoría | — | `IAuditable` |
| `Version` | xmin | 409 |

**`FormDraft`** (jsonb, versionado con `format: 1`):
- `submitLabel{es,en}`;
- `questions[]`, cada una con `fieldId`, `type`, `label{es,en}`, `help{es,en}`, `required`, `maxLength?`, `decimals?` y `options[]`;
- cada opción lleva `{choiceId, label{es,en}}`.

**FormVersions** (inmutable)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `DefinitionId` | uuid | FK compuesta |
| `Number` | int | único por definición |
| `NameEs`, `NameEn`, `Questions`, `SubmitLabel` | texto y jsonb | copia sellada, con los dos idiomas completos |
| `ContentHash` | texto 64 | SHA-256 del JSON canónico (formato versionado y orden fijo) |
| `CreatedAtUtc`, `CreatedByUserId` | — | — |

**FormPublications** (`IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `DefinitionId`, `VersionId` | uuid | `VersionId` es único |
| `State` | texto | `Accepted`, `Preparing`, `Publishing`, `Unconfirmed`, `ActivationPending`, `Published`, `Failed` |
| `ErrorCode`, `Issues` | texto?, jsonb? | lo que devolvió el canal |
| `RequestedByUserId`, `RequestedAtUtc`, `CompletedAtUtc?` | — | — |
| — | índice único filtrado | una sola publicación abierta por definición (`UniqueKeys.FormPublicationOpen`) |

**FormRecords**

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `DefinitionId`, `VersionId` | uuid | FK compuestas |
| `Number` | int | único por definición. Se muestra como `PREFIX-000123`, armado al leer. |
| `SourceKey` | texto ≤200 | único por organización: `whatsapp:<sessionId>` |
| `Channel` | texto | `whatsapp` |
| `RespondentUserId` | uuid | quien respondió |
| `RespondentCompanyIds` | uuid[] | todas las empresas activas de quien respondió, al responder ([§13](#13-decisiones-de-producto-de-la-etapa)) |
| `Culture` | texto | es o en |
| `SubmittedAtUtc` | timestamptz | la hora firmada por Meta |
| `CreatedAtUtc` | — | — |

**FormRecordValues**

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `RecordId`, `FieldId` | uuid | PK; `FieldId` sin FK (se valida contra la versión sellada) |
| `Type` | texto | el tipo de la pregunta |
| `TextValue` | texto ≤10.000 | — |
| `NumberValue` | numeric | sin redondeo |
| `DateValue` | date | `DateOnly` |
| `BoolValue` | bool | — |
| `ChoiceId`, `ChoiceLabel` | uuid, texto | el texto que vio la persona |
| — | CHECK | exactamente un valor |

Una pregunta opcional sin respuesta no tiene fila: es «Sin respuesta», y nunca se confunde con `false` ni con `0`.

**platform.FormPublicationWorks** (registro técnico global, declarado en el contribuidor):
- columnas: `Id` (el de la publicación), `TenantId`, `ActorUserId`, `Culture`, `TimeZoneId`, `Status`, `LeaseExpiresAtUtc`, `NextCheckAtUtc` y `Attempts`;
- no guarda datos de negocio;
- modelo: `AssistantTurnWorks` y `Infrastructure/Modules/Assistant/Persistence/Repositories/AssistantTurnRepository.cs` (`ListReadyAsync`).

### WhatsApp (se suma al módulo de la E11)

**WhatsAppFlowBindings**

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `PublicationId`, `VersionId` | uuid | ids opacos de Forms, sin FK |
| `Culture` | texto | es o en |
| `WabaId` | texto | **[E11]** la cuenta del número |
| `MetaFlowId` | texto? | único |
| `Step` | texto | `Create`, `Upload`, `Validate`, `Publish`, `Verify` |
| `State` | texto | `Accepted`, `InFlight`, `Uncertain`, `Published`, `Failed` |
| `FlowJsonHash`, `FieldMap` | texto, jsonb | `field_<N>` ↔ `FieldId`/`ChoiceId` |
| `ErrorCode` y fechas | — | — |
| — | único | `(TenantId, VersionId, Culture, WabaId)` |

**WhatsAppFlowSessions**

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `ConversationId` | uuid | **[E11]** |
| `RecipientUserId`, `SentByUserId` | uuid | — |
| `VersionId`, `BindingId`, `Culture` | — | `VersionId` opaco |
| `TokenHash` | texto 64 | único; el token viaja como `flow_token` y no se guarda |
| `ExpiresAtUtc` | timestamptz | se crea + `WhatsApp:FlowSessionHours` (24 por defecto, de 1 a 168) |
| `State` | texto | `Pending`, `Completed`, `Rejected`, `Expired` |
| `RecordId`, `RecordNumber`, `ErrorCode` | — | resultado de Forms |
| `ResponseMessageId` | uuid? | único; el mensaje entrante **[E11]** |

**Lo que se suma a las tablas de la E11 [E11]:**
- **mensaje saliente:** un tipo «Formulario» que apunta a la sesión;
- **mensaje entrante:** `ProtectedFlowResponse`, cifrado con `IPayloadProtector` y de hasta 24 KiB. Se borra con la retención de WhatsApp, solo cuando ya se procesó.

## 4. Dominio y errores

**Entidades (`Domain/Forms`):**
- **`FormDefinition`:** `Create`, `SaveDraft`, `ChangePrefix` (prohibido si está bloqueado), `LockPrefix`, `Activate(versionId)` y `IssueNextNumber`.
- **`FormVersion.Seal`.**
- **`FormPublication`:** las transiciones de estado, con `Result`.
- **`FormRecord`**, con sus valores.

**Tipos de pregunta y límites:**
- **Tipos** (`FormQuestionType`): `ShortText`, `LongText`, `Number`, `Date`, `YesNo` y `Choice`.
- **Límites de Forms:** nombre ≤80, prefijo ≤12, hasta 40 preguntas y hasta 50 opciones por lista.
- **Límites de WhatsApp:** los fija el perfil del canal, con `CheckAsync` (§14).

**Reglas:**
- **Identidades:**
  - el servidor emite `FieldId` y `ChoiceId` (UUID v7);
  - cambiar el tipo de una pregunta crea una identidad nueva;
  - una identidad quitada no vuelve.
- **Guardar y publicar:**
  - el borrador se puede guardar incompleto;
  - para publicar, nombre, preguntas, ayudas, opciones y botón tienen que estar completos en los dos idiomas;
  - publicar sella una versión nueva solo si el borrador cambió respecto de la activa. Si no cambió, devuelve `Forms.Publication.NothingToPublish`.
- **Prefijo:** se propone desde el nombre (letras sin acentos, en mayúsculas, hasta 3, y un número si ya está tomado). Se edita hasta la primera publicación aceptada, y nunca se recicla.
- **Respuestas:**
  - cada respuesta conserva la versión con la que llegó;
  - la versión que se manda es la activa al momento de mandar.

**Errores** (en `Domain/Forms/<Entidad>Errors.cs`, con textos en `Application/Modules/Forms/Resources/FormsTexts.resx` y `.en.resx`). El modelo es `Domain/Assistant/AssistantErrors.cs` y su `AssistantErrorTextSource`:

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Definition.NotFound` | NotFound | ajena o inexistente |
| `Forms.Definition.PrefixTaken` | Conflict | el prefijo ya existe en la organización |
| `Forms.Definition.PrefixLocked` | Conflict | se quiso cambiar después de publicar |
| `Forms.Definition.PrefixInvalid` | Validation (campo `prefix`) | no cumple el formato |
| `Forms.Draft.QuestionInvalid` | Validation (campo `questions[i]`) | tipo, opciones o decimales fuera de regla |
| `Forms.Draft.TooManyQuestions` / `TooManyOptions` | Validation | más de 40 preguntas o más de 50 opciones |
| `Forms.Publication.MissingTranslation` | Validation (campo por pregunta) | falta un idioma |
| `Forms.Publication.ChannelIssues` | Validation | el canal marcó problemas: lista de `{fieldId, choiceId?, path, code, limit?, culture}` |
| `Forms.Publication.InProgress` | Conflict | ya hay una publicación abierta |
| `Forms.Publication.NothingToPublish` | Conflict | el borrador es igual a la versión activa |
| `Forms.Publication.ChannelUnavailable` | ServiceUnavailable | sin WhatsApp, o sin número conectado [E11] |
| `Forms.Publication.NotFound` | NotFound | — |
| `Forms.Preview.NotAvailable` | NotFound | la versión no tiene Flow publicado en ese idioma |
| `Forms.Record.InvalidResponse` | Validation | valor desconocido, falta uno obligatorio o se pasa del límite |
| `Forms.Record.ReplayConflict` | Conflict | la misma sesión llegó con otro contenido |
| `Forms.Record.NotAllowed` | Forbidden | quien responde ya no tiene `formrecords.create` o dejó de ser miembro |
| `WhatsApp.Flow.TextTooLong` / `TooManyComponents` / `ResponseTooLarge` | Validation | límites del perfil (§14) |
| `WhatsApp.Flow.ProviderRejected` / `ValidationFailed` | Failure | rechazo definitivo de Meta |
| `WhatsApp.Flow.AccountUnavailable` | ServiceUnavailable | la WABA sin medio de pago o sin salud [E11] |
| `WhatsApp.Flow.NotPublishedForNumber` | Conflict | el número del chat está en una WABA sin Flow de esa versión |
| `WhatsApp.Flow.SessionExpired` | Conflict | la respuesta llegó pasada la sesión (hora de Meta) |
| `WhatsApp.Chat.WindowClosed` / `NotEmployee` / `FormUnavailable` / `SendPending` | Conflict | reglas del botón (§6.6) |

Los códigos siguen `^[A-Z][A-Za-z]+(\.[A-Z][A-Za-z0-9]+){2,}$` (`ErrorCodeTests`). El 409 de edición es el común, `General.ConcurrencyConflict`.

## 5. Contrato borrador

Todas las rutas Business llevan `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Las escrituras que crean o envían llevan `[Idempotent]`. Los listados, `PagedRequest` (10 por defecto) y `filter-counts`. El modelo es `Api/Modules/Assistant/Controllers/BusinessAssistantController.cs`.

| Método y ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| GET `/api/organization/forms` | `forms.read` | `page`, `pageSize`, `sort` (`name`, `updatedAtUtc`), `search`, `status` (`all`, `published`, `drafts`) | 200 `PagedResult<FormSummary>`: `{id, name, prefix, status, activeVersionNumber?, publishedAtUtc?, publicationState?}` |
| GET `/api/organization/forms/filter-counts` | `forms.read` | `search` | 200 `{all, published, drafts}` |
| POST `/api/organization/forms` | `forms.manage` | `{name}` (en español) | 201 `FormDetail` + Location, con el prefijo propuesto |
| GET `/api/organization/forms/{id}` | `forms.read` | — | 200 `FormDetail`: `{id, name{es,en}, prefix, prefixLocked, submitLabel, questions[], activeVersion?{id, number, publishedAtUtc}, publication?{id, state, errorCode?, issues?}, canManage, canPublish, version}` |
| PUT `/api/organization/forms/{id}/draft` | `forms.manage` | `{name{es,en}, prefix, submitLabel{es,en}, questions[], version}` | 200 `{version, questions[]}`, con las identidades emitidas; 409 |
| POST `/api/organization/forms/{id}/publication-checks` | `forms.publish` | `{version}` | 200 `{canPublish, nextVersionNumber, issues[], changes[]}`; no escribe |
| POST `/api/organization/forms/{id}/publications` | `forms.publish` | `{version}` | 202 `{publicationId, state}` + Location; no llama a Meta |
| GET `/api/organization/forms/{id}/publications/{publicationId}` | `forms.read` | — | 200 `{state, errorCode?, issues?, versionNumber}` |
| GET `/api/organization/forms/{id}/versions/{versionId}/preview` | `forms.read` | `culture` (es o en) | 200 `{url, expiresAtUtc}`, con `Cache-Control: no-store` |
| GET `/api/organization/whatsapp/conversations/{id}/forms` **[E11]** | «Atender internos» | `page`, `search` | 200 `{available, reason?, culture, forms: PagedResult<{definitionId, versionId, name, prefix}>}` |
| POST `/api/organization/whatsapp/conversations/{id}/forms` **[E11]** | «Atender internos» | `{definitionId, versionId, text}` (≤1024) | 202 `{sessionId, messageId}` |

**Otras piezas del contrato:**
- **Tarjeta de respuesta [E11]:** los mensajes de la conversación suman un tipo `form`: `{sessionId, state, formName, displayNumber?, values?: [{fieldId, label, type, value}], canReadValues}`.
- **Webhook:** la misma ruta de la E8. Se agrega leer `interactive.nfm_reply`.
- **Plataforma:** no tiene rutas nuevas. La feature `forms` aparece sola en la pestaña «Módulos» de la ficha de la organización.

**Contrato y errores en OpenAPI:** `Api/Modules/Forms/FormsContractContributor.cs`, como `Api/Modules/Assistant/AssistantContractContributor.cs`.

## 6. Flujos, con sus transacciones

«UoW» es una `ExecuteInTransactionAsync(…, CommitPolicy.OnSuccess, ct)`. Cada método público va en `OperationLog.RunAsync`.

1. **Crear.**
   - Afuera de la UoW se valida el nombre.
   - En la UoW: se propone el prefijo libre, se crea la definición con el borrador vacío y se responde 201.
2. **Guardar el borrador.**
   - Afuera: se validan el pedido y las reglas de las preguntas.
   - En la UoW:
     - se carga la definición con la versión esperada;
     - si el prefijo cambió, se comprueba que no esté bloqueado y que esté libre (el choque del índice se traduce a `PrefixTaken`);
     - se emiten las identidades nuevas y se guarda.
   - Responde 200 `{version, questions}`.
3. **Comprobar.** Es solo lectura:
   - reglas de Forms y los dos idiomas;
   - `IStructuredFormChannel.CheckAsync`, que es puro: sin escrituras, sin bloqueos y sin Meta;
   - la diferencia con la versión activa.
4. **Publicar.**
   - **En la UoW del pedido:**
     - bloqueo de la definición;
     - la versión esperada;
     - se comprueba otra vez todo lo del punto 3;
     - que no haya otra publicación abierta;
     - se sella la `FormVersion` (número = última + 1);
     - se crean `FormPublication` (`Accepted`) y `FormPublicationWorks`;
     - se bloquea el prefijo.
   - Responde 202.
   - **El worker** (`Infrastructure/Modules/Forms/FormPublicationWorker.cs`, modelo `AssistantTurnWorker`) hace tres pasos:
     1. busca trabajos listos y entra con `ExecutionActor` (el operador que publicó, revalidado: si perdió `forms.publish` antes de tocar Meta, la publicación termina `Failed`). Reserva con una UoW y pasa a `Preparing`;
     2. fuera de toda UoW llama a `IStructuredFormChannel.PublishAsync`:
        - el canal arma el Flow de cada idioma para cada WABA con números conectados **[E11]**;
        - por cada uno sigue los pasos Create → Upload → Validate → Publish → Verify;
        - antes de cada llamada a Meta guarda `InFlight` (en su UoW), y después guarda el resultado (en otra UoW);
     3. con lo que devolvió el canal, en una UoW:
        - `Published` → `ActivationPending`, y se activa la versión en la misma UoW → `Published`;
        - `Uncertain` → `Unconfirmed`, con `NextCheckAtUtc`;
        - rechazo definitivo → `Failed`, con sus `issues`.
   - **Cuando quedó `Unconfirmed`,** el worker vuelve a consultar con `GetPublicationAsync`, que solo verifica y nunca repite un POST.
   - **Si se cae a mitad:** la reserva vuelve a tomar el trabajo, y el canal, al ver un `InFlight`, verifica en vez de repetir.
5. **Vista previa:** `IStructuredFormChannel.GetPreviewAsync(versionId, culture)` devuelve la URL de Meta con su vencimiento, sin guardar nada.
6. **Mandar desde el chat** (módulo WhatsApp).
   - **Cuándo aparece el botón:**
     - el chat es de la bandeja Internos y quien escribe es miembro activo **[E11]**;
     - el operador tiene «Atender internos»;
     - la ventana de 24 horas está abierta;
     - el número está disponible;
     - no hay otro envío pendiente;
     - `IStructuredFormSource.ListSendableAsync(recipientUserId, culture)` devuelve al menos un formulario. Forms filtra los publicados que quien recibe puede responder (`formrecords.create`).
   - **Si el botón no se puede usar:** se ve apagado, con el motivo en un ⓘ.
   - **Mandar:**
     - afuera de la UoW se valida el texto (≤1024) y se lee el formulario elegido con `FindSendableAsync`;
     - en la UoW: bloqueo de la conversación, se revalidan ventana, empleado y envío pendiente, se toma el chat si no estaba tomado (regla de la E11), se crea la sesión (token aleatorio de 32 bytes; se guarda solo el hash) y se encola el mensaje interactivo de tipo Flow por el outbox (`WhatsAppOutboundMessage` con su variante nueva).
   - **Si la WABA del número no tiene Flow de esa versión:** `WhatsApp.Flow.NotPublishedForNumber`.
7. **Recibir y capturar** (worker de WhatsApp, siguiendo la entrada de la E11 **[E11]**).
   - **Webhook:** guarda el mensaje con `ProtectedFlowResponse`, sin duplicados por `wamid`.
   - **El worker,** por cada mensaje, aislando los errores mensaje por mensaje:
     1. saca el hash del `flow_token` y busca la sesión;
     2. compara la hora firmada de Meta con `ExpiresAtUtc`: si se pasó, `Expired`;
     3. decodifica con el `FieldMap` del binding (§14);
     4. entra con `ExecutionActor` (quien respondió) y, fuera de su UoW, llama a `IStructuredRecordSink.CaptureAsync`.
   - **La captura en Forms,** en una sola UoW:
     - busca el `SourceKey`: si ya existe con el mismo contenido, devuelve el mismo número; si el contenido es otro, `ReplayConflict`;
     - revalida la membresía y `formrecords.create`;
     - valida estricto contra la versión sellada;
     - bloquea la definición y suma `LastRecordNumber`;
     - guarda la respuesta y sus valores.
   - **Después, en otra UoW de WhatsApp:** se marca la sesión `Completed` (con número) o `Rejected` (con código).
   - **Si se cae entre las dos UoW:** se repite y devuelve el mismo número.
8. **Tarjeta en el chat.**
   - Al listar los mensajes, WhatsApp llama a `IStructuredRecordSource.ReadAsync(recordIds, culture)`.
   - Forms decide con los permisos de quien mira: con `formrecords.read` devuelve los valores, y sin él solo el número.
   - En la lista de chats, la vista previa es el texto fijo «Respuesta del formulario».
9. **Retención y baja.**
   - La retención de WhatsApp borra `ProtectedFlowResponse` cuando el mensaje ya se procesó; la respuesta y su número quedan.
   - `FormsAccountDeletionParticipant` corre dentro de la UoW de la baja; qué se conserva lo decide el plan de la etapa ([datos-personales](../../rules/datos-personales.md)). El modelo es `Infrastructure/Modules/Assistant/AssistantAccountDeletionParticipant.cs`.

## 7. Puertos neutrales del núcleo

**Dónde van:**
- las interfaces, en `Application/Interfaces/Integrations/StructuredData/`;
- los modelos, en `Application/Models/StructuredData/`;
- las implementaciones apagadas, en `Infrastructure/StructuredData/`. Se registran con `TryAdd`, y cada módulo las reemplaza al componerse.

Los nombres no mencionan formularios ni WhatsApp.

```csharp
// WhatsApp → Forms (implementa Forms)
public interface IStructuredFormSource
{
    Task<PagedResult<StructuredFormChoice>> ListSendableAsync(Guid recipientUserId, string culture, PagedRequest page, CancellationToken ct);
    Task<StructuredFormVersion?> FindSendableAsync(Guid recipientUserId, Guid definitionId, Guid versionId, string culture, CancellationToken ct);
}
// Forms → WhatsApp (implementa WhatsApp)
public interface IStructuredFormChannel
{
    bool IsAvailable { get; }
    StructuredFormCheck Check(StructuredFormVersion version);                           // puro
    Task<StructuredPublicationResult> PublishAsync(StructuredPublicationRequest request, CancellationToken ct);
    Task<StructuredPublicationResult> GetPublicationAsync(Guid publicationId, CancellationToken ct);
    Task<StructuredPreview?> GetPreviewAsync(Guid versionId, string culture, CancellationToken ct);
}
// WhatsApp → Forms (implementa Forms)
public interface IStructuredRecordSink
{
    Task<Result<StructuredRecordReceipt>> CaptureAsync(StructuredRecordCapture capture, CancellationToken ct);
}
public interface IStructuredRecordSource
{
    Task<IReadOnlyList<StructuredRecordCard>> ReadAsync(IReadOnlyCollection<Guid> recordIds, string culture, CancellationToken ct);
}
```

**Modelos:**
- `StructuredFormVersion(DefinitionId, VersionId, Number, Prefix, Name{es,en}, SubmitLabel{es,en}, Questions[FieldId, Type, Label{es,en}, Help{es,en}, Required, MaxLength?, Decimals?, Options[ChoiceId, Label{es,en}]], ContentHash)`;
- `StructuredRecordCapture(SourceKey, DefinitionId, VersionId, RespondentUserId, Culture, SubmittedAtUtc, Values[FieldId, Text?|Number?|Date?|Bool?|ChoiceId?])`;
- `StructuredRecordReceipt(RecordId, Number, DisplayNumber)`;
- `StructuredRecordCard(RecordId, DisplayNumber, FormName, CanReadValues, Values?)`;
- `StructuredPublicationResult(State: Published|Pending|Uncertain|Failed, Issues[], NextCheckAtUtc?)`.

**Implementaciones apagadas:**
- **Sin WhatsApp:** `IsAvailable = false`. Se puede diseñar, y publicar responde `ChannelUnavailable`.
- **Sin Forms:** la fuente devuelve una lista vacía, el botón no aparece y la captura devuelve un error que deja la sesión `Rejected`.

## 8. Archivos

Cada pieza nueva copia la **estructura** del archivo modelo de este repo; no se copia código de ArquitecturaBase. Hay dos sufijos de ArquitecturaBase que no están en la lista cerrada y cambian:
- `Compiler` pasa a `WhatsAppFlowBuilder` («arma un modelo»);
- `Codec` pasa a `WhatsAppFlowReplyVerifier` («verifica algo que entra»).

**Abreviaturas:** `D/`, `A/`, `I/`, `P/` son `src/ArquitecturaBaseMultitenant.{Domain,Application,Infrastructure,Api}/`.

| Archivo nuevo | Modelo en este repo |
| --- | --- |
| `D/Forms/FormDefinition.cs`, `FormVersion.cs`, `FormPublication.cs`, `FormRecord.cs`, `FormQuestionType.cs`, `FormDraft.cs`, `*Errors.cs` | `D/Assistant/AssistantOrganizationSettings.cs`, `D/Assistant/AssistantErrors.cs` |
| `D/Authorization/Permissions.cs`: clase `Forms` (`forms.read`, `forms.manage`, `forms.publish`, `formrecords.create`, `formrecords.read`) en `OrganizationScoped` | la clase `Assistant` del mismo archivo |
| `A/Modules/Forms/FormsModule.cs` | `A/Modules/Assistant/AssistantModule.cs` |
| `A/Modules/Forms/Services/FormsFeatureDefinitionContributor.cs` (`new("forms", FeatureScope.Business, false)`) | `AssistantFeatureDefinitionContributor.cs` |
| `A/Modules/Forms/Interfaces/Services/IFormDefinitionService.cs`, `IFormPublicationService.cs` | `IAssistantConversationService.cs` |
| `A/Modules/Forms/Services/FormDefinitionService.cs`, `FormPublicationService.cs` y los helpers `FormPrefixResolver`, `FormDraftEditor`, `FormVersionCreator`, `FormRecordNumberIssuer`, `FormRecordWriter` | `AssistantConversationService.cs` y sus helpers |
| `A/Modules/Forms/Services/FormStructuredSource.cs`, `FormRecordSink.cs`, `FormRecordSource.cs` (implementan los puertos) | `A/Modules/WhatsApp/Services/WhatsAppLoginCodeChannel.cs` (un módulo implementa un puerto del núcleo) |
| `A/Modules/Forms/Interfaces/Persistence/IFormDefinitionRepository.cs`, `IFormReader.cs`, `UniqueKeys.cs` | `A/Modules/Assistant/Interfaces/Persistence/` |
| `A/Modules/Forms/Models/`, `Validation/`, `Resources/FormsTexts{,.en}.resx`, `FormsErrorTextSource.cs` | las mismas carpetas del Asistente |
| `A/Interfaces/Integrations/StructuredData/*.cs`, `A/Models/StructuredData/*.cs` | `A/Interfaces/Integrations/Messaging/ILoginCodeChannel.cs` |
| `I/StructuredData/Disabled*.cs` | `I/Messaging/Email/EmailLoginCodeChannel.cs` (implementación del núcleo) |
| `I/Modules/Forms/FormsModule.cs`, `Persistence/FormsModelContributor.cs`, `Persistence/Configurations/*.cs`, `Persistence/Repositories/*.cs`, `Persistence/Readers/*.cs` | `I/Modules/Assistant/...` (`AssistantModelContributor.cs`) |
| `I/Modules/Forms/FormPublicationWorker.cs`, `FormsAccountDeletionParticipant.cs` | `I/Modules/Assistant/AssistantTurnWorker.cs`, `AssistantAccountDeletionParticipant.cs` |
| `I/Persistence/Migrations/<fecha>_FormsModule.cs` y `<fecha>_SystemRoleFormsPermissions.cs` | `20261010011328_AssistantModule.cs`, `20261010203618_SystemRoleAssistantPermissions.cs` |
| `P/Modules/Forms/FormsApiModule.cs`, `FormsContractContributor.cs`, `Controllers/BusinessFormsController.cs`, `Contracts/*HttpRequest.cs` | `P/Modules/Assistant/...` |
| WhatsApp: `A/Modules/WhatsApp/Services/WhatsAppFlowBuilder.cs`, `WhatsAppFlowReplyVerifier.cs`, `WhatsAppFormSendService.cs`, `WhatsAppStructuredFormChannel.cs`; `I/Modules/WhatsApp/WhatsAppFlowClient.cs`, el worker de respuestas y las configuraciones de `WhatsAppFlowBindings` y `WhatsAppFlowSessions` | `I/Modules/WhatsApp/WhatsAppCloudClient.cs`, `WhatsAppInboundProcessor.cs` y lo que deje la E11 **[E11]** |
| `Program.cs`: `AddFormsModule` en las tres capas | la línea del Asistente |

**Front** (`F/` = `ArquitecturaBaseMutitenantFront/`):

| Archivo nuevo | Modelo |
| --- | --- |
| `F/src/modules/forms/registration.ts`: rutas `/org/formularios` y `/org/formularios/:id`; guarda con `features.includes("forms")` y `forms.read`; `navigation` en el teléfono y en la administración | `F/src/modules/assistant/registration.ts` |
| `F/src/modules/forms/{api,components,hooks,pages}/`, `errors.ts` | `F/src/modules/assistant/` |
| `F/src/locales/{es,en}/forms.json` | `assistant.json` |
| `F/src/app/moduleComposition.ts`: suma `formsRegistration` | la línea del Asistente |
| WhatsApp **[E11]**: el diálogo «Mandar formulario» y la tarjeta de respuesta, en el módulo de WhatsApp del front que deje la E11 | — |

**Punteros:** cada carpeta nueva lleva su `AGENTS.md` de una a tres líneas («qué va acá · Leé ficha · Copiá de archivo») y un `CLAUDE.md` con `@AGENTS.md`, más su fila en `docs/architecture/arbol.md`. **Se crean con la carpeta, en la E12, no antes** (`HarnessTests`). Al cerrar la etapa nacen `docs/features/formularios.md` y la fila de Forms en «Dónde va cada cosa» de `AGENTS.md`.

## 9. Pantallas (tableros para el chat de Diseño)

Se dibujan en el lienzo vigente, de a una, con opciones y tu aprobación. ArquitecturaBase sirve para el contenido (sus tableros A–E, P1–P3, C1–C5 y R1–R3 en `ArquitecturaBaseFront/docs/design/formularios-whatsapp.md`), no para el estilo. Rigen los criterios del lienzo:
- fichas con pestañas;
- lo corto, en diálogo;
- estados con color y palabra corta, y el detalle en ⓘ;
- el teléfono es una app.

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `Formularios` y `M-Formularios` | listado: nombre, prefijo, estado y última publicación; ⋮; «Nuevo formulario» | vacío, sin resultados, cargando, error; filtro Todos, Publicados y Borradores |
| `Formularios-Nuevo` | diálogo con el nombre | prefijo propuesto |
| `Formulario` y `M-Formulario` | ficha con pestañas: **Preguntas** (lista, «Agregar pregunta», subir y bajar, vista previa del teléfono al costado), **Inglés** (los textos en inglés al lado del original, con lo que falta marcado) y **Publicación** (versión activa, estado y vista previa oficial) | guardado, guardando, no se guardó, conflicto (409) |
| `Formulario-Pregunta` | diálogo: tipo, texto y ayuda es/en, obligatoria, largo máximo, decimales u opciones | cada uno de los seis tipos |
| `Formulario-Prefijo` | el prefijo en la ficha | propuesto, editado, tomado, bloqueado |
| `Formulario-Publicar` | diálogo de comprobación: problemas por pregunta, versión nueva y cambios | sin problemas, con problemas, borrador sin guardar, otra publicación en curso, WhatsApp no disponible |
| `Formulario-Publicacion` | el estado en la pestaña Publicación | Publicando, Sin confirmar, Publicada vN, No se pudo publicar (con el motivo por pregunta) |
| `WA-Chats-Formulario` y `M-WA-Chats-Formulario` | en un chat de Internos, «Mandar formulario»: elegir, texto (≤1024) y vista del mensaje | botón apagado con su motivo (ventana cerrada, no es empleado, sin formularios, envío pendiente), mandando, encolado, falló |
| `WA-Chats-Respuesta` y `M-WA-Conversacion` | la tarjeta de respuesta en el chat | esperando, recibida con número y valores, recibida solo con número (sin permiso), rechazada, vencida; los seis tipos sin perder «No», «0», «Sin respuesta» ni los decimales |
| `Rol` | el área «Formularios» con sus cinco permisos | ya existe: solo suma filas |

## 10. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | identidades; tipo cambiado = identidad nueva; prefijo (formato, bloqueo); transiciones de `FormPublication`; numeración |
| `tests/…Application.UnitTests/Modules/Forms/` | validadores; reglas de publicar (idiomas, «nada para publicar»); armado del Flow y verificador de respuestas (cada tipo, punto y coma, decimales de más, «125.500» con 0 decimales, obligatoria vacía, opción ajena, presupuesto de 16 KiB) |
| `tests/…Api.IntegrationTests/Modules/Forms/` | cada ruta: 401, 403, feature apagada = 404, validación, 409, idempotencia y paginado; publicar con un canal de prueba (Published, Uncertain → verifica, Failed); captura idempotente y número sin saltos con dos capturas a la vez; tarjeta con y sin `formrecords.read`; baja de cuenta |
| `tests/…Api.IntegrationTests/Modules/Forms/*IsolationTests.cs` | otra organización recibe 404 en cada ruta; RLS de cada tabla; la captura no cruza organizaciones (modelo: `Modules/Assistant/AssistantConversationIsolationTests.cs`) |
| `tests/…Api.IntegrationTests/Modules/WhatsApp/` **[E11]** | botón y motivos; sesión vencida por la hora de Meta; un mensaje con error no frena a los demás; respuesta de más de 16 KiB rechazada y visible |
| Guardas (partials del módulo) | `ExplicitRouteInventoryTests`, `ModuleControllersTests`, `ApplicationServicesTests`, `TenantScopeUsageTests`, `ResourceParityTests` (sumar los resx de Forms), `PermissionTextsTests` y `ModuleIsolationTests` en verde |
| Front | Vitest con MSW y axe en las páginas y el diálogo; `registration`, `navigation` y `composition` del módulo; paridad de `forms.json` |
| E2E real y recorrido | crear, completar el inglés, publicar contra el canal real o el de prueba, mandar desde un chat de Internos, responder y ver el número tras recargar |
| Prueba con Meta | con el número de una organización de prueba: publicar, mandar, completar en el teléfono y ver el número |
| Retiro | una vez, con la base vacía: quitar `Forms` (guía nueva, `docs/guides/quitar-formularios.md`, copiando `quitar-asistente.md`) y que el núcleo y WhatsApp sigan en verde |

## 11. Premisas aplicadas

Rige la tabla de la [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#premisas-del-repo-aplicadas-a-formularios). Lo propio de la E12:

- **Servicios:** cada servicio tiene hasta 8 dependencias de negocio, y solo los servicios usan `IUnitOfWork`. Los helpers no abren transacciones.
- **Lo externo, fuera de la transacción:** nada de Meta adentro de una UoW. El estado `InFlight` se commitea antes de cada llamada.
- **Logs:**
  - `[LoggerMessage]`, con códigos y ids;
  - nunca el `flow_token`, los valores de una respuesta, el texto del mensaje ni teléfonos completos.
- **Textos:**
  - lo que ve una persona en WhatsApp (el botón «Abrir formulario», la ayuda «Usá punto para los decimales») sale de resources es/en del módulo WhatsApp;
  - los textos del formulario son de la organización.
- **Fechas:** `SubmittedAtUtc` y `ExpiresAtUtc` con `TimeProvider`, comparados con la hora firmada de Meta.
- **Sin datos de ejemplo:** no hay formularios sembrados.

## 12. Puntos de contacto con otras etapas

Al planificar la E12 se verifican contra el código real.

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E11] | El modelo de números: `WhatsAppChannels`, la WABA de cada número y el token por organización. | Un Flow por versión, idioma y WABA. Se publica en todas las WABAs con números conectados. |
| [E11] | Conversaciones y mensajes: ids, tipos de mensaje y el contrato de la lista de mensajes. | Se suman un mensaje saliente «Formulario» y la tarjeta `form`. |
| [E11] | Reconocer al empleado y la bandeja Internos; «tomar» un chat. | Mandar cuenta como responder: toma el chat. |
| [E11] | La entrada de mensajes por organización. Hoy hay un solo `IWhatsAppInboundConsumer` y no se lee `nfm_reply`. | El worker de respuestas de Flow sigue el patrón de entrada de la E11. |
| [E11] | El outbox con el número de la organización. | `WhatsAppOutboundMessage` suma la variante Flow con el `PhoneNumberId` de la organización. |
| [E11] | Las rutas de WhatsApp (`/api/organization/whatsapp/...`) y el módulo de WhatsApp del front. | Los nombres de esta spec son provisorios. |
| [E9] | Los límites por organización y por usuario. | Las rutas de Forms usan la política de la E9; no se definen límites propios. |
| [E9] | Redis. | La E12 no cachea nada nuevo. |
| [E10] | La nube de `IFileStore`. | La E12 no guarda archivos. |
| #51 | `Enter` borra `TenantKind`. | Se usa `ExecutionActor` (T5). |

## 13. Decisiones de producto de la etapa

Las tomó el usuario el 2026-10-12. No se vuelven a preguntar.

1. **La respuesta guarda todas las empresas activas de quien respondió,** tomadas al responder. El filtro por empresa de la planilla (E15) la encuentra por cualquiera de ellas.
2. **Puede mandar un formulario cualquiera con «Atender internos»,** sin un permiso de formularios aparte. Lo que limita es que quien recibe tenga `formrecords.create`.

## 14. Referencia de Meta (tomada de ArquitecturaBase)

Es lo único que se toma de ArquitecturaBase: lo que dice Meta y lo que ya se pagó. Se lee en `origin/main` (`202b65d`); los archivos están en `src/ArquitecturaBase.*/Modules/WhatsApp/`. **Antes de programar, se verifica contra la documentación vigente de Meta.**

| Tema | Lo que se sabe |
| --- | --- |
| Perfil | Flow JSON `7.3`, Graph `v25.0` (configuración del módulo), sin endpoint ni `data_api_version`. Una pantalla `FORM` terminal: `SingleColumnLayout` > `Form` > controles + `Footer` con `on-click-action: complete` y un payload `field_<GuidN>: ${form.field_<GuidN>}`. |
| Componentes | Texto corto: `TextInput` con `max-chars` (80 por defecto). Texto largo: `TextArea` con `max-length` (600). Número: `TextInput` de texto con el patrón `^-?(0|[1-9][0-9]*)([.,][0-9]+)?$`, de hasta 60 caracteres. Fecha: `CalendarPicker` single. Sí/no: `RadioButtonsGroup` con ids `true` y `false`. Lista: `Dropdown` con el `ChoiceId` como id. `max-chars` y `max-length` van como **números** JSON (Meta rechazó la versión que los mandaba como texto). |
| Límites | Etiqueta: 20 caracteres (30 en sí/no, 40 en fecha). Ayuda: 80 (300 en la descripción de sí/no). Opción: 30. Título: 80. Botón: 35. Hasta 40 preguntas, 50 opciones y 50 componentes; JSON ≤1 MiB. Se cuentan en UTF-16. Sin recortar ni usar el otro idioma: si no entra, se informa con su `FieldId` y el texto. |
| Presupuesto de respuesta | 16 KiB. El peor caso es 785 + la suma de (42 + los bytes del valor): texto 2 + 6 × largo, número 62, fecha 12, sí/no 7, opción 38. Si se pasa, no se publica (`ResponseTooLarge` sobre los largos de texto). |
| Publicar | (1) Comprobar que el número sea de la WABA: `GET {waba}/phone_numbers`. (2) `POST {waba}/flows` con `{name, categories:["OTHER"]}`. (3) `POST {flow}/assets` multipart con `asset_type=FLOW_JSON`; los `validation_errors[].pointers[].path` se mapean a la pregunta y se descarta el texto de Meta. (4) `POST {flow}/publish`. (5) Verificar: `GET` del Flow y de la salud del número, bajar el asset (solo de `*.fbcdn.net` o `mmg.whatsapp.net`, sin redirecciones, ≤1 MiB) y comparar el SHA-256. Nombre técnico del Flow: `form_<publicationId>_<culture>`. |
| Vista previa | `GET {flow}?fields=preview.invalidate(false)`. La URL tiene que ser de `business.facebook.com` y trae `expires_at`. |
| Errores | **Incierto** (no se repite solo, se verifica): 5xx, 429, timeout, cuerpo ilegible, Create 2xx sin id. **Definitivo:** otros 4xx y 2xx con `validation_errors`. Lo que falla antes de llamar a Meta termina en fallida, no en «Sin confirmar». **Sin medio de pago** (141006): el Flow aparece disponible pero la salud total queda BLOCKED. En ArquitecturaBase la operación quedaba incierta: acá se muestra como `WhatsApp.Flow.AccountUnavailable`. |
| Mensaje | `type: interactive`, `interactive.type: flow`, cuerpo ≤1024, `action.name: flow`, `parameters: {flow_message_version: "3", flow_id, flow_token (≤128), flow_cta (≤20, «Abrir formulario»), flow_action: navigate, flow_action_payload: {screen: "FORM"}}`. |
| Respuesta | `messages[].interactive.type == "nfm_reply"`, `nfm_reply.name == "flow"`, `response_json` (texto) con `flow_token` y `field_<GuidN>`. Profundidad ≤4, ≤50 campos, solo escalares. Una respuesta de más de 16 KiB se guarda como rechazada, visible, y no se descarta en silencio. |
| Valores | Texto: hasta el largo y sin NUL. Número: punto o coma, sin exponente ni separador de miles, sin redondear; se recortan los ceros a la derecha. Fecha: `yyyy-MM-dd`. Sí/no: solo `true` o `false`. Opción: un `ChoiceId` de la versión sellada. Vacío: si es obligatoria, error; si no, ausente. |
| Lecciones | <ol><li>commitear `InFlight` antes de cada POST, y ante una duda verificar, no repetir;</li><li>un Create sin id confirmado nunca se adopta por nombre;</li><li>usar la hora firmada de Meta para vencer, y aislar los errores mensaje por mensaje;</li><li>el token no forma parte de la identidad de la cuenta (WABA + número): rotarlo no rompe nada;</li><li>no hashear arreglos sin orden fijo;</li><li>en el teléfono quedó sin probar: ausente contra `false`, el largo máximo de texto y la sesión de una versión anterior. Entran en la prueba con Meta.</li></ol> |
| No se toma | El número y la WABA globales; los permisos planos (`formtemplates.*`); el contacto atado 1:1 a un usuario con `LinkRevision`; el bloqueo global `forms:definitions` y el prefijo global; los modos Manual, Bot y Asistente; la pausa de 6 s por destinatario en Redis; los recibos de borrador de 30 días; los formatos de manifiesto 1 y 2 y las revisiones r1 a r4 del perfil. |
