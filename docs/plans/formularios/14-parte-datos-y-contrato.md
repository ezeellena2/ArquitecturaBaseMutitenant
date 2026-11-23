# E14 · Formularios: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E14](14-formularios.md), que dice qué leer y cómo se cierra.

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
| `Name` | texto ≤80 | obligatorio para publicar |
| `Prefix` | texto ≤12 | `^[A-Z][A-Z0-9]{0,11}$`; único por organización (`UniqueKeys.FormPrefix`) |
| `Audience` | texto | `Employees`, `Customers` o `Both` (50); va en el borrador y se sella con la versión |
| `Draft` | jsonb | `FormDraft` (abajo) |
| `CreatedAtUtc` y demás auditoría | — | `IAuditable` |
| `Version` | xmin | 409; cambia solo al editar el borrador (T7) |

**FormDefinitionStates** (una fila por formulario, sin auditoría ni `IVersioned`; T7)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `DefinitionId` | uuid | clave; FK compuesta a `FormDefinitions` |
| `ActiveVersionId` | uuid? | FK compuesta a `FormVersions` |
| `PrefixLockedAtUtc` | timestamptz? | se fija en la primera publicación aceptada |
| `LastRecordNumber` | int | arranca en 0 |

**`FormDraft`** (jsonb, `format: 1`). Cada texto es uno solo:
- `audience`: `Employees`, `Customers` o `Both` (50);
- `submitLabel`;
- `questions[]`, cada una con `fieldId`, `type`, `label`, `help`, `required`, `maxLength?`, `decimals?`, `minSelections?`, `maxSelections?`, `catalogId?` y `presentation?` (en `Choice` y `MultiChoice`, T17 y T22) y `options[]`;
- cada opción lleva `{choiceId, label}`. Una pregunta con `catalogId` no tiene opciones propias;
- `titles[]`, cada uno con `titleId`, `text` y la pregunta antes de la que va (T24). La forma exacta se fija en el plan.

**FormVersions** (inmutable)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `DefinitionId` | uuid | FK compuesta |
| `Number` | int | único por definición |
| `Name`, `Audience`, `Questions`, `Titles`, `SubmitLabel` | texto y jsonb | copia sellada |
| `ContentHash` | texto 64 | SHA-256 del JSON canónico (formato versionado y orden fijo) |
| `CreatedAtUtc`, `CreatedByUserId` | — | — |

