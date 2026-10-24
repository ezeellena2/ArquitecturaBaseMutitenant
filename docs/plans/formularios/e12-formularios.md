# E12 · Formularios: crear, publicar, mandar y recibir (spec)

**Estado:** spec de la primera etapa del bloque (decisión 34), con las decisiones 35, 38 y 43. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E11]**, **[E9]** y **[E10]** contra el código real.

**Fuentes:**
- alcance y decisiones de producto: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E12 y §5;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- conocimiento de Meta: ArquitecturaBase ([sección 14](#14-referencia-de-meta-tomada-de-arquitecturabase)). Es solo referencia, no código para copiar.

**Requisitos:** la E11 (números por organización, bandeja Internos, token por organización), la E9 (Redis, límites y fuzz) y la E10.

## 1. Alcance

**Entra:**
- **Editor:** formularios de la organización con siete tipos de pregunta, entre ellos «elegir varias» (38), prefijo de numeración y vista previa en el teléfono. El contenido es un solo texto por campo, tal como lo escribe la persona, en el idioma que quiera (35).
- **Publicación en WhatsApp:**
  - un Flow por versión y por cuenta de WhatsApp Business (WABA);
  - operación durable, con sus estados visibles y la vista previa oficial de Meta.
- **Envío desde el chat:** «Enviar formulario» en un chat de Internos (a un empleado) o de Clientes (a un cliente), con la sesión de 24 horas. Cada formulario dice para quién es: solo empleados, solo clientes, o los dos (49 y 50).
- **Recepción:**
  - el `nfm_reply` se convierte en una respuesta numerada (`VIS-000123`), sin duplicados;
  - en el chat aparece una tarjeta con los valores, o solo con el número si quien mira no puede ver respuestas.
- **El módulo:**
  - `Forms` quitable, con su feature `forms` y sus permisos;
  - los cuatro puertos neutrales del núcleo con sus implementaciones apagadas;
  - el retiro probado una vez.

**No entra:**
- dar de baja, reactivar, eliminar y deprecar los Flows (E12b);
- catálogos (E13 a E13c);
- fotos y documentos (E14);
- «Formularios», planilla, detalle de una respuesta y Excel (E15);
- reglas y aprobación (E16);
- biblioteca de plantillas (E17);
- prellenado del asistente y sugerencias (E18);
- copiar formularios y límites por plan.

## 2. Decisiones técnicas

Las de producto están en la hoja de ruta (§5). Estas siguen las reglas del repo y, cuando hay más de una forma, la más simple.

| # | Decisión | Por qué |
| --- | --- | --- |
| T1 | `Forms` y `WhatsApp` se hablan solo por cuatro puertos neutrales del núcleo (§7). | `ModuleIsolationTests` prohíbe que un módulo use tipos de otro. |
| T2 | **«Enviar formulario» es de WhatsApp.** La pantalla del chat, la ruta, la sesión y el mensaje van en el módulo WhatsApp, que lista los formularios con `IStructuredFormSource`. | El que manda es el operador desde el chat. |
| T3 | El borrador usa `IVersioned` y 409 `General.ConcurrencyConflict`, sin recibos de cambios. | La edición es interna (robustez proporcional, §4 de la hoja de ruta). |
| T4 | **Un solo worker de publicación, el de Forms.** Descubre trabajos en `platform.FormPublicationWorks`, entra con `ExecutionActor` y llama a las fases de `IFormPublicationService`. Fuera de toda UoW llama al canal, que coordina Meta con las fases de `IWhatsAppFlowPublicationService`; cada fase que escribe abre una sola UoW. | Copia la separación entre `AssistantTurnWorker` e `IAssistantTurnService`; ni el worker ni el adaptador neutral reciben `IUnitOfWork`. |
| T5 | Los workers entran con `IExecutionActorFactory` e `IExecutionActorScope`, no con `ITenantScope.Enter` solo. | Con `Enter` solo, `IFeatureService` da `false`, porque `Enter` deja `TenantKind` en null (backlog #51). |
| T6 | **Un Flow por versión y por WABA.** Los dos textos del sistema que van adentro del Flow (la ayuda de los números y «Elegí hasta N») viajan en el `data` del mensaje, en el idioma de la cuenta de quien recibe (35 y 42). | El contenido no tiene idioma; los textos del sistema sí, y cambian por destinatario sin otro Flow. |
| T7 | **Numeración:** `LastRecordNumber` en la definición, sumado bajo bloqueo de fila al capturar, con índice único (definición, número). El prefijo es único **dentro de la organización**. | Sin bloqueos globales: cada organización numera aparte. |
| T8 | **Sin ticket de comprobación:** `publication-checks` solo informa, y `publications` vuelve a comprobar todo adentro de su transacción. | La comprobación final es la que vale. |
| T9 | **«Número con decimales»:** cada pregunta fija sus decimales (de 0 a 6) y admite hasta 15 dígitos enteros. Al responder se acepta punto o coma; más decimales que los permitidos se rechaza. | Evita que «125.500» se lea como 125,5: con 0 decimales, se rechaza. |
| T10 | **Lo que recibe la persona:** el formulario sale como está escrito. El mensaje por defecto, el botón «Abrir formulario» y los textos del sistema del Flow van en el idioma de su cuenta (35 y 42). La sesión guarda ese idioma. | Un formulario se ve y se manda igual con el menú en cualquier idioma. |
| T11 | Los valores viven en filas tipadas (`FormRecordValues`), sin un JSON paralelo. «Elegir varias» guarda una fila por opción, con su posición. | Una sola fuente. La planilla de la E15 filtra sobre las columnas. |
| T12 | **Las capturas rechazadas no crean nada en Forms.** El rechazo queda en la sesión de WhatsApp, con su código. | La idempotencia la da el índice único `SourceKey`. |
| T13 | **«Elegir varias» es un tipo propio** (`MultiChoice`), no una marca de la lista: de 1 a 20 opciones, con `0 ≤ mínimo ≤ máximo ≤ 20` y máximo mayor que cero. Es obligatoria si y solo si el mínimo es mayor que cero. Pasar de «elegir una» a «elegir varias» crea otra identidad; cambiar el rango la conserva. Sin ninguna marcada es «Sin respuesta». | Es la regla de ArquitecturaBase (C1-04), con el tope de WhatsApp. |
| T14 | **Un mensaje sin datos no lleva `data`.** Un mapa vacío (`prefill_FORM:{}`) hace que el Flow no abra en Android. | Error ya pagado en el piloto de ArquitecturaBase. |
| T15 | **Quien recibe y quien responde es un empleado o un cliente** (49). La sesión y la respuesta guardan uno de los dos: el usuario del miembro, o el contacto de la bandeja Clientes. A un empleado se le revisan la membresía y `formrecords.create`; a un cliente, que el formulario sea para clientes. | Un cliente no tiene cuenta ni permisos: se lo reconoce por el contacto, que es su número. |
| T16 | **La respuesta de un cliente es dato de la organización** (`ITenantOwned`). Si el contacto vinculó su cuenta personal, ve la copia por la copia de la conversación de la E11, sin datos compartidos. Si el contacto se borra, la respuesta queda (decisión 23) sin el vínculo ni el nombre. | La regla de datos personales y la D4 de la E11. |

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
| `PrefixLockedAtUtc` | timestamptz? | se fija en la primera publicación aceptada |
| `Draft` | jsonb | `FormDraft` (abajo) |
| `ActiveVersionId` | uuid? | FK compuesta a `FormVersions` |
| `LastRecordNumber` | int | arranca en 0 |
| `CreatedAtUtc` y demás auditoría | — | `IAuditable` |
| `Version` | xmin | 409 |

**`FormDraft`** (jsonb, `format: 1`). Cada texto es uno solo:
- `submitLabel`;
- `questions[]`, cada una con `fieldId`, `type`, `label`, `help`, `required`, `maxLength?`, `decimals?`, `minSelections?`, `maxSelections?` y `options[]`;
- cada opción lleva `{choiceId, label}`.

**FormVersions** (inmutable)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `DefinitionId` | uuid | FK compuesta |
| `Number` | int | único por definición |
| `Name`, `Questions`, `SubmitLabel` | texto y jsonb | copia sellada |
| `ContentHash` | texto 64 | SHA-256 del JSON canónico (formato versionado y orden fijo) |
| `CreatedAtUtc`, `CreatedByUserId` | — | — |

**FormPublications** (`IVersioned`)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `DefinitionId`, `VersionId` | uuid | `VersionId` es único |
| `State` | texto | `Accepted`, `Preparing`, `Publishing`, `Unconfirmed`, `NeedsReview`, `ActivationPending`, `Published`, `Failed` |
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
| `RespondentUserId` | uuid? | el empleado que respondió |
| `RespondentContactId` | uuid? | el cliente que respondió (contacto de la E11); `null` si el contacto se borró. Un CHECK exige uno de los dos al crear |
| `RespondentCompanyIds` | uuid[] | todas las empresas activas del empleado que respondió, al responder (decisión 9); vacío para un cliente |
| `SubmittedAtUtc` | timestamptz | la hora firmada por Meta |
| `CreatedAtUtc` | — | — |

**FormRecordValues**

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `RecordId`, `FieldId`, `Position` | uuid y smallint | PK; `FieldId` sin FK (se valida contra la versión sellada); `Position` es 0, salvo en «elegir varias» (0 a 19, en el orden recibido) |
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
| `WabaId` | texto | **[E11]** la cuenta del número |
| `MetaFlowId` | texto? | único |
| `Step` | texto | `Create`, `Upload`, `Validate`, `Publish`, `Verify` |
| `State` | texto | `Accepted`, `InFlight`, `Uncertain`, `Published`, `Failed` |
| `FlowJsonHash`, `FieldMap` | texto, jsonb | `field_<N>` ↔ `FieldId`/`ChoiceId` |
| `ErrorCode` y fechas | — | — |
| — | único | `(TenantId, VersionId, WabaId)` |

**WhatsAppFlowSessions**

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId` | uuid | — |
| `ConversationId` | uuid | **[E11]** |
| `RecipientUserId` o `RecipientContactId`, `SentByUserId` | uuid | quien recibe es un empleado o un cliente (T15) |
| `VersionId`, `BindingId`, `Culture` | — | `VersionId` opaco; `Culture` es el idioma de la cuenta de quien recibe, para los textos del sistema (T10) |
| `TokenHash` | texto 64 | único; el token viaja como `flow_token` y no se guarda |
| `ExpiresAtUtc` | timestamptz | se crea + `WhatsApp:FlowSessionHours` (24 por defecto, de 1 a 168) |
| `State` | texto | `Pending`, `Completed`, `Rejected`, `Expired`. «Venció sin respuesta» se calcula al leer (`Pending` con `ExpiresAtUtc` pasado), sin worker |
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
- **Tipos** (`FormQuestionType`): `ShortText`, `LongText`, `Number`, `Date`, `YesNo`, `Choice` y `MultiChoice`.
- **Límites de Forms:** nombre ≤80, prefijo ≤12, hasta 40 preguntas, hasta 50 opciones en «elegir una» y de 1 a 20 en «elegir varias» (T13).
- **Límites de WhatsApp:** los fija el perfil del canal, con `Check(StructuredFormVersion)` (§7 y §14), síncrono y puro.

**Reglas:**
- **Identidades:**
  - el servidor emite `FieldId` y `ChoiceId` (UUID v7);
  - cambiar el tipo de una pregunta crea una identidad nueva;
  - una identidad quitada no vuelve.
- **Guardar y publicar:**
  - el borrador se puede guardar incompleto;
  - para publicar, nombre, preguntas, opciones y botón tienen que tener texto;
  - guardar sin cambios no escribe: no cambian la versión, `ModifiedAtUtc` ni la auditoría;
  - publicar sella una versión nueva solo si el borrador cambió respecto de la activa. Si no cambió, devuelve `Forms.Publication.NothingToPublish`.
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
| `Forms.Publication.Incomplete` | Validation (campo por pregunta) | falta un texto obligatorio |
| `Forms.Publication.ChannelIssues` | Validation | el canal marcó problemas: lista de `{fieldId, choiceId?, path, code, limit?}` |
| `Forms.Publication.InProgress` | Conflict | ya hay una publicación abierta |
| `Forms.Publication.NothingToPublish` | Conflict | el borrador es igual a la versión activa |
| `Forms.Publication.ChannelUnavailable` | ServiceUnavailable | sin WhatsApp, o sin número conectado [E11] |
| `Forms.Publication.NotFound` | NotFound | — |
| `Forms.Preview.NotAvailable` | NotFound | la versión no tiene un Flow publicado |
| `Forms.Record.InvalidResponse` | Validation | valor desconocido, falta uno obligatorio, se pasa del límite o queda fuera del rango de «elegir varias» |
| `Forms.Record.ReplayConflict` | Conflict | la misma sesión llegó con otro contenido |
| `Forms.Record.NotAllowed` | Forbidden | quien responde ya no tiene `formrecords.create` o dejó de ser miembro |
| `WhatsApp.Flow.TextTooLong` / `TooManyComponents` / `ResponseTooLarge` | Validation | límites del perfil (§14) |
| `WhatsApp.Flow.ProviderRejected` / `ValidationFailed` | Failure | rechazo definitivo de Meta |
| `WhatsApp.Flow.AccountUnavailable` | ServiceUnavailable | la WABA sin medio de pago o sin salud [E11] |
| `WhatsApp.Flow.NotPublishedForNumber` | Conflict | el número del chat está en una WABA sin Flow de esa versión |
| `WhatsApp.Flow.SessionExpired` | Conflict | la respuesta llegó pasada la sesión (hora de Meta) |
| `WhatsApp.Chat.WindowClosed` / `NotEmployee` / `FormUnavailable` / `SendPending` | Conflict | reglas del botón (§6.6) |

Los códigos siguen `^[A-Z][A-Za-z]+(\.[A-Z][A-Za-z0-9]+){2,}$` (`ErrorCodeTests`). El 409 de edición es el común, `General.ConcurrencyConflict`, y es el único que pone el editor en conflicto: los otros 409 son errores que se corrigen.

## 5. Contrato borrador

Todas las rutas Business llevan `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Las escrituras que crean o envían llevan `[Idempotent]`. Los listados, `PagedRequest` (10 por defecto) y `filter-counts`. Toda respuesta que consume el front tiene ejemplos exportados de una corrida real, con sus estados y sus errores de negocio. El modelo es `Api/Modules/Assistant/Controllers/BusinessAssistantController.cs`.

| Método y ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| GET `/api/organization/forms` | `forms.read` | `page`, `pageSize`, `sort` (`name`, `updatedAtUtc`), `search`, `status` (`all`, `published`, `drafts`) | 200 `PagedResult<FormSummary>`: `{id, name, prefix, status, activeVersionNumber?, publishedAtUtc?, publicationState?}` |
| GET `/api/organization/forms/filter-counts` | `forms.read` | `search` | 200 `{all, published, drafts}` |
| POST `/api/organization/forms` | `forms.manage` | `{name}` | 201 `FormDetail` + Location, con el prefijo propuesto |
| GET `/api/organization/forms/{id}` | `forms.read` | — | 200 `FormDetail`: `{id, name, prefix, prefixLocked, submitLabel, questions[], activeVersion?{id, number, publishedAtUtc}, publication?{id, state, errorCode?, issues?}, canManage, canPublish, version}` |
| PUT `/api/organization/forms/{id}/draft` | `forms.manage` | `{name, prefix, submitLabel, questions[], version}` | 200 `{version, questions[]}`, con las identidades emitidas; 409 |
| POST `/api/organization/forms/{id}/publication-checks` | `forms.publish` | `{version}` | 200 `{canPublish, nextVersionNumber, issues[], changes[]}`; no escribe |
| POST `/api/organization/forms/{id}/publications` | `forms.publish` | `{version}` | 202 `{publicationId, state}` + Location; no llama a Meta |
| GET `/api/organization/forms/{id}/publications/{publicationId}` | `forms.read` | — | 200 `{state, errorCode?, issues?, versionNumber}` |
| GET `/api/organization/forms/{id}/versions/{versionId}/preview` | `forms.read` | — | 200 `{url, expiresAtUtc}`, con `Cache-Control: no-store` |
| GET `/api/organization/whatsapp/conversations/{id}/forms` **[E11]** | «Atender internos» o «Atender clientes», según la bandeja | `page`, `search` | 200 `{available, reason?, culture, forms: PagedResult<{definitionId, versionId, name, prefix}>}`; `culture` es el idioma de los textos del sistema que acompañan |
| POST `/api/organization/whatsapp/conversations/{id}/forms` **[E11]** | «Atender internos» o «Atender clientes», según la bandeja | `{definitionId, versionId, text}` (≤1024) | 202 `{sessionId, messageId}` |

**Otras piezas del contrato:**
- **Tarjeta de respuesta [E11]:** los mensajes de la conversación suman un tipo `form`: `{sessionId, state, formName, displayNumber?, values?: [{fieldId, label, type, value}], canReadValues}`. En «elegir varias», `value` es la lista de opciones en el orden recibido.
- **Webhook:** la misma ruta de la E8. Se agrega leer `interactive.nfm_reply`.
- **Plataforma:** no tiene rutas nuevas. La feature `forms` aparece sola en la pestaña «Módulos» de la ficha de la organización.

**Contrato y errores en OpenAPI:** `Api/Modules/Forms/FormsContractContributor.cs`, como `Api/Modules/Assistant/AssistantContractContributor.cs`.

## 6. Flujos, con sus transacciones

«UoW» es una `ExecuteInTransactionAsync(…, CommitPolicy.OnSuccess, ct)`. Cada método público de servicio va en `OperationLog.RunAsync`. Las escrituras de definición pertenecen a `IFormDefinitionService`; las de publicación, a `IFormPublicationService`. Los workers coordinan las fases, y los adaptadores neutrales delegan en los servicios del módulo; ninguno abre una UoW ni recibe `IUnitOfWork`.

1. **Crear.**
   - Afuera de la UoW se valida el nombre.
   - En la UoW: se propone el prefijo libre, se crea la definición con el borrador vacío y se responde 201.
2. **Guardar el borrador.**
   - Afuera: se validan el pedido y las reglas de las preguntas.
   - En la UoW:
     - se carga la definición con la versión esperada;
     - si el contenido no cambió, no se escribe nada y se responde la misma versión;
     - si el prefijo cambió, se comprueba que no esté bloqueado y que esté libre (el choque del índice se traduce a `PrefixTaken`);
     - se emiten las identidades nuevas y se guarda.
   - Responde 200 `{version, questions}`.
3. **Comprobar.** Es solo lectura:
   - reglas de Forms y textos obligatorios;
   - `IStructuredFormChannel.Check(StructuredFormVersion)`, síncrono y puro: sin escrituras, sin bloqueos y sin Meta;
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
     1. busca trabajos listos y entra con `ExecutionActor` (el operador que publicó, revalidado: si perdió `forms.publish` antes de tocar Meta, la publicación termina `Failed`). Llama a `IFormPublicationService.ClaimAsync`, que reserva en su única UoW y pasa a `Preparing`;
     2. fuera de toda UoW llama a `IStructuredFormChannel.PublishAsync`:
        - el canal arma el Flow de la versión para cada WABA con números conectados **[E11]**;
        - por cada uno sigue los pasos Create → Upload → Validate → Publish → Verify;
        - el adaptador `WhatsAppStructuredFormChannel` llama a `IWhatsAppFlowPublicationService.BeginStepAsync` para reservar el binding/paso y guardar `InFlight` en su única UoW; hace la llamada a Meta después del commit y entrega el resultado a `CompleteStepAsync`, que abre otra UoW. El adaptador no abre transacciones;
     3. entrega lo que devolvió el canal a `IFormPublicationService.CompleteAsync`, que en su única UoW aplica:
        - `Published` → `ActivationPending`, y se activa la versión en la misma UoW → `Published`;
        - `Uncertain` → `Unconfirmed`, con `NextCheckAtUtc`;
        - rechazo definitivo → `Failed`, con sus `issues`.
   - **Cuando quedó `Unconfirmed`,** el worker vuelve a consultar con `GetPublicationAsync`, que solo verifica y nunca repite un POST.
   - **Si se cae a mitad:** la reserva vuelve a tomar el trabajo, y el canal, al ver un `InFlight`, verifica en vez de repetir.
5. **Vista previa:** `IStructuredFormChannel.GetPreviewAsync(versionId)` devuelve la URL de Meta con su vencimiento, sin guardar nada.
6. **Mandar desde el chat** (módulo WhatsApp).
   - **Ubicación:** en escritorio, el botón «Enviar formulario» al lado de «Enviar»; en el teléfono, la opción «Enviar formulario» en el ⋮ del chat. Ambos abren el mismo diálogo. Si la acción no aplica al acceso, bandeja o permiso, no se muestra.
   - **Cuándo se puede usar:**
     - en Internos, quien escribe es miembro activo y el operador tiene «Atender internos» (decisión 10) **[E11]**;
     - en Clientes, el operador tiene «Atender clientes» **[E11]**;
     - la ventana de 24 horas está abierta;
     - el número está disponible;
     - no hay otro envío pendiente ni sin confirmar;
     - `IStructuredFormSource.ListSendableAsync(recipient)` devuelve al menos un formulario. Forms filtra los publicados para ese tipo de destinatario (50) y, a un empleado, los que puede responder (`formrecords.create`).
   - **Si no se puede usar:** el botón y la opción del ⋮ quedan grisados, con el motivo en su tooltip (43).
   - **Mandar:**
     - afuera de la UoW se valida el texto (≤1024) y se lee el formulario elegido con `FindSendableAsync`. El idioma de los textos del sistema es el de la cuenta de quien recibe (T10);
     - en la UoW: bloqueo de la conversación, se revalidan ventana, empleado y envío pendiente, se toma el chat si no estaba tomado (regla de la E11), se crea la sesión (token aleatorio de 32 bytes; se guarda solo el hash) y se encola el mensaje interactivo de tipo Flow por el outbox (`WhatsAppOutboundMessage` con su variante nueva).
   - **Si la WABA del número no tiene Flow de esa versión:** `WhatsApp.Flow.NotPublishedForNumber`.
7. **Recibir y capturar** (worker de WhatsApp, siguiendo la entrada de la E11 **[E11]**).
   - **Webhook:** guarda el mensaje con `ProtectedFlowResponse`, sin duplicados por `wamid`.
   - **El worker,** por cada mensaje, aislando los errores mensaje por mensaje:
     1. saca el hash del `flow_token` y busca la sesión;
     2. compara la hora firmada de Meta con `ExpiresAtUtc`: si se pasó, `Expired`;
     3. decodifica con el `FieldMap` del binding (§14); los arrays de «elegir varias» van a la captura, nunca a la descarga de medios;
     4. revalida y entra con `ExecutionActor` (quien respondió) y, sin transacción abierta, llama a `IStructuredRecordSink.CaptureAsync`. Las escrituras previas de reserva o vencimiento son fases de `IWhatsAppFlowReplyService.ClaimAsync`, con una única UoW; el worker no las abre.
   - **La captura en Forms:** `FormRecordSink` delega en `IFormRecordService.CaptureAsync`, que abre su única UoW:
     - busca el `SourceKey`: si ya existe con el mismo contenido, devuelve el mismo número; si el contenido es otro, `ReplayConflict`;
     - a un empleado le revalida la membresía y `formrecords.create`; a un cliente, que el formulario siga siendo para clientes;
     - valida estricto contra la versión sellada;
     - bloquea la definición y suma `LastRecordNumber`;
     - guarda la respuesta y sus valores.
   - **Después:** `IWhatsAppFlowReplyService.CompleteAsync` abre su única UoW en WhatsApp y marca la sesión `Completed` (con número) o `Rejected` (con código).
   - **Si se cae entre las dos UoW:** se repite y devuelve el mismo número.
8. **Tarjeta en el chat.**
   - Al listar los mensajes, WhatsApp llama a `IStructuredRecordSource.ReadAsync(recordIds, culture)`, con el idioma de quien mira para los textos del sistema («Sí», «No»).
   - Forms decide con los permisos de quien mira: con `formrecords.read` devuelve los valores, y sin él solo el número.
   - En la lista de chats, la vista previa es el texto fijo «Respuesta del formulario».
9. **Retención y baja.**
   - La retención de WhatsApp borra `ProtectedFlowResponse` cuando el mensaje ya se procesó; la respuesta y su número quedan (decisión 23).
   - `Application/Modules/Forms/Services/FormsAccountDeletionParticipant` decide las reglas dentro del alcance y la UoW de la baja, sin abrir otra ([datos-personales](../../rules/datos-personales.md)). Copia `Application/Modules/Assistant/Services/AssistantAccountDeletionParticipant.cs`: usa un puerto `IFormsAccountDeletionCleanupRepository`, cuya implementación en Infrastructure hace solo el cleanup EF y exige la transacción del llamador. Lo protege `AssistantMaintenanceLayerTests`, que se extiende para Forms.

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
    Task<PagedResult<StructuredFormChoice>> ListSendableAsync(StructuredRecipient recipient, PagedRequest page, CancellationToken ct);
    Task<StructuredFormVersion?> FindSendableAsync(StructuredRecipient recipient, Guid definitionId, Guid versionId, CancellationToken ct);
}
// Forms → WhatsApp (implementa WhatsApp)
public interface IStructuredFormChannel
{
    bool IsAvailable { get; }
    StructuredFormCheck Check(StructuredFormVersion version);                           // puro
    Task<StructuredPublicationResult> PublishAsync(StructuredPublicationRequest request, CancellationToken ct);
    Task<StructuredPublicationResult> GetPublicationAsync(Guid publicationId, CancellationToken ct);
    Task<StructuredPreview?> GetPreviewAsync(Guid versionId, CancellationToken ct);
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
- `StructuredFormVersion(DefinitionId, VersionId, Number, Prefix, Name, SubmitLabel, Questions[FieldId, Type, Label, Help, Required, MaxLength?, Decimals?, MinSelections?, MaxSelections?, Options[ChoiceId, Label]], ContentHash)`;
- `StructuredRecipient(Kind: Member|Contact, UserId?, ContactId?)`: quien recibe (T15);
- `StructuredRecordCapture(SourceKey, DefinitionId, VersionId, Respondent: StructuredRecipient, SubmittedAtUtc, Values[FieldId, Text?|Number?|Date?|Bool?|ChoiceId?|ChoiceIds[]?])`;
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
| `A/Modules/Forms/Interfaces/Services/IFormDefinitionService.cs`, `IFormPublicationService.cs`, `IFormRecordService.cs` | `IAssistantConversationService.cs`; `IAssistantTurnService.cs` para las fases |
| `A/Modules/Forms/Services/FormDefinitionService.cs`, `FormPublicationService.cs`, `FormRecordService.cs` y los helpers `FormPrefixResolver`, `FormDraftEditor`, `FormVersionCreator`, `FormRecordNumberIssuer`, `FormRecordWriter` | `AssistantConversationService.cs`, `AssistantTurnService.cs` y sus helpers |
| `A/Modules/Forms/Services/FormStructuredSource.cs`, `FormRecordSink.cs`, `FormRecordSource.cs` (implementan los puertos) | `A/Modules/WhatsApp/Services/WhatsAppLoginCodeChannel.cs` (un módulo implementa un puerto del núcleo) |
| `A/Modules/Forms/Interfaces/Persistence/IFormDefinitionRepository.cs`, `IFormReader.cs`, `UniqueKeys.cs` | `A/Modules/Assistant/Interfaces/Persistence/` |
| `A/Modules/Forms/Models/`, `Validation/`, `Resources/FormsTexts{,.en}.resx`, `FormsErrorTextSource.cs` | las mismas carpetas del Asistente |
| `A/Interfaces/Integrations/StructuredData/*.cs`, `A/Models/StructuredData/*.cs` | `A/Interfaces/Integrations/Messaging/ILoginCodeChannel.cs` |
| `I/StructuredData/Disabled*.cs` | `I/Messaging/Email/EmailLoginCodeChannel.cs` (implementación del núcleo) |
| `I/Modules/Forms/FormsModule.cs`, `Persistence/FormsModelContributor.cs`, `Persistence/Configurations/*.cs`, `Persistence/Repositories/*.cs`, `Persistence/Readers/*.cs` | `I/Modules/Assistant/...` (`AssistantModelContributor.cs`) |
| `A/Modules/Forms/Services/FormsAccountDeletionParticipant.cs`, `A/Modules/Forms/Interfaces/Persistence/IFormsAccountDeletionCleanupRepository.cs`; `I/Modules/Forms/Persistence/Repositories/FormsAccountDeletionCleanupRepository.cs` | `A/Modules/Assistant/Services/AssistantAccountDeletionParticipant.cs`, su puerto de cleanup y `I/Modules/Assistant/Persistence/Repositories/AssistantAccountDeletionCleanupRepository.cs`; `AssistantMaintenanceLayerTests` |
| `I/Modules/Forms/FormPublicationWorker.cs` | `I/Modules/Assistant/AssistantTurnWorker.cs` (descubre, entra con actor y llama fases del servicio) |
| `I/Persistence/Migrations/<fecha>_FormsModule.cs` y `<fecha>_SystemRoleFormsPermissions.cs` | `20261010011328_AssistantModule.cs`, `20261010203618_SystemRoleAssistantPermissions.cs` |
| `P/Modules/Forms/FormsApiModule.cs`, `FormsContractContributor.cs`, `Controllers/BusinessFormsController.cs`, `Contracts/*HttpRequest.cs` | `P/Modules/Assistant/...` |
| WhatsApp: `A/Modules/WhatsApp/Services/WhatsAppFlowBuilder.cs`, `WhatsAppFlowReplyVerifier.cs`, `WhatsAppFormSendService.cs`, `WhatsAppStructuredFormChannel.cs`; `I/Modules/WhatsApp/WhatsAppFlowClient.cs`, el worker de respuestas y las configuraciones de `WhatsAppFlowBindings` y `WhatsAppFlowSessions` | `I/Modules/WhatsApp/WhatsAppCloudClient.cs`, `WhatsAppInboundProcessor.cs` y lo que deje la E11 **[E11]** |
| WhatsApp: `A/Modules/WhatsApp/Interfaces/Services/IWhatsAppFlowPublicationService.cs`, `IWhatsAppFlowReplyService.cs` y sus implementaciones en `Services/` | `IAssistantTurnService.cs` y `AssistantTurnService.cs` (fases con una UoW cada una) |
| `Program.cs`: `AddFormsModule` en las tres capas | la línea del Asistente |

**Front** (`F/` = `ArquitecturaBaseMutitenantFront/`):

| Archivo nuevo | Modelo |
| --- | --- |
| `F/src/modules/forms/registration.ts`: rutas `/org/formularios/disenar` y `/org/formularios/disenar/:id`; guarda con `features.includes("forms")` y `forms.read`; la entrada «Gestión de formularios» del menú (34), en el teléfono y en la compu; aporta `resources` es/en de `forms` y sus entradas de `enums` | `F/src/modules/assistant/registration.ts` |
| `F/src/modules/forms/{api,components,hooks,pages}/`, `errors.ts` | `F/src/modules/assistant/` |
| `F/src/modules/forms/locales/{es,en}/forms.json` y `enums.json` | `F/src/modules/assistant/locales/{es,en}/assistant.json` y `enums.json`; importados por la registration del módulo |
| `F/src/app/moduleComposition.ts`: suma `formsRegistration` | la línea del Asistente |
| WhatsApp **[E11]**: el diálogo «Enviar formulario» y la tarjeta de respuesta, en el módulo de WhatsApp del front que deje la E11 | — |

**Punteros:** cada carpeta nueva lleva su `AGENTS.md` de una a tres líneas («qué va acá · Leé ficha · Copiá de archivo») y un `CLAUDE.md` con `@AGENTS.md`, más su fila en `docs/architecture/arbol.md`. **Se crean con la carpeta, en la E12, no antes** (`HarnessTests`). Al cerrar la etapa nacen `docs/features/formularios.md` y la fila de Forms en «Dónde va cada cosa» de `AGENTS.md`.

## 9. Pantallas (tableros)

**Navegación (34):** el menú principal dice «Gestión de formularios». En esta etapa su panel tiene solo «Diseñar formularios»; Catálogos, Formularios y Formularios que faltan llegan con sus etapas.

**Diseño redibuja** (evaluación del traspaso 2, §5.1), sin «Textos en», «Cambios sin guardar», revisiones junto a Guardar ni textos junto a los botones:
- `FRM-Editor` y `M-FRM-Editor`: el editor en una sola pantalla:
  - el formulario (nombre, texto del botón final y prefijo);
  - las preguntas, que se abren en el lugar, con ⋮ (Editar, Subir, Bajar y Quitar);
  - «elegir varias», con el mínimo y el máximo;
  - «Así lo ve el empleado» al costado.

  En el teléfono, la pregunta se edita en una hoja y los botones van fijos abajo.
- `FRM-Publicar`: «Publicar la versión N en WhatsApp», con qué cambia, qué queda fijo y qué va a pasar.
- `FRM-Publicando`: cómo termina, arriba del editor, con los pasos Aceptada, Enviada a WhatsApp, WhatsApp la revisa y Lista para mandar. Al costado, la vista oficial con «Borrador | Versión N en WhatsApp».

**Se quedan como están:**
- `FRM-Listado` y `M-FRM-Listado`: «Diseñar formularios», una tabla con Formulario, Preguntas, En WhatsApp y Último cambio. El nombre abre el editor, sin columna de acciones. Tiene el filtro «Estado ▾» y «Nuevo formulario ▾», con «Desde cero» y «Desde una plantilla» (esta, con la E17). La E12b le suma el ciclo de vida;
- `WA-Chats-Formulario` y `M-WA-Chats-Formulario`: «Enviar formulario» al lado de «Enviar» en Internos (en el teléfono, en el ⋮ del chat). **Diseño suma** la misma acción en un chat de Clientes, y en el editor, «Para quién es» (empleados, clientes o los dos). El diálogo «Enviar un formulario a …» tiene:
  - tarjetas con «Versión N · K preguntas», con buscador y paginado si son muchos;
  - el mensaje prellenado, de hasta 1024 caracteres;
  - hasta cuándo se puede mandar;
- `WA-Chats-Respuesta` y `M-WA-Conversacion-Respuesta`: la tarjeta con las primeras 3 respuestas en grilla, «Ver las N respuestas» (en el teléfono, en una hoja) y «Guardada como VIS-000123»;
- `Rol` y la ficha de la organización: los cinco permisos y el módulo.

**Estados que se dibujan** (cada uno tiene que poder producirse; si no, es un bug):
- **Editor:**
  - guardar, no se guardó (lo escrito se conserva);
  - otra persona guardó, con «Qué cambió | Guardado actualmente | Mis cambios»;
  - prefijo ocupado, quitar una pregunta, cambiar el tipo y descartar;
  - solo lectura, cargando y no existe.
- **Publicar:**
  - primera publicación;
  - comprobando, y publicar grisado mientras hay cambios sin guardar;
  - una pregunta que no se puede mostrar, con el botón grisado;
  - cambió después de comprobar y ya se está publicando.
- **Cómo termina:** publicado; rechazada con «Ir a la pregunta»; sin confirmar con «Consultar ahora» (nunca «Reintentar»); necesita revisión.
- **Enviar:**
  - En cola, Leído, y No se pudo mandar (con ⓘ, sin reintento);
  - Envío sin confirmar con «Ya lo revisé», que pide confirmación;
  - con el diálogo abierto, ventana cerrada y formulario que ya no está.
- **Respuesta:**
  - guardando, no se pudo guardar, no reconocida y sin permiso;
  - el mensaje ya no está;
  - una respuesta de «elegir varias»;
  - venció sin respuesta.

**Mandan sobre esta spec en:**
- **Textos:** el editor se llama «Diseñar formularios», y la acción del chat es «Enviar formulario».
- **Botones (43):** «Guardar cambios» se habilita cuando hay cambios, sin «Cambios sin guardar». Mientras trabaja, queda grisado con su mismo texto. Si falta un dato obligatorio, el botón sigue habilitado y al tocarlo se marca el campo.
- **Si no se sabe si algo salió (43):** ante un corte de red al guardar o al mandar, la pantalla vuelve a leer sola y muestra lo que quedó, sin pisar lo escrito:
  - **al guardar:** relee el borrador y compara **solo el contenido**, sin las identidades que emite el servidor. Si es igual a lo mandado, quedó guardado; si no, Guardar sigue habilitado con lo escrito;
  - **al mandar:** relee el chat. Si la sesión existe, se ve el envío; si no, se puede mandar de nuevo con la misma `Idempotency-Key`, que devuelve el resultado guardado si el primero sí llegó.
- **Envío pendiente:** un envío pendiente **o sin confirmar** bloquea el chat (`WhatsApp.Chat.SendPending`). No bloquea que haya otro formulario sin responder.
- **Envío sin confirmar:** lo resuelve una persona con «Ya lo revisé»: «salió» o «no salió», atribuido y durable.
- **«Cambió después de comprobar»** es el 409 de versión al publicar (T8).
- **«Necesita revisión»:** `FormPublication` tiene el estado `NeedsReview`. Es para cuando Meta no confirmó la creación de un Flow y la consulta no puede saber si existe (un Create sin id no se adopta por nombre). Una persona con `forms.publish` lo mira en Meta y lo resuelve.
- **«No reconocida»:** una respuesta de Flow que no corresponde a ninguna sesión se muestra en el chat como un mensaje sin valores y no crea nada en Forms.
- **La banda de conflicto** («Otra persona cambió esto mientras lo editabas.») lleva ⚠.
- **«Venció sin respuesta»:** se calcula al leer: la sesión sigue `Pending` y ya pasó `ExpiresAtUtc`. `Expired` queda para una respuesta que llegó tarde.

## 10. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | identidades; tipo cambiado = identidad nueva; de «elegir una» a «elegir varias» = identidad nueva, y cambiar el rango la conserva; prefijo (formato, bloqueo); transiciones de `FormPublication`; numeración |
| `tests/…Application.UnitTests/Modules/Forms/` | validadores (rango de «elegir varias»); reglas de publicar (textos obligatorios, «nada para publicar»); guardar sin cambios no escribe; armado del Flow (el `CheckboxGroup`, sin `data` cuando no hay nada que mandar, los textos del sistema en el idioma de quien recibe) y verificador de respuestas (cada tipo, punto y coma, decimales de más, «125.500» con 0 decimales, obligatoria vacía, opción ajena, ids repetidos o fuera del rango en «elegir varias», presupuesto de 16 KiB) |
| `tests/…Api.IntegrationTests/Modules/Forms/` | cada ruta: 401, 403, feature apagada = 404, validación, 409, idempotencia y paginado; publicar con un canal de prueba (Published, Uncertain → verifica, Failed); captura idempotente y número sin saltos con dos capturas a la vez; «elegir varias» con una fila por opción y su posición; tarjeta con y sin `formrecords.read`; baja de cuenta; ejemplos exportados de cada respuesta |
| `tests/…Api.IntegrationTests/Modules/Forms/*IsolationTests.cs` | otra organización recibe 404 en cada ruta; RLS de cada tabla; la captura no cruza organizaciones (modelo: `Modules/Assistant/AssistantConversationIsolationTests.cs`) |
| `tests/…Api.IntegrationTests/Modules/WhatsApp/` **[E11]** | botón y motivos; sesión vencida por la hora de Meta; un mensaje con error no frena a los demás; respuesta de más de 16 KiB rechazada y visible |
| Guardas (partials del módulo) | `ExplicitRouteInventoryTests`, `ModuleControllersTests`, `ApplicationServicesTests`, `TenantScopeUsageTests`, `ResourceParityTests` (sumar los resx de Forms), `PermissionTextsTests` y `ModuleIsolationTests` en verde; `TransactionBoundaryTests` sin ampliar excepciones, y la guarda de capas de baja como `AssistantMaintenanceLayerTests` |
| Front | Vitest con MSW y axe en las páginas y el diálogo; los ejemplos exportados validados con zod estricto; `registration`, `navigation` y `composition` del módulo; paridad de sus recursos `forms` y `enums` compuestos desde `registration.ts`, también al retirar Forms; los botones según la decisión 43 |
| E2E real y recorrido | crear con los siete tipos, publicar contra el canal real o el de prueba, mandar a una cuenta en inglés (el formulario llega como está escrito y los textos del sistema, en inglés); responder y ver el número tras recargar |
| Prueba con Meta | con el número de una organización de prueba: §15 |
| Retiro | una vez, con la base vacía: quitar `Forms` (guía nueva, `docs/guides/quitar-formularios.md`, copiando `quitar-asistente.md`) y que el núcleo y WhatsApp sigan en verde |

## 11. Premisas aplicadas

Rige la tabla de la [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#premisas-del-repo-aplicadas-a-formularios). Lo propio de la E12:

- **Servicios:** cada servicio tiene hasta 8 dependencias de negocio, y solo los servicios usan `IUnitOfWork`. Los helpers no abren transacciones.
- **Lo externo, fuera de la transacción:** nada de Meta adentro de una UoW. El estado `InFlight` se commitea antes de cada llamada.
- **Logs:**
  - `[LoggerMessage]`, con códigos y ids;
  - nunca el `flow_token`, los valores de una respuesta, el texto del mensaje ni teléfonos completos.
- **Textos:**
  - lo que ve una persona en WhatsApp por el sistema (el botón «Abrir formulario», la ayuda de los números y «Elegí hasta N») sale de resources es/en del módulo WhatsApp, en el idioma de su cuenta;
  - los textos del formulario son de la organización, un solo texto sin idioma (35).
- **Fechas:** `SubmittedAtUtc` y `ExpiresAtUtc` con `TimeProvider`, comparados con la hora firmada de Meta.
- **Sin datos de ejemplo:** no hay formularios sembrados.

## 12. Puntos de contacto con otras etapas

Al planificar la E12 se verifican contra el código real.

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E11] | El modelo de números: `WhatsAppChannels`, la WABA de cada número y el token por organización. | Un Flow por versión y WABA. Se publica en todas las WABAs con números conectados. |
| [E11] | Conversaciones y mensajes: ids, tipos de mensaje y el contrato de la lista de mensajes. | Se suman un mensaje saliente «Formulario» y la tarjeta `form`. |
| [E11] | Reconocer al empleado y la bandeja Internos; «tomar» un chat. | Mandar cuenta como responder: toma el chat. |
| [E11] | La entrada de mensajes por organización. | El worker de respuestas de Flow sigue el patrón de entrada de la E11. |
| [E11] | El outbox con el número de la organización. | `WhatsAppOutboundMessage` suma la variante Flow con el `PhoneNumberId` de la organización. |
| [E11] | Las rutas de WhatsApp (`/api/organization/whatsapp/...`) y el módulo de WhatsApp del front. | Los nombres de esta spec son provisorios. |
| [E9] | Las cuotas compartidas ([cuotas](../../rules/cuotas.md)). | Las rutas de Forms cuentan en las cuotas generales; no se definen perfiles propios. |
| [E9] | Redis. | La E12 no cachea nada nuevo. |
| [E10] | La nube de `IFileStore`. | La E12 no guarda archivos. |
| #51 | `Enter` borra `TenantKind`. | Se usa `ExecutionActor` (T5). |

## 13. Decisiones de producto de la etapa

Están en la §5 de la hoja de ruta y no se vuelven a preguntar: 9 (la respuesta guarda todas las empresas activas de quien respondió), 10 (manda cualquiera con «Atender internos», y a un cliente, con «Atender clientes»), 49 y 50 (los clientes completan los formularios que son para clientes), 35 (el contenido no tiene idioma), 38 («elegir varias» va acá) y 43 (botones y avisos).

## 14. Referencia de Meta (tomada de ArquitecturaBase)

Es lo único que se toma de ArquitecturaBase: lo que dice Meta y lo que ya se pagó. Los archivos están en `src/ArquitecturaBase.*/Modules/WhatsApp/` de `origin/main`. **Antes de programar, se verifica contra la documentación vigente de Meta.**

| Tema | Lo que se sabe |
| --- | --- |
| Perfil | `flow-static-7.3-r9`: Flow JSON `7.3`, Graph `v25.0` (configuración del módulo), sin endpoint ni `data_api_version`. Una pantalla `FORM` terminal: `SingleColumnLayout` > `Form` > controles + `Footer` con `on-click-action: complete` y un payload `field_<GuidN>: ${form.field_<GuidN>}`. Los textos del sistema son propiedades dinámicas de `data` (T6). |
| Componentes | Texto corto: `TextInput` con `max-chars` (80 por defecto). Texto largo: `TextArea` con `max-length` (600). Número: `TextInput` de texto con el patrón `^-?(0|[1-9][0-9]*)([.,][0-9]+)?$`, de hasta 60 caracteres. Fecha: `CalendarPicker` single. Sí/no: `RadioButtonsGroup` con ids `true` y `false`. Elegir una: `Dropdown` con el `ChoiceId` como id. Elegir varias: `CheckboxGroup` de 1 a 20 opciones, con `min-selected-items` y `max-selected-items` sellados; sin ayuda, dice «Elegí hasta N». `max-chars` y `max-length` van como **números** JSON (Meta rechazó la versión que los mandaba como texto). |
| Límites | Etiqueta: 20 caracteres (30 en sí/no y en elegir varias, 40 en fecha). Ayuda: 80 (300 en la descripción de sí/no y en elegir varias). Opción: 30. Título: 80. Botón: 35. Hasta 40 preguntas, 50 opciones y 50 componentes; JSON ≤1 MiB. Se cuentan en UTF-16. Sin recortar: si no entra, se informa con su `FieldId` y el texto. |
| Presupuesto de respuesta | 16 KiB. El peor caso es 785 + la suma de (42 + los bytes del valor): texto 2 + 6 × largo, número 62, fecha 12, sí/no 7, opción 38, elegir varias 38 por opción marcable. Si se pasa, no se publica (`ResponseTooLarge` sobre los largos de texto). |
| Publicar | (1) Comprobar que el número sea de la WABA: `GET {waba}/phone_numbers`. (2) `POST {waba}/flows` con `{name, categories:["OTHER"]}`. (3) `POST {flow}/assets` multipart con `asset_type=FLOW_JSON`; los `validation_errors[].pointers[].path` se mapean a la pregunta y se descarta el texto de Meta. (4) `POST {flow}/publish`. (5) Verificar: `GET` del Flow y de la salud del número, bajar el asset (solo de `*.fbcdn.net` o `mmg.whatsapp.net`, sin redirecciones, ≤1 MiB) y comparar el SHA-256. Nombre técnico del Flow: `form_<publicationId>_<wabaId>`. |
| Vista previa | `GET {flow}?fields=preview.invalidate(false)`. La URL tiene que ser de `business.facebook.com` y trae `expires_at`. |
| Errores | **Incierto** (no se repite solo, se verifica): 5xx, 429, timeout, cuerpo ilegible, Create 2xx sin id. **Definitivo:** otros 4xx y 2xx con `validation_errors`. Lo que falla antes de llamar a Meta termina en fallida, no en «Sin confirmar». **Sin medio de pago** (141006): el Flow aparece disponible pero la salud total queda BLOCKED; acá se muestra como `WhatsApp.Flow.AccountUnavailable`. |
| Mensaje | `type: interactive`, `interactive.type: flow`, cuerpo ≤1024, `action.name: flow`, `parameters: {flow_message_version: "3", flow_id, flow_token (≤128), flow_cta (≤20, «Abrir formulario»), flow_action: navigate, flow_action_payload: {screen: "FORM", data?}}`. `data` lleva solo los textos del sistema que el Flow usa; si no hay ninguno, no se manda (T14). |
| Respuesta | `messages[].interactive.type == "nfm_reply"`, `nfm_reply.name == "flow"`, `response_json` (texto) con `flow_token` y `field_<GuidN>`. Profundidad ≤4, ≤50 campos; «elegir varias» llega como array de ids. Una respuesta de más de 16 KiB se guarda como rechazada, visible, y no se descarta en silencio. |
| Valores | Texto: hasta el largo y sin NUL. Número: punto o coma, sin exponente ni separador de miles, sin redondear; se recortan los ceros a la derecha. Fecha: `yyyy-MM-dd`. Sí/no: solo `true` o `false`. Opción: un `ChoiceId` de la versión sellada. Elegir varias: ids distintos de la versión, en el orden recibido. Vacío: si es obligatoria, error; si no, ausente. Veinte selecciones no consumen el tope de diez archivos. |
| Lecciones | <ol><li>commitear `InFlight` antes de cada POST, y ante una duda verificar, no repetir;</li><li>un Create sin id confirmado nunca se adopta por nombre;</li><li>usar la hora firmada de Meta para vencer, y aislar los errores mensaje por mensaje;</li><li>el token no forma parte de la identidad de la cuenta (WABA + número): rotarlo no rompe nada;</li><li>no hashear arreglos sin orden fijo;</li><li>en el teléfono hay que probar: ausente contra `false`, el largo máximo de texto y la sesión de una versión anterior. Entran en la prueba con Meta.</li></ol> |
| No se toma | El número y la WABA globales; los permisos planos (`formtemplates.*`); el contacto atado 1:1 a un usuario con `LinkRevision`; el bloqueo global `forms:definitions` y el prefijo global; un Flow por idioma; los modos Manual, Bot y Asistente; la pausa de 6 s por destinatario en Redis; los recibos de borrador de 30 días; los perfiles anteriores a r9. |

## 15. Cómo se cierra

La puerta de la etapa ([etapa en paralelo](../../guides/etapa-en-paralelo.md)) tiene las suites, el E2E real, la documentación en 0, la galería, el recorrido y la lista de control de `/puerta`. Además, hay una **prueba real con Meta** con el número de una organización de prueba, abriendo cada variante en un celular:
1. **Formulario de prueba:** con los siete tipos de pregunta, entre ellos «elegir varias» con mínimo 1 y máximo 3. Meta confirma `PUBLISHED`, con un solo Flow.
2. **Mandar a una cuenta en inglés:** el formulario llega como está escrito, y el botón, la ayuda de los números y «Elegí hasta N» llegan en inglés. Se abre en Android y en iPhone.
3. **Completar:** dejar vacía una opcional, poner `12.50` en el número y marcar dos opciones. En el chat aparece el número de la respuesta tras recargar, y las dos opciones quedan en su orden.
4. **Mirar lo que llega de Meta:** qué manda para una opcional vacía y para «elegir varias» sin marcar, y que `12.50` y `12,50` se guardan iguales.
5. **Una WABA sin medio de pago** (si se puede probar): queda `AccountUnavailable` visible, no «Sin confirmar» para siempre.

## 16. Criterios

Salen de los reviews y los pilotos de ArquitecturaBase. El plan los toma como lista de control, junto con la de `/puerta`.

- **Captura:**
  - una falla técnica se reintenta hasta **5 veces y al menos 15 minutos** desde la primera, con intervalos 30, 90, 210, 450 y 930 s; recién ahí queda `Rejected` y visible;
  - un mensaje trabado no frena a los demás.
- **Estados inciertos:**
  - un error definitivo de Meta termina en «No se pudo», visible;
  - «Sin confirmar» se **consulta** y, si sigue incierto, una persona dice «salió» o «no salió»; nunca se ofrece «Reintentar» sobre lo incierto;
  - un envío del chat que quedó incierto sigue la misma regla **[E11]**.
- **Contratos:**
  - cada campo nuevo de un DTO se exporta, con su ejemplo de una corrida real, y el front lo consume con zod estricto en el mismo corte;
  - las rutas de error tienen la misma forma en los dos lados;
  - `""` no es `null`: lo que falta va como `null`, y una propiedad vacía se omite en lo que va a Meta;
  - una entrada inválida es 400, nunca 500;
  - un campo omitido no apaga nada.
- **Guardado:** ante un error de red o un 5xx, el editor relee solo y reintenta con la misma versión; solo el 409 de versión pide revisar. Guardar sin cambios no escribe.
- **Pantalla:**
  - **errores:** un solo dueño por error (aviso común o junto al campo, nunca los dos), y «Reintentar» solo ante errores de red;
  - **navegación:** no desmonta el editor; se conserva el contenido con `aria-busy` y el foco nunca queda en `body`;
  - **permisos:** un 403 en una acción no oculta la pantalla de lectura.
- **Privacidad:**
  - `ToString` redactado en todo modelo con valores o nombres;
  - sin `formrecords.read`, ningún valor en avisos, vistas previas, `aria-label` ni caché;
  - lo sellado (payloads cifrados) entra en la retención.
- **Meta:**
  - el perfil se versiona, y lo sellado no se recompila;
  - los IDs de pantalla son solo letras y guiones bajos;
  - 141006 cuando la WABA no tiene medio de pago;
  - 131030 con el 9 argentino en un número de prueba.