**FormPublications** (`IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `DefinitionId`, `VersionId` | uuid | `VersionId` es único |
| `State` | texto | `Accepted`, `Preparing`, `Publishing`, `Unconfirmed`, `NeedsReview`, `ActivationPending`, `Published`, `Failed` |
| `ErrorCode`, `Issues` | texto?, jsonb? | lo que devolvió el canal |
| `FlowName` | texto? ≤80 | el nombre del Flow que fijó el canal (165), para buscarlo en Meta con «Necesita revisión» |
| `RequestedByUserId`, `RequestedAtUtc`, `CompletedAtUtc?` | — | — |
| `ResolvedByUserId?`, `ResolvedAtUtc?` | — | quién dijo «Salió» o «No salió», y cuándo (175) |
| — | índice único filtrado | una sola publicación abierta por definición (`UniqueKeys.FormPublicationOpen`) |

**FormRecords**

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `DefinitionId`, `VersionId` | uuid | FK compuestas |
| `Number` | int | único por definición. Se muestra como `PREFIX-000123`, armado al leer. |
| `SourceKey` | texto ≤200 | único por organización: `whatsapp:<sessionId>` |
| `Channel` | texto | `whatsapp` |
| `RespondentKind` | texto | `Member` o `Contact`, nunca nulo: dice quién respondió aunque el id ya no esté |
| `RespondentUserId` | uuid? | el empleado que respondió; `null` si dio de baja su cuenta |
| `RespondentContactId` | uuid? | el cliente que respondió: el id de su `WhatsAppCustomer` de la E11 (el contacto de la organización, no el `WhatsAppContact` técnico global). Un CHECK ata cada id a su tipo: `Member` no lleva `RespondentContactId`, y `Contact` no lleva `RespondentUserId` |
| `RespondentCompanyIds` | uuid[] | todas las empresas activas del empleado que respondió, al responder (decisión 9); vacío para un cliente |
| `SubmittedAtUtc` | timestamptz | la hora firmada por Meta |
| `CreatedAtUtc` | — | — |

**FormRecordValues**

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `RecordId`, `FieldId`, `Position` | uuid y smallint | PK; `FieldId` sin FK (se valida contra la versión sellada); `Position` es 0, salvo en «elegir varias» (0 a 19, en el orden recibido) |
| `Type` | texto | el tipo de la pregunta |
| `TextValue` | texto ≤10.000 | — |
| `NumberValue` | numeric(28, 10) | sin redondeo |
| `DateValue` | date | `DateOnly` |
| `BoolValue` | bool | — |
| `ChoiceId`, `ChoiceLabel` | uuid, texto | la opción propia, o el ítem de un catálogo (opción a mano, miembro o contacto); `ChoiceLabel` es el texto que vio la persona |
| `CatalogId`, `ChoiceCode`, `ChoiceDetail`, `ChoiceValue` | uuid?, texto? | en una pregunta de catálogo, sellados tal como estaban al responder; `ChoiceValue` es el texto canónico del valor, si el catálogo es tipado |
| `ChoiceFields` | jsonb? | los campos de la opción, sellados (T22) |
| — | CHECK | exactamente un valor |
| — | índice | `(TenantId, CatalogId, ChoiceId)`: si una respuesta eligió una opción, que entonces no se borra (129) |

Una pregunta opcional sin respuesta no tiene fila: es «Sin respuesta», y nunca se confunde con `false` ni con `0`.

**Índices de búsqueda y de orden** ([paginado-y-busqueda](../../rules/paginado-y-busqueda.md)): `IX_FormDefinitions_Search_Name`, GIN trigram en SQL, que usan la lista y `used-by`, y `(TenantId, campo, Id)` por cada campo del `SortMap` de la lista que es una columna (`name` y `updatedAtUtc`). `questionCount` y `status` de la lista, y `usedByCount` del listado de catálogos, son proyecciones: `SortIndexTests` declara su columna, como manda la ficha.

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
| `WabaId` | texto | **[E11]** la cuenta del número |
| `MetaFlowId` | texto? | único; puede faltar en uno que una persona dio por publicado con «Salió» (§6, punto 4) |
| `FlowName` | texto ≤80 | el nombre en Meta (165 y §14), fijado antes del Create; no entra en `FlowJsonHash` |
| `Step` | texto | `Create`, `Upload`, `Validate`, `Publish`, `Verify` |
| `State` | texto | `Accepted`, `InFlight`, `Uncertain`, `Published`, `Failed` |
| `Profile` | texto | el perfil con que se publicó (§14) |
| `FlowJsonHash`, `FieldMap` | texto, jsonb | `field_<N>` ↔ `FieldId`/`ChoiceId` |
| `ErrorCode` y fechas | — | — |
| — | único | `(TenantId, VersionId, WabaId)` |

**WhatsAppFlowSessions**

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `ConversationId` | uuid | **[E11]** |
| `RecipientUserId` o `RecipientContactId`, `SentByUserId` | uuid | quien recibe es un empleado o un cliente (T15) |
| `VersionId`, `BindingId`, `FormName`, `Culture` | — | `VersionId` opaco; `FormName` se copia al mandar, porque una sesión rechazada o pendiente no tiene respuesta de donde leerlo; `Culture` es el idioma de la cuenta de quien recibe, para los textos del sistema (T10) |
| `TokenHash` | texto 64 | único; el token viaja como `flow_token` y no se guarda |
| `ExpiresAtUtc` | timestamptz | se crea + el tiempo para contestar de la configuración de WhatsApp de la organización, leído al mandar (270) |
| `State` | texto | `Pending`, `Completed`, `Rejected`, `Expired`. «Venció sin respuesta» se calcula al leer (`Pending` con `ExpiresAtUtc` pasado), sin worker |
| `RecordId`, `RecordNumber`, `ErrorCode`, `ErrorFieldLabel` | — | resultado de Forms; `ErrorFieldLabel` es el texto de la pregunta que nombra un rechazo |
| `ResponseMessageId` | uuid? | único; el mensaje entrante **[E11]** |
| `ProtectedOffer`, `OfferHash` | texto?, texto? | la oferta de las preguntas de catálogo, cifrada con `IPayloadProtector` (T19): `[{fieldId, options: [{localId, catalogId, itemId, text, detail?, code?, value?}]}]` |

**Lo que se suma a las tablas de la E11 [E11]:**
- **la configuración de WhatsApp de la organización** (la que deja la segunda parte de la base con el plazo de los chats, 272): `FlowSessionHours`, el tiempo para contestar un formulario, 24 por defecto, de 1 a 168 y nunca más que el tope de la organización, que da el resolvedor de topes de las Costuras de módulos ([0069](../../decisions/0069-modulos-y-suscripciones.md)): WhatsApp lo declara con su máximo de la plataforma (`MaxFlowSessionHours`, 168 por defecto) y lo lee por él, con su revisión y el 409. Se lee de la base en cada envío, y una sesión creada antes conserva su vencimiento (270 y [ADR 0060](../../decisions/0060-dos-versiones-conviven.md));
- **mensaje saliente:** un tipo «Formulario» que apunta a la sesión;
- **mensaje entrante:** `ProtectedFlowResponse`, cifrado con `IPayloadProtector` y de hasta 24 KiB. Se borra con la retención de WhatsApp, solo cuando ya se procesó;
- **copia compartida:** `WhatsAppLinkedMessage` suma `FormCard`, la tarjeta de T16. Es contenido del chat y se guarda cifrado, con el propósito de la copia vinculada ([ADR 0065](../../decisions/0065-contenido-del-chat-cifrado.md)): un JSON en `bytea`, no jsonb. El mensaje «Formulario» de la conversación también va cifrado, como todo texto del chat.

## 4. Dominio y errores

**Entidades (`Domain/Forms`):**
- **`FormDefinition`:** `Create`, `SaveDraft` y `ChangePrefix` (prohibido si está bloqueado).
- **`FormDefinitionState`:** `LockPrefix`, `Activate(versionId)` y `IssueNextNumber` (T7).
- **`FormVersion.Seal`.**
- **`FormPublication`:** las transiciones de estado, con `Result`.
- **`FormRecord`**, con sus valores.

**Tipos de pregunta y límites:**
- **Tipos** (`FormQuestionType`): `ShortText`, `LongText`, `Number`, `Date`, `YesNo`, `Choice` y `MultiChoice`.
- **Límites de Forms:** nombre ≤80, prefijo ≤12, hasta 40 preguntas, hasta 50 opciones propias en «elegir una» y de 1 a 20 en «elegir varias» (T13). De un catálogo, hasta 200 en «elegir una» y 20 en «elegir varias», para cada destinatario.
- **Límites de WhatsApp:** los fija el perfil del canal, con `Check(StructuredFormVersion)` (§7 y §14), síncrono y puro.

**Reglas:**
- **Identidades:**
  - el servidor emite `FieldId` y `ChoiceId` (UUID v7);
  - cambiar el tipo de una pregunta crea una identidad nueva;
  - una identidad quitada no vuelve.
- **Guardar y publicar:**
  - el borrador se puede guardar incompleto;
  - para publicar, nombre, preguntas, opciones y botón tienen que tener texto;
  - guardar sin cambios sigue [concurrencia](../../rules/concurrencia.md): compara la versión escribiendo solo `ModifiedAtUtc`, sin emitir identidades ni auditar preguntas;
  - publicar sella una versión nueva solo si el borrador cambió respecto de la activa. Si no cambió, devuelve `Forms.Publication.NothingToPublish`, salvo que una cuenta de WhatsApp conectada no tenga el Flow de la versión activa (103): ahí se sella una versión nueva, con el mismo contenido, que se publica en todas.
- **Prefijo:** se propone desde el nombre (letras sin acentos, en mayúsculas, hasta 3, y un número si ya está tomado). Se edita hasta la primera publicación aceptada, y nunca se recicla.
- **Respuestas:**
  - cada respuesta conserva la versión con la que llegó;
  - la versión que se manda es la activa al momento de mandar;
  - «elegir varias» valida ids distintos de la versión sellada, sin ordenarlos ni quitar repetidos, y entre el mínimo y el máximo.

**Errores** (en `Domain/Forms/<Entidad>Errors.cs`, con textos en `Application/Modules/Forms/Resources/FormsTexts.resx` y `.en.resx`). El modelo es `Domain/Assistant/AssistantErrors.cs` y su `AssistantErrorTextSource`:

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Definition.NotFound` | NotFound | ajena o inexistente |
| `Forms.Definition.PrefixTaken` | Conflict | el prefijo ya existe en la organización |
| `Forms.Definition.PrefixLocked` | Conflict | se quiso cambiar después de publicar |
| `Forms.Definition.PrefixInvalid` | Validation (campo `prefix`) | no cumple el formato |
| `Forms.Draft.QuestionInvalid` | Validation (campo `questions[i]`) | tipo, opciones, decimales o rango de «elegir varias» fuera de regla |
| `Forms.Draft.TooManyQuestions` / `TooManyOptions` | Validation | más de 40 preguntas, más de 50 opciones en «elegir una» o más de 20 en «elegir varias» |
| `Forms.Publication.Incomplete` | Validation (campo por pregunta) | falta un texto obligatorio, o un título no tiene preguntas debajo (T24) |
| `Forms.Publication.ChannelIssues` | Validation | el canal marcó problemas: lista de `{fieldId, choiceId?, path, code, limit?, length?}` (T26) |
| `Forms.Publication.InProgress` | Conflict | ya hay una publicación abierta; trae su `publicationId` (225) |
| `Forms.Publication.NotDiscardable` | Conflict | descartar una publicación que no está `Unconfirmed` (225) |
| `Forms.Meta.TooManyCalls` | TooManyRequests, con `Retry-After` | la organización pasó su tope de consultas a Meta (vista oficial y «Consultar publicación») |
| `Forms.Publication.NothingToPublish` | Conflict | el borrador es igual a la versión activa, y todas las cuentas conectadas tienen su Flow |
| `Forms.Publication.DraftChanged` | Conflict | el borrador cambió después de comprobar (T8) |
| `Forms.Publication.ChannelUnavailable` | ServiceUnavailable | sin WhatsApp, o sin número conectado [E11] |
| `Forms.Publication.NotFound` | NotFound | — |
| `Forms.Publication.NotResolvable` | Conflict | decir qué pasó con una publicación que no está `NeedsReview` (175) |
| `Forms.Preview.NotAvailable` | NotFound | la versión no tiene un Flow publicado con su id |
| `Forms.Record.InvalidResponse` | Validation | valor desconocido, falta uno obligatorio, se pasa del límite o queda fuera del rango de «elegir varias» |
| `Forms.Record.ReplayConflict` | Conflict | la misma sesión llegó con otro contenido |
| `Forms.Record.NotAllowed` | Forbidden | quien responde ya no tiene `formrecords.create` o dejó de ser miembro |
| `Forms.Draft.CatalogUnavailable` | Validation (campo `questions[i]`) | la pregunta apunta a un catálogo dado de baja, que no se puede usar o ajeno |
| `Forms.Draft.CatalogTooLarge` | Validation (campo `questions[i]`) | un catálogo a mano de más de 200 opciones activas en «elegir una», o de más de 20 en «elegir varias» (T23) |
| `Forms.Draft.CatalogPresentationInvalid` | Validation (campo `questions[i]`) | el título o la descripción que arma la presentación de una opción a mano queda vacío o no entra (T22) |
| `Forms.Offer.TooManyOptions` | Conflict | más de 200 opciones en «elegir una», o más de 20 en «elegir varias», para ese destinatario |
| `Forms.Offer.TooFewOptions` | Conflict | menos opciones que el mínimo de una pregunta de «elegir varias» |
| `Forms.Offer.NoOptions` | Conflict | ninguna opción en una pregunta obligatoria |
| `Forms.Offer.CatalogUnavailable` | Conflict | el catálogo dejó de poder usarse |
| `Forms.Offer.PresentationInvalid` | Conflict | el título o la descripción de una opción ofrecida queda vacío o no entra |
| `Forms.Record.OptionWithdrawn` | Conflict | eligió una opción a mano que se dio de baja después de mandar (T20) |
| `WhatsApp.Flow.OptionNotOffered` | Validation | el id elegido no está en la oferta de la sesión |
| `Forms.Catalog.FieldInUse` | Conflict | quitar un campo que usa la presentación de una pregunta: dice qué formulario y qué pregunta (T22) |
| `Forms.Catalog.ItemInUse` (el de la E12) | Conflict | suma `uses: records`: borrar una opción que eligió una respuesta (129) |
| `WhatsApp.Flow.TextTooLong` / `TooManyComponents` / `ResponseTooLarge` / `UnsupportedConfiguration` | Validation | límites del perfil (§14) |
| `WhatsApp.Flow.ProviderRejected` / `ValidationFailed` | Failure | rechazo definitivo de Meta |
| `WhatsApp.Flow.AccountUnavailable` | ServiceUnavailable | la WABA sin medio de pago o sin salud [E11] |
| `WhatsApp.Flow.PreviewUnavailable` | ServiceUnavailable | Meta no contestó la vista oficial en 30 s (§6, punto 5) |
| `WhatsApp.Flow.NotPublishedForNumber` | Conflict | el número del chat está en una WABA sin Flow de esa versión |
| `WhatsApp.Flow.NumberAmbiguous` | Validation | un número que se lee distinto con y sin los miles en el idioma de la sesión, como «1.500» en español (T9, 207) |
| `WhatsApp.Flow.SessionExpired` | Conflict | la respuesta llegó pasada la sesión (hora de Meta) |
| `WhatsApp.Conversation.WindowClosed`, `TakenByOther` y `PaymentRequired` (los de la E11), `WhatsApp.Chat.NotEmployee` / `FormUnavailable` / `SendPending` | Conflict | reglas del botón (§6.6) |

Los códigos siguen `^[A-Z][A-Za-z]+(\.[A-Z][A-Za-z0-9]+){2,}$` (`ErrorCodeTests`). El 409 de edición es el común, `General.ConcurrencyConflict`, y es el único que pone el editor en conflicto: los otros 409 son errores que se corrigen.

## 5. Contrato borrador

Las rutas Business de Forms llevan `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Las del chat son de WhatsApp, van bajo el prefijo de su bandeja **[E11]** y no llevan la feature de formularios: sin Forms, o con `forms` apagada, `IStructuredFormSource.IsAvailableAsync` da `false` y el botón no aparece. Las escrituras que crean o envían llevan `[Idempotent]`. Los listados, `PagedRequest` (10 por defecto) y `filter-counts`. Toda respuesta que consume el front tiene ejemplos exportados de una corrida real, con sus estados y sus errores de negocio. El modelo es `Api/Modules/Assistant/Controllers/BusinessAssistantController.cs`.

| Método y ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| GET `/api/organization/forms` | `forms.read` | `page`, `pageSize`, `sort` (`name`, `questionCount`, `status` o `updatedAtUtc`, y con `-` delante el otro sentido; el estado, por el orden de su circuito, 264), `search`, `status` (`all`, `published`, `drafts`) | 200 `PagedResult<FormSummaryResponse>`: `{id, name, prefix, audience, status, questionCount, updatedAtUtc, activeVersionNumber?, publishedAtUtc?, publication?{state, versionNumber}}`; es la lista «Formularios» (79), con las preguntas del borrador y el último cambio |
| GET `/api/organization/forms/filter-counts` | `forms.read` | — | 200 `{all}`: solo lo que dice el resumen («4 formularios»), porque el filtro «Estado» no lleva cantidades (262) |
| POST `/api/organization/forms` | `forms.manage` | `{name}` | 201 `FormDetailResponse` + Location, con el prefijo propuesto |
| GET `/api/organization/forms/{id}` | `forms.read` | — | 200 `FormDetailResponse`: `{id, name, prefix, prefixLocked, audience, submitLabel, questions[], titles[], activeVersion?{id, number, publishedAtUtc}, publication?{id, state, versionNumber, flowName?, errorCode?, issues?}, canManage, canPublish, version}` |
| PUT `/api/organization/forms/{id}/draft` | `forms.manage` | `{name, prefix, audience, submitLabel, questions[], titles[], version}` | 200 `{version, questions[], titles[]}`, con las identidades emitidas; 409 |
| POST `/api/organization/forms/{id}/publication-checks` | `forms.publish` | — | 200 `{contentHash, canPublish, nextVersionNumber, issues[], changes[]}`, con lo que muestra el diálogo (T25); no escribe (T8) |
| POST `/api/organization/forms/{id}/publications` | `forms.publish` | `{contentHash}`, con `[Idempotent]` | 202 `{publicationId, state}` + Location; no llama a Meta; `DraftChanged` si el borrador cambió (T8). La misma `Idempotency-Key` devuelve la misma publicación («Repetir el mismo intento») |
| GET `/api/organization/forms/{id}/publications/{publicationId}` | `forms.read` | — | 200 `{state, versionNumber, flowName?, errorCode?, issues?}` |
| POST `/api/organization/forms/{id}/publications/{publicationId}/check` | `forms.publish` | — | 200 `{state, versionNumber, flowName?, errorCode?, issues?}`: «Consultar publicación» le pregunta a Meta y nunca repite un POST |
| POST `/api/organization/forms/{id}/publications/{publicationId}/resolution` | `forms.publish` | `{outcome}`, `Published` o `NotPublished` | 200 lo mismo: la persona dice qué pasó, solo en `NeedsReview` (175) |
| POST `/api/organization/forms/{id}/publications/{publicationId}/discard` | `forms.publish` | — | 200 lo mismo: «Descartar esta publicación», solo en `Unconfirmed` (225). Sin `version`, como `resolution`: lo protege el estado, porque el worker escribe la fila en cada consulta |
| GET `/api/organization/forms/{id}/versions/{versionId}/preview` | `forms.read` | — | 200 `{url, expiresAtUtc}`: el enlace interactivo de la vista oficial ([§6, punto 5](14-parte-decisiones-y-flujos.md#6-flujos-con-sus-transacciones)), con `Cache-Control: no-store`; 503 `WhatsApp.Flow.PreviewUnavailable` si Meta no contesta en 30 s |
| GET `/api/organization/whatsapp/{customers\|internal}/conversations/{id}/forms` **[E11]** | «Atender internos» o «Atender clientes», según la bandeja | `page`, `search` | 200 `{available, reason?, culture, forms: PagedResult<{definitionId, versionId, versionNumber, name, prefix, questionCount, optionCounts[{fieldId, label, count}], offerProblems[]}>}`. `available: false` sin `reason` esconde el botón (Forms no está disponible), y con `reason` lo grisa con su motivo; `culture` es el idioma de los textos del sistema que acompañan |
| POST `/api/organization/whatsapp/{customers\|internal}/conversations/{id}/forms` **[E11]** | «Atender internos» o «Atender clientes», según la bandeja | `{definitionId, versionId, text?}`: `null` es el mensaje por defecto (T10), y si viene, ≤1024 | 202 `{sessionId, messageId}` |

**Otras piezas del contrato:**
- **`changes[]` de `publication-checks`:** `{kind, fieldId, label}`, con `kind` `added`, `changed` o `removed`, comparando el borrador guardado con la versión activa. En la primera publicación viene vacío.
- **La campana:** Forms aporta `formPublicationFailed` por `INotificationSource` (base de la 12), a quien tiene `forms.publish`.
- **Tarjeta de respuesta [E11]:** los mensajes de la conversación suman un tipo `form`: `{sessionId, state, errorCode?, formName, displayNumber?, values?: [{fieldId, label, type, value, catalogName?, code?, detail?}], canReadValues}`. En «elegir varias», `value` es la lista de opciones en el orden recibido. El valor de catálogo muestra el texto que vio la persona, y el código y el detalle sellados y el nombre actual del catálogo van cada uno en su propio renglón de lectura, con su rótulo y su valor, nunca debajo del valor ni unidos con « · » (263). Si la respuesta se rechazó con `OptionWithdrawn`, nombra la pregunta, no la opción.
- **Webhook:** la misma ruta de la E8. Se agrega leer `interactive.nfm_reply`.

**La pregunta de catálogo en las rutas:**
- **`FormDetailResponse` y `PUT …/draft`:** las preguntas `Choice` y `MultiChoice` llevan `catalogId?`. El combo de catálogo de la pregunta (111) usa el listado de la E12, con `status=all`: un dado de baja o uno que no se puede usar viene apagado, con su motivo. Pide `formcatalogs.read`; sin ese permiso, «De un catálogo» queda grisado, con el motivo en su tooltip (43).
- **`FormDetailResponse` y `PUT …/draft`:** la pregunta lleva también su `presentation?` propia (T22).
- **`publication-checks`:** suma `Forms.Draft.CatalogUnavailable`, `CatalogTooLarge` y `CatalogPresentationInvalid`.
- **`GET …/conversations/{id}/forms`:** cada formulario trae `offerProblems[{fieldId?, label?, code, minSelections?}]` (`TooManyOptions`, `TooFewOptions`, `NoOptions`, `CatalogUnavailable` y `PresentationInvalid`) y `optionCounts`, la cantidad de opciones de cada pregunta de catálogo con el nombre de la pregunta, nunca los de las opciones. `minSelections` va con `TooFewOptions` («se piden al menos 2»). WhatsApp suma `NotPublishedForNumber`, sin `fieldId`, si el número del chat está en una cuenta sin el Flow de esa versión (103). Si hay algún problema, el formulario aparece pero no se puede mandar.
- **La configuración de WhatsApp de la organización** suma `flowSessionHours`, y la de la plataforma, `maxFlowSessionHours`, cada una en la ruta que ya tiene, con su revisión y el 409 (270).
- **Las rutas de catálogos de la E12:** el listado y el detalle suman `usedByCount` (`null` sin `forms.read`), que el listado suma a su `sort` (264), y `fields[]` suma `inUse` y `usedIn` (`null` sin `forms.read`): hasta 3 `{formId, formName, fieldId, questionLabel, version}`, con `version` en `draft` o `active`, para que «Quitar «…»» diga qué formulario y qué pregunta lo usan («en la versión activa») y adónde lleva «Ir a la pregunta». Sin `forms.read`, el diálogo dice que está en uso, sin nombrarlos. Se suma GET `/api/organization/forms/catalogs/{id}/used-by` (`forms.read`, porque da nombres de formularios; `page`, `pageSize`, `search`, `sort` (`name`) → 200 `PagedResult<{formId, name}>`), con su segunda página en las pruebas de integración.

**Contrato y errores en OpenAPI:** se suman al `FormsContractContributor.cs` de la E12.

## 7. Puertos neutrales del núcleo

**Dónde van** (la carpeta nace en la E12, con `IStructuredContactSource`):
- las interfaces, en `Application/Interfaces/Integrations/StructuredData/`;
- los modelos, en `Application/Models/StructuredData/`;
- las implementaciones apagadas, en `Infrastructure/StructuredData/`. Se registran con `TryAdd`, y cada módulo las reemplaza al componerse.

Los nombres no mencionan formularios ni WhatsApp.

```csharp
// WhatsApp → Forms (implementa Forms)
public interface IStructuredFormSource
{
    Task<bool> IsAvailableAsync(CancellationToken ct);                                   // el módulo y `forms` encendida
    Task<PagedResult<StructuredFormChoice>> ListSendableAsync(StructuredRecipient recipient, PagedRequest page, CancellationToken ct);
    Task<StructuredSendableForm?> FindSendableAsync(StructuredRecipient recipient, Guid definitionId, Guid versionId, CancellationToken ct);
}
// Forms → WhatsApp (implementa WhatsApp)
public interface IStructuredFormChannel
{
    bool IsAvailable { get; }
    Task<bool> HasConnectedNumberAsync(CancellationToken ct);
    Task<bool> HasAccountWithoutFlowAsync(Guid versionId, CancellationToken ct);
    StructuredFormCheck Check(StructuredFormVersion version);                           // puro
    Task<StructuredPublicationResult> PublishAsync(StructuredPublicationRequest request, CancellationToken ct);
    Task<StructuredPublicationResult> GetPublicationAsync(Guid publicationId, CancellationToken ct);
    Task<Result> ResolvePublicationAsync(Guid publicationId, bool published, CancellationToken ct); // «Salió» o «No salió», sin Meta
    Task<Result<StructuredPreview>> GetPreviewAsync(StructuredPreviewRequest request, CancellationToken ct);
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
- `StructuredFormVersion(DefinitionId, VersionId, Number, Prefix, Name, Audience, SubmitLabel, Questions[FieldId, Type, Label, Help, Required, MaxLength?, Decimals?, MinSelections?, MaxSelections?, CatalogId?, Presentation?, Options[ChoiceId, Label]], Titles[TitleId, Text, BeforeFieldId], ContentHash)`;
- `StructuredSendableForm(Version, Offers[FieldId → Options[ItemRef(CatalogId, ItemId), Text, Detail?, Code?, Value?]], Problems[FieldId, Code])`: lo que se manda, con la oferta de cada pregunta de catálogo (§6b);
- `StructuredFormChoice(DefinitionId, VersionId, VersionNumber, Name, Prefix, QuestionCount, OptionCounts[FieldId, Label, Count], OfferProblems[FieldId?, Label?, Code, MinSelections?])`, de `ListSendableAsync`, con lo que muestra el diálogo;
- `StructuredRecipient(Kind: Member|Contact, UserId?, ContactId?)`: quien recibe (T15). `ContactId` es el id del `WhatsAppCustomer` de la organización;
- `StructuredRecordCapture(SourceKey, DefinitionId, VersionId, Respondent: StructuredRecipient, SubmittedAtUtc, Values[FieldId, Text?|Number?|Date?|Bool?|Choices[]?])`. Cada opción elegida es `StructuredChosenOption(ChoiceId, Label, CatalogId?, Code?, Detail?, Value?)`, y «elegir varias» manda una por opción, en el orden recibido;
- `StructuredRecordReceipt(RecordId, Number, DisplayNumber, Card)`: la tarjeta, con el nombre del formulario y lo que mandó, para la copia de T16;
- `StructuredRecordCard(RecordId, DisplayNumber, FormName, CanReadValues, Values?)`, con el catálogo, el código y el detalle de cada valor de catálogo;
- `StructuredPublicationResult(State: Published|Pending|Uncertain|Failed, RequiresReview, FlowName, Issues[], NextCheckAtUtc?)`: `RequiresReview` va con `Uncertain` cuando la verificación no puede decidir (§6, punto 4);
- `StructuredPreviewRequest(VersionId, Culture, Samples[FieldId → Options[Text, Detail?]])` y `StructuredPreview(Url, ExpiresAtUtc)`: las opciones de ejemplo de la vista oficial, que viajan en memoria y no se guardan (176).

**Implementaciones apagadas:**
- **Sin WhatsApp:** `IsAvailable = false` y ningún número. Se puede diseñar, y publicar responde `ChannelUnavailable`.
- **Sin Forms:** la fuente da `IsAvailableAsync` en `false` y una lista vacía, el botón no aparece y la captura devuelve un error que deja la sesión `Rejected`.
