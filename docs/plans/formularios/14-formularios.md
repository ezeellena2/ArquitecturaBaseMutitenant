# E14 · Formularios: crear, publicar, mandar y recibir (spec)

**Estado:** spec de la etapa 14 del bloque (decisiones 34 y 78), con las decisiones 35, 38, 43, 79 y 80. Nace con la pregunta «Elegir de un catálogo», sobre los catálogos de la E12 y sus campos de la E13. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E11]**, **[E12]**, **[E9]** y **[Meta]** contra el código real y la documentación vigente.

**Fuentes:**
- alcance y decisiones de producto: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E14 y §5;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- conocimiento de Meta: ArquitecturaBase ([sección 14](#14-referencia-de-meta-tomada-de-arquitecturabase)). Es solo referencia, no código para copiar.

**Requisitos:** la E13 cerrada (con ella, el módulo `Forms`, los catálogos y sus campos), la E11 (números por organización, bandejas, token por organización) y la E9 (Redis, límites y fuzz). Para la prueba con Meta, una organización con su número conectado (la I1 de la 10).

## 1. Alcance

**Entra:**
- **«Formularios»** (79): una sola lista, con «Nuevo formulario». Cada formulario se abre con sus pestañas, y esta etapa trae «Diseño»; lo que ve cada persona depende de sus permisos.
- **Editor:** formularios de la organización con siete tipos de pregunta, entre ellos «elegir varias» (38), prefijo de numeración y vista previa en el teléfono. El contenido es un solo texto por campo, tal como lo escribe la persona, en el idioma que quiera (35).
- **Publicación en WhatsApp:**
  - un Flow por versión y por cuenta de WhatsApp Business (WABA);
  - operación durable, con sus estados visibles y la vista previa oficial de Meta.
- **Envío desde el chat:** «Enviar formulario» en un chat de Internos (a un empleado) o de Clientes (a un cliente), con la sesión de 24 horas. Cada formulario dice para quién es: solo empleados, solo clientes, o los dos (49 y 50). El mensaje sale por la cola de WhatsApp y, si no se sabe si salió, se reintenta solo, como los demás (80).
- **Recepción:**
  - el `nfm_reply` se convierte en una respuesta numerada (`VIS-000123`), sin duplicados;
  - en el chat aparece una tarjeta con los valores, o solo con el número si quien mira no puede ver respuestas.
- **La pregunta «Elegir de un catálogo»** (78): «elegir una» y «elegir varias» toman sus opciones de la pregunta o de un catálogo de la E12 (a mano, de los miembros o de los contactos). Ver la §6b.
  - **la presentación:** la pregunta usa la del catálogo (E13) o tiene la suya, que se publica con su versión;
  - **al publicar:** un catálogo a mano con más de 200 opciones activas en «elegir una», o más de 20 en «elegir varias», no se publica, porque nunca se podría mandar;
  - **al mandar:** las opciones se arman para cada destinatario y viajan dentro del mensaje. No se puede mandar con más de 200 opciones en «elegir una», con más de 20 en «elegir varias», ni sin ninguna si la pregunta es obligatoria;
  - **al responder:** la opción tiene que estar en la oferta, y se sellan el texto, el detalle, el código, el valor y los campos que vio la persona. Si era una opción a mano dada de baja, no se guarda nada, el operador ve el motivo y puede mandar de nuevo;
  - **en los catálogos:** «Lo usan» en el listado y en el catálogo. Dado de baja, un catálogo no se elige en preguntas nuevas, y los formularios publicados que lo usan no se pueden mandar: la confirmación de la baja lo advierte. Un campo que usa la presentación propia de una pregunta no se quita, y el error dice qué formulario y qué pregunta.
- **El módulo** (nace en la E12):
  - los permisos de formularios;
  - los cuatro puertos neutrales de los formularios, con sus implementaciones apagadas;
  - el retiro probado una vez, con los catálogos de la E12 y la E13.

**No entra:**
- dar de baja, reactivar, eliminar y deprecar los Flows (E17);
- los datos de las personas y los filtros por destinatario (E16);
- fotos y documentos (E18);
- las respuestas: pestañas «Respuestas» y «Tablero», planilla, detalle y Excel (E15);
- reglas, aprobación, PDF y avisos (E21);
- biblioteca de plantillas y «Desde una plantilla» (E22);
- el asistente (E23);
- copiar formularios (E17);
- condiciones, cálculos y líneas (E19);
- cargar desde la web (E20);
- límites por plan.

## 2. Decisiones técnicas

Las de producto están en la hoja de ruta (§5). Estas siguen las reglas del repo y, cuando hay más de una forma, la más simple.

| # | Decisión | Por qué |
| --- | --- | --- |
| T1 | `Forms` y `WhatsApp` se hablan solo por puertos neutrales del núcleo: el de contactos de la E12 y cuatro más (§7). | `ModuleIsolationTests` prohíbe que un módulo use tipos de otro. |
| T2 | **«Enviar formulario» es de WhatsApp.** La pantalla del chat, la ruta, la sesión y el mensaje van en el módulo WhatsApp, que lista los formularios con `IStructuredFormSource`. | El que manda es el operador desde el chat. |
| T3 | El borrador usa `IVersioned` y 409 `General.ConcurrencyConflict`, sin recibos de cambios. | La edición es interna (robustez proporcional, §4 de la hoja de ruta). |
| T4 | **Un solo worker de publicación, el de Forms.** Descubre trabajos en `platform.FormPublicationWorks`, entra con `ExecutionActor` y llama a las fases de `IFormPublicationService`. Fuera de toda UoW llama al canal, que coordina Meta con las fases de `IWhatsAppFlowPublicationService`; cada fase que escribe abre una sola UoW. | Copia la separación entre `AssistantTurnWorker` e `IAssistantTurnService`; ni el worker ni el adaptador neutral reciben `IUnitOfWork`. |
| T5 | **El worker de publicación** entra con `IExecutionActorFactory` e `IExecutionActorScope` (quien publicó), no con `ITenantScope.Enter` solo. **La captura de una respuesta no usa `ExecutionActor`:** un cliente no tiene cuenta ni actor. Va en una fase aparte del procesador de entrada, fuera de su transacción, con el contexto de la organización armado con `SetFromAccess`, como la entrada de la E11; la feature se consulta con ese contexto. A un empleado se le revalidan la membresía y `formrecords.create` dentro de la captura. Un `nfm_reply` no pasa por el bot. | Con `Enter` solo, `IFeatureService` da `false`, porque `Enter` deja `TenantKind` en null (backlog #51); y `ExecutionActorFactory` no puede armar un actor sin cuenta. |
| T6 | **Un Flow por versión y por WABA.** Los dos textos del sistema que van adentro del Flow (la ayuda de los números y «Elegí hasta N») viajan en el `data` del mensaje, en el idioma de la cuenta de quien recibe; un cliente sin cuenta, en el de la organización ([textos](../../rules/textos-y-traducciones.md); 35 y 42). | El contenido no tiene idioma; los textos del sistema sí, y cambian por destinatario sin otro Flow. |
| T7 | **Lo que cambia sin que nadie edite va en otra fila.** `FormDefinitionStates` (una por formulario) guarda la versión activa, el bloqueo del prefijo y `LastRecordNumber`, sumado bajo bloqueo de esa fila al capturar, con índice único (definición, número). La fila del borrador cambia solo al editar. El prefijo es único **dentro de la organización**. | Si el contador o la versión activa vivieran en la fila del borrador, cada respuesta o publicación cambiaría su `xmin` y quien edita recibiría un 409 falso. Sin bloqueos globales: cada organización numera aparte. |
| T8 | **Sin ticket de comprobación:** `publication-checks` solo informa y devuelve el `contentHash` del borrador que comprobó; `publications` lo recibe, vuelve a comprobar todo adentro de su transacción y, si el borrador cambió, responde `Forms.Publication.DraftChanged`. Ninguna de las dos lleva `version`, y publicar no escribe la fila del borrador. | La comprobación final es la que vale. `VersionedContractTests` exige `VersionResponse` a todo pedido con `version`. |
| T9 | **«Número con decimales»:** cada pregunta fija sus decimales (de 0 a 6) y admite hasta 15 dígitos enteros. Al responder se acepta punto o coma; más decimales que los permitidos se rechaza. | Evita que «125.500» se lea como 125,5: con 0 decimales, se rechaza. |
| T10 | **Lo que recibe la persona:** el formulario sale como está escrito. El mensaje por defecto, el botón «Abrir formulario» y los textos del sistema del Flow van en el idioma de su cuenta, y si es un cliente sin cuenta, en el de la organización (35 y 42). La sesión guarda ese idioma. | Un formulario se ve y se manda igual con el menú en cualquier idioma. |
| T11 | Los valores viven en filas tipadas (`FormRecordValues`), sin un JSON paralelo. «Elegir varias» guarda una fila por opción, con su posición. | Una sola fuente, que se puede filtrar por columna. |
| T12 | **Las capturas rechazadas no crean nada en Forms.** El rechazo queda en la sesión de WhatsApp, con su código. | La idempotencia la da el índice único `SourceKey`. |
| T13 | **«Elegir varias» es un tipo propio** (`MultiChoice`), no una marca de la lista: de 1 a 20 opciones, con `0 ≤ mínimo ≤ máximo ≤ 20` y máximo mayor que cero. Es obligatoria si y solo si el mínimo es mayor que cero. Pasar de «elegir una» a «elegir varias» crea otra identidad; cambiar el rango la conserva. Sin ninguna marcada es «Sin respuesta». | Es la regla de ArquitecturaBase (C1-04), con el tope de WhatsApp. |
| T14 | **Un mensaje sin datos no lleva `data`.** Un mapa vacío (`prefill_FORM:{}`) hace que el Flow no abra en Android. | Error ya pagado en el piloto de ArquitecturaBase. |
| T15 | **Quien recibe y quien responde es un empleado o un cliente** (49). La sesión y la respuesta guardan uno de los dos: el usuario del miembro, o el contacto de la bandeja Clientes. A un empleado se le revisan la membresía y `formrecords.create`; a un cliente, que el formulario sea para clientes. | Un cliente no tiene cuenta ni permisos: se lo reconoce por el contacto, que es su número. |
| T16 | **La respuesta de un cliente es dato de la organización** (`ITenantOwned`). Si el contacto vinculó su cuenta personal, ve la copia por la copia de la conversación de la E11, que ya es compartida: Forms no suma tablas compartidas. **En «Mis mensajes» ve la tarjeta con sus propias respuestas:** al capturar, WhatsApp guarda en esa copia la tarjeta armada (el nombre del formulario, el número y los valores que mandó), porque el acceso personal no lee `tenant` ni tiene `formrecords.read`. Si el contacto se borra, la respuesta queda (decisión 23) sin el vínculo ni el nombre. Si el cliente da de baja su cuenta vinculada, la respuesta queda con el contacto, y solo desaparece la copia de «Mis mensajes». | La regla de datos personales y la D4 de la E11. |
| T17 | Una pregunta de elegir toma sus opciones de la pregunta o de un catálogo (`catalogId`). Cambiar el catálogo, o pasar de opciones propias a un catálogo, en una pregunta publicada crea una pregunta nueva, como cambiar el tipo. | Cambiar opciones no obliga a republicar, y las respuestas viejas no cambian de sentido. |
| T18 | **Los ids que viajan a Meta son locales a la oferta** (`o1` … `o200`). La sesión guarda a qué ítem corresponde cada uno. También en «elegir varias». | No se mandan a Meta ids internos ni de personas, y «estar en la oferta» se comprueba solo. |
| T19 | **La oferta la arma Forms y la guarda WhatsApp,** cifrada en la sesión (`ProtectedOffer`, texto que devuelve `IPayloadProtector`). Se borra con la retención, una vez procesada. | Forms conoce los catálogos y WhatsApp conoce la sesión. Como contiene nombres de personas, va cifrada. |
| T20 | Para un catálogo a mano, la captura comprueba que la opción siga activa; si no, `Forms.Record.OptionWithdrawn`. Para uno con filtros alcanza con que esté en la oferta. | Con filtros, lo que vio la persona es lo que vale. |
| T21 | El Flow usa un perfil con datos iniciales. Cada pregunta de catálogo es un `Dropdown` o un `CheckboxGroup` con `data-source: ${data.field_<N>}`, y la pantalla declara su `data` con un `__example__` **[Meta]**. | Permite mandar opciones distintas a cada persona sin un endpoint de intercambio. |
| T22 | **La presentación** arma el título y la descripción de cada opción al mandar: la del catálogo (E13) o la propia de la pregunta, que se guarda en el borrador y se sella con la versión. Lo que queda vacío o no entra frena la comprobación de publicar y el envío (36). Un campo que usa la presentación de una pregunta del borrador o de la versión activa no se quita (`Forms.Catalog.FieldInUse`). La captura lee los campos activos de las opciones elegidas, por lote y bajo sus bloqueos, y sella identidad, clave, nombre, tipo y valor. La tarjeta del chat no cambia. | Editar el catálogo después no cambia lo respondido. |
| T23 | **Un catálogo que nunca se podría mandar no se publica:** uno a mano con más de 200 opciones activas en «elegir una», o más de 20 en «elegir varias» (`Forms.Draft.CatalogTooLarge`). Los de miembros y contactos se comprueban al mandar, porque dependen de quien recibe y cambian solos. | Sin esto, un catálogo de 10.000 opciones se publicaba y nunca se podía mandar. |

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
- cada opción lleva `{choiceId, label}`. Una pregunta con `catalogId` no tiene opciones propias.

**FormVersions** (inmutable)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `TenantId`, `DefinitionId` | uuid | FK compuesta |
| `Number` | int | único por definición |
| `Name`, `Audience`, `Questions`, `SubmitLabel` | texto y jsonb | copia sellada |
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
| `RespondentContactId` | uuid? | el cliente que respondió: el id de su `WhatsAppCustomer` de la E11 (el contacto de la organización, no el `WhatsAppContact` técnico global); `null` si el contacto se borró. Un CHECK exige uno de los dos al crear |
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
| `ProtectedOffer`, `OfferHash` | texto?, texto? | la oferta de las preguntas de catálogo, cifrada con `IPayloadProtector` (T19): `[{fieldId, options: [{localId, catalogId, itemId, text, detail?, code?, value?}]}]` |

**Lo que se suma a las tablas de la E11 [E11]:**
- **mensaje saliente:** un tipo «Formulario» que apunta a la sesión;
- **mensaje entrante:** `ProtectedFlowResponse`, cifrado con `IPayloadProtector` y de hasta 24 KiB. Se borra con la retención de WhatsApp, solo cuando ya se procesó.

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
| `Forms.Publication.DraftChanged` | Conflict | el borrador cambió después de comprobar (T8) |
| `Forms.Publication.ChannelUnavailable` | ServiceUnavailable | sin WhatsApp, o sin número conectado [E11] |
| `Forms.Publication.NotFound` | NotFound | — |
| `Forms.Preview.NotAvailable` | NotFound | la versión no tiene un Flow publicado |
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
| `WhatsApp.Flow.TextTooLong` / `TooManyComponents` / `ResponseTooLarge` | Validation | límites del perfil (§14) |
| `WhatsApp.Flow.ProviderRejected` / `ValidationFailed` | Failure | rechazo definitivo de Meta |
| `WhatsApp.Flow.AccountUnavailable` | ServiceUnavailable | la WABA sin medio de pago o sin salud [E11] |
| `WhatsApp.Flow.NotPublishedForNumber` | Conflict | el número del chat está en una WABA sin Flow de esa versión |
| `WhatsApp.Flow.SessionExpired` | Conflict | la respuesta llegó pasada la sesión (hora de Meta) |
| `WhatsApp.Chat.WindowClosed` / `NotEmployee` / `FormUnavailable` / `SendPending` | Conflict | reglas del botón (§6.6) |

Los códigos siguen `^[A-Z][A-Za-z]+(\.[A-Z][A-Za-z0-9]+){2,}$` (`ErrorCodeTests`). El 409 de edición es el común, `General.ConcurrencyConflict`, y es el único que pone el editor en conflicto: los otros 409 son errores que se corrigen.

## 5. Contrato borrador

Las rutas Business de Forms llevan `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Las del chat son de WhatsApp, van bajo el prefijo de su bandeja **[E11]** y no llevan la feature de formularios: con `forms` apagada, `IStructuredFormSource` no devuelve formularios y el botón no aparece. Las escrituras que crean o envían llevan `[Idempotent]`. Los listados, `PagedRequest` (10 por defecto) y `filter-counts`. Toda respuesta que consume el front tiene ejemplos exportados de una corrida real, con sus estados y sus errores de negocio. El modelo es `Api/Modules/Assistant/Controllers/BusinessAssistantController.cs`.

| Método y ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| GET `/api/organization/forms` | `forms.read` | `page`, `pageSize`, `sort` (`name`, `updatedAtUtc`), `search`, `status` (`all`, `published`, `drafts`) | 200 `PagedResult<FormSummary>`: `{id, name, prefix, audience, status, activeVersionNumber?, publishedAtUtc?, publicationState?}`; es la lista «Formularios» (79) |
| GET `/api/organization/forms/filter-counts` | `forms.read` | `search` | 200 `{all, published, drafts}` |
| POST `/api/organization/forms` | `forms.manage` | `{name}` | 201 `FormDetail` + Location, con el prefijo propuesto |
| GET `/api/organization/forms/{id}` | `forms.read` | — | 200 `FormDetail`: `{id, name, prefix, prefixLocked, audience, submitLabel, questions[], activeVersion?{id, number, publishedAtUtc}, publication?{id, state, errorCode?, issues?}, canManage, canPublish, version}` |
| PUT `/api/organization/forms/{id}/draft` | `forms.manage` | `{name, prefix, audience, submitLabel, questions[], version}` | 200 `{version, questions[]}`, con las identidades emitidas; 409 |
| POST `/api/organization/forms/{id}/publication-checks` | `forms.publish` | — | 200 `{contentHash, canPublish, nextVersionNumber, issues[], changes[]}`; no escribe (T8) |
| POST `/api/organization/forms/{id}/publications` | `forms.publish` | `{contentHash}` | 202 `{publicationId, state}` + Location; no llama a Meta; `DraftChanged` si el borrador cambió (T8) |
| GET `/api/organization/forms/{id}/publications/{publicationId}` | `forms.read` | — | 200 `{state, errorCode?, issues?, versionNumber}` |
| GET `/api/organization/forms/{id}/versions/{versionId}/preview` | `forms.read` | — | 200 `{url, expiresAtUtc}`, con `Cache-Control: no-store` |
| GET `/api/organization/whatsapp/{customers\|internal}/conversations/{id}/forms` **[E11]** | «Atender internos» o «Atender clientes», según la bandeja | `page`, `search` | 200 `{available, reason?, culture, forms: PagedResult<{definitionId, versionId, name, prefix}>}`; `culture` es el idioma de los textos del sistema que acompañan |
| POST `/api/organization/whatsapp/{customers\|internal}/conversations/{id}/forms` **[E11]** | «Atender internos» o «Atender clientes», según la bandeja | `{definitionId, versionId, text}` (≤1024) | 202 `{sessionId, messageId}` |

**Otras piezas del contrato:**
- **Tarjeta de respuesta [E11]:** los mensajes de la conversación suman un tipo `form`: `{sessionId, state, errorCode?, formName, displayNumber?, values?: [{fieldId, label, type, value, code?}], canReadValues}`. En «elegir varias», `value` es la lista de opciones en el orden recibido. El valor de catálogo muestra el texto que vio la persona y su código. Si la respuesta se rechazó con `OptionWithdrawn`, nombra la pregunta, no la opción.
- **Webhook:** la misma ruta de la E8. Se agrega leer `interactive.nfm_reply`.

**La pregunta de catálogo en las rutas:**
- **`FormDetail` y `PUT …/draft`:** las preguntas `Choice` y `MultiChoice` llevan `catalogId?`. El selector de catálogo de la pregunta usa el listado de la E12, con `status=active`, y pide `formcatalogs.read`; sin ese permiso, «De un catálogo» queda grisado, con el motivo en su tooltip (43).
- **`FormDetail` y `PUT …/draft`:** la pregunta lleva también su `presentation?` propia (T22).
- **`publication-checks`:** suma `Forms.Draft.CatalogUnavailable`, `CatalogTooLarge` y `CatalogPresentationInvalid`.
- **`GET …/conversations/{id}/forms`:** cada formulario trae `offerProblems[{fieldId, code}]` (`TooManyOptions`, `TooFewOptions`, `NoOptions`, `CatalogUnavailable` y `PresentationInvalid`) y la cantidad de opciones de cada pregunta de catálogo, nunca sus nombres. Si hay algún problema, el formulario aparece pero no se puede mandar.
- **Las rutas de catálogos de la E12:** el listado y el detalle suman `usedByCount` (`null` sin `forms.read`), y se suma GET `/api/organization/forms/catalogs/{id}/used-by` (`formcatalogs.read`; `page`, `pageSize`, `search`, `sort` (`name`) → 200 `PagedResult<{formId, name}>`), con su segunda página en las pruebas de integración.

**Contrato y errores en OpenAPI:** se suman al `FormsContractContributor.cs` de la E12.

## 6. Flujos, con sus transacciones

«UoW» es una `ExecuteInTransactionAsync(…, CommitPolicy.OnSuccess, ct)`. Cada método público de servicio va en `OperationLog.RunAsync`. Las escrituras de definición pertenecen a `IFormDefinitionService`; las de publicación, a `IFormPublicationService`. Los workers coordinan las fases, y los adaptadores neutrales delegan en los servicios del módulo; ninguno abre una UoW ni recibe `IUnitOfWork`.

1. **Crear.**
   - Afuera de la UoW se valida el nombre.
   - En la UoW: se propone el prefijo libre, se crea la definición con el borrador vacío y se responde 201.
2. **Guardar el borrador.**
   - Afuera: se validan el pedido y las reglas de las preguntas.
   - En la UoW:
     - se carga la definición con la versión esperada;
     - si el contenido no cambió, se escribe solo `ModifiedAtUtc`, para comparar la versión ([concurrencia](../../rules/concurrencia.md));
     - si el prefijo cambió, se comprueba que no esté bloqueado y que esté libre (el choque del índice se traduce a `PrefixTaken`);
     - se emiten las identidades nuevas y se guarda.
   - Responde 200 `{version, questions}`.
3. **Comprobar.** Es solo lectura:
   - reglas de Forms y textos obligatorios;
   - `IStructuredFormChannel.Check(StructuredFormVersion)`, síncrono y puro: sin escrituras, sin bloqueos y sin Meta;
   - la diferencia con la versión activa.
4. **Publicar.**
   - **En la UoW del pedido:**
     - bloqueo de la fila de estado de la definición (T7);
     - el borrador tiene el `contentHash` comprobado; si no, `DraftChanged` (T8);
     - se comprueba otra vez todo lo del punto 3;
     - que no haya otra publicación abierta;
     - se sella la `FormVersion` (número = última + 1);
     - se crean `FormPublication` (`Accepted`) y `FormPublicationWorks`;
     - se bloquea el prefijo en la fila de estado; la fila del borrador no se escribe.
   - Responde 202.
   - **El worker** (`Infrastructure/Modules/Forms/FormPublicationWorker.cs`, modelo `AssistantTurnWorker`) hace tres pasos:
     1. busca trabajos listos y entra con `ExecutionActor` (el operador que publicó, revalidado: si perdió `forms.publish` antes de tocar Meta, la publicación termina `Failed`). Llama a `IFormPublicationService.ClaimAsync`, que reserva en su única UoW y pasa a `Preparing`;
     2. fuera de toda UoW llama a `IStructuredFormChannel.PublishAsync`:
        - el canal arma el Flow de la versión para cada WABA con números conectados **[E11]**;
        - por cada uno sigue los pasos Create → Upload → Validate → Publish → Verify;
        - el adaptador `WhatsAppStructuredFormChannel` llama a `IWhatsAppFlowPublicationService.BeginStepAsync` para reservar el binding/paso y guardar `InFlight` en su única UoW; hace la llamada a Meta después del commit y entrega el resultado a `CompleteStepAsync`, que abre otra UoW. El adaptador no abre transacciones;
     3. entrega lo que devolvió el canal a `IFormPublicationService.CompleteAsync`, que en su única UoW aplica:
        - `Published` → `ActivationPending`, y se activa la versión en la fila de estado, en la misma UoW → `Published`;
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
     - no hay otro envío pendiente;
     - `IStructuredFormSource.ListSendableAsync(recipient)` devuelve al menos un formulario. Forms filtra los publicados para ese tipo de destinatario (50) y, a un empleado, los que puede responder (`formrecords.create`).
   - **Si no se puede usar:** el botón y la opción del ⋮ quedan grisados, con el motivo en su tooltip (43).
   - **Mandar:**
     - afuera de la UoW se valida el texto (≤1024) y se lee el formulario elegido con `FindSendableAsync`. El idioma de los textos del sistema es el de la cuenta de quien recibe (T10);
     - en la UoW: bloqueo de la conversación, se revalidan ventana, empleado y envío pendiente, se toma el chat si no estaba tomado (regla de la E11), se crea la sesión (token aleatorio de 32 bytes; se guarda solo el hash) y se encola el mensaje interactivo de tipo Flow por el outbox (`WhatsAppOutboundMessage` con su variante nueva);
     - el outbox lo manda y, si no se sabe si salió, lo reintenta solo, como a los demás mensajes; no hay un estado «sin confirmar» (80).
   - **Si la WABA del número no tiene Flow de esa versión:** `WhatsApp.Flow.NotPublishedForNumber`.
7. **Recibir y capturar** (worker de WhatsApp, siguiendo la entrada de la E11 **[E11]**).
   - **Webhook:** guarda el mensaje con `ProtectedFlowResponse`, sin duplicados por `wamid`.
   - **El procesador de entrada,** por cada mensaje, aislando los errores mensaje por mensaje y sin pasar por el bot:
     1. saca el hash del `flow_token` y busca la sesión;
     2. compara la hora firmada de Meta con `ExpiresAtUtc`: si se pasó, `Expired`;
     3. decodifica con el `FieldMap` del binding (§14); los arrays de «elegir varias» van a la captura, nunca a la descarga de medios;
     4. en una fase aparte, fuera de la transacción del procesador y con el contexto de la organización de `SetFromAccess` (T5), llama a `IStructuredRecordSink.CaptureAsync`. Las escrituras previas de reserva o vencimiento son fases de `IWhatsAppFlowReplyService.ClaimAsync`, con una única UoW; el procesador no las abre.
   - **La captura en Forms:** `FormRecordSink` delega en `IFormRecordService.CaptureAsync`, que abre su única UoW:
     - busca el `SourceKey`: si ya existe con el mismo contenido, devuelve el mismo número; si el contenido es otro, `ReplayConflict`;
     - a un empleado le revalida la membresía y `formrecords.create`; a un cliente, que el formulario siga siendo para clientes;
     - valida estricto contra la versión sellada;
     - bloquea la fila de estado de la definición y suma `LastRecordNumber` (T7);
     - guarda la respuesta y sus valores.
   - **Después:** `IWhatsAppFlowReplyService.CompleteAsync` abre su única UoW en WhatsApp y marca la sesión `Completed` (con número) o `Rejected` (con código). Si el contacto vinculó su cuenta, guarda en la copia compartida de la conversación la tarjeta armada para «Mis mensajes» (T16).
   - **Si se cae entre las dos UoW:** se repite y devuelve el mismo número.
8. **Tarjeta en el chat.**
   - Al listar los mensajes, WhatsApp llama a `IStructuredRecordSource.ReadAsync(recordIds, culture)`, con el idioma de quien mira para los textos del sistema («Sí», «No»).
   - Forms decide con los permisos de quien mira: con `formrecords.read` devuelve los valores, y sin él solo el número.
   - En la lista de chats, la vista previa es el texto fijo «Respuesta del formulario».
9. **Retención y baja.**
   - La retención de WhatsApp borra `ProtectedFlowResponse` y `ProtectedOffer` cuando el mensaje ya se procesó; la respuesta y su número quedan (decisión 23).
   - `Application/Modules/Forms/Services/FormsAccountDeletionParticipant` decide las reglas dentro del alcance y la UoW de la baja, sin abrir otra ([datos-personales](../../rules/datos-personales.md)). Copia `Application/Modules/Assistant/Services/AssistantAccountDeletionParticipant.cs`: usa un puerto `IFormsAccountDeletionCleanupRepository`, cuya implementación en Infrastructure hace solo el cleanup EF y exige la transacción del llamador. Lo protege `AssistantMaintenanceLayerTests`, que se extiende para Forms. `ChoiceLabel` puede ser el nombre de otra persona (un catálogo de miembros), y la regla lo contempla.

## 6b. La pregunta de catálogo

Cambia los flujos 2, 3, 6 y 7 para las preguntas con `catalogId`.

1. **Guardar y comprobar:**
   - `PUT …/draft` valida que el catálogo exista y se pueda usar;
   - cambiar el catálogo de una pregunta ya publicada emite un `FieldId` nuevo (T17);
   - la presentación propia de la pregunta se guarda en el borrador; si usa un campo inactivo o inexistente, es `QuestionInvalid` en su pregunta;
   - `IStructuredFormChannel.Check(StructuredFormVersion)` valida el perfil con datos (T21); un catálogo a mano que nunca se podría mandar no se publica (T23), ni una presentación que no entra (T22).
2. **Mandar:**
   - `FindSendableAsync` de Forms resuelve, para quien recibe, la oferta de cada pregunta de catálogo, con la presentación de la pregunta o la del catálogo. Lee los catálogos a mano activos o la fuente con los filtros (los lectores de la E12), con una fila más que el tope; es solo lectura.
   - **Al operador** le llega solo la cantidad de opciones por pregunta y los problemas, nunca los nombres. Las opciones completas quedan en la sesión.
   - Si una pregunta tiene un problema, no se puede mandar.
   - **Nombres largos:** un nombre de las fuentes «Miembros» o «Contactos» de más de 30 caracteres se corta en 29 más «…», y el completo va primero en el detalle (decisiones 11, 21 y 36).
   - **En la UoW de WhatsApp:**
     - se numeran las opciones `o1` a `oN` (T18);
     - se arma `flow_action_payload.data` con `{id: localId, title, description?}`, sin `description` si no hay detalle;
     - se guarda `ProtectedOffer` en la sesión y se encola el mensaje.
3. **Recibir:**
   - el verificador mira que cada `localId` elegido esté en la oferta (si no, `OptionNotOffered`), y lo traduce a `{catalogId, itemId, text, detail, code, value}`;
   - la captura de Forms, en su UoW, comprueba con un catálogo a mano que la opción siga activa, bajo el mismo bloqueo con que se edita: si no, `OptionWithdrawn`, sin respuesta ni número (T20). Sella los campos de la opción (T22);
   - después WhatsApp marca la sesión `Rejected` con ese código, y la tarjeta ofrece «Mandar de nuevo», que abre el mismo diálogo de envío.

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
    Task<PagedResult<StructuredFormChoice>> ListSendableAsync(StructuredRecipient recipient, PagedRequest page, CancellationToken ct);
    Task<StructuredSendableForm?> FindSendableAsync(StructuredRecipient recipient, Guid definitionId, Guid versionId, CancellationToken ct);
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
- `StructuredFormVersion(DefinitionId, VersionId, Number, Prefix, Name, Audience, SubmitLabel, Questions[FieldId, Type, Label, Help, Required, MaxLength?, Decimals?, MinSelections?, MaxSelections?, CatalogId?, Presentation?, Options[ChoiceId, Label]], ContentHash)`;
- `StructuredSendableForm(Version, Offers[FieldId → Options[ItemRef(CatalogId, ItemId), Text, Detail?, Code?, Value?]], Problems[FieldId, Code])`: lo que se manda, con la oferta de cada pregunta de catálogo (§6b);
- `StructuredFormChoice`, de `ListSendableAsync`, suma `HasProblems`;
- `StructuredRecipient(Kind: Member|Contact, UserId?, ContactId?)`: quien recibe (T15). `ContactId` es el id del `WhatsAppCustomer` de la organización;
- `StructuredRecordCapture(SourceKey, DefinitionId, VersionId, Respondent: StructuredRecipient, SubmittedAtUtc, Values[FieldId, Text?|Number?|Date?|Bool?|ChoiceId?|ChoiceIds[]?, CatalogId?, ChoiceCode?, ChoiceDetail?, ChoiceValue?])`;
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

El esqueleto del módulo lo deja la E12: `AddFormsModule` en las tres capas, la feature `forms`, `FormsModelContributor`, `FormsContractContributor`, los recursos, la clase `Forms` de permisos y el registro del front. La E14 le suma sus piezas.

| Archivo nuevo o cambiado | Modelo en este repo |
| --- | --- |
| `D/Forms/FormDefinition.cs`, `FormDefinitionState.cs`, `FormVersion.cs`, `FormPublication.cs`, `FormRecord.cs`, `FormQuestionType.cs`, `FormDraft.cs`, `*Errors.cs` (con `FormOfferErrors.cs`) | `D/Assistant/AssistantOrganizationSettings.cs`, `D/Assistant/AssistantErrors.cs` |
| `D/Authorization/Permissions.cs`: la clase `Forms` de la E12 suma `forms.read`, `forms.manage`, `forms.publish`, `formrecords.create` y `formrecords.read` | la clase `Assistant` del mismo archivo |
| `A/Modules/Forms/Interfaces/Services/IFormDefinitionService.cs`, `IFormPublicationService.cs`, `IFormRecordService.cs` | `IAssistantConversationService.cs`; `IAssistantTurnService.cs` para las fases |
| `A/Modules/Forms/Services/FormDefinitionService.cs`, `FormPublicationService.cs`, `FormRecordService.cs` y los helpers `FormPrefixResolver`, `FormDraftEditor`, `FormVersionCreator`, `FormRecordNumberIssuer`, `FormRecordWriter` y `FormOfferBuilder` (arma la oferta de cada pregunta de catálogo) | `AssistantConversationService.cs`, `AssistantTurnService.cs` y sus helpers |
| `A/Modules/Forms/Services/FormStructuredSource.cs`, `FormRecordSink.cs`, `FormRecordSource.cs` (implementan los puertos) | `A/Modules/WhatsApp/Services/WhatsAppLoginCodeChannel.cs` (un módulo implementa un puerto del núcleo) |
| `A/Modules/Forms/Interfaces/Persistence/IFormDefinitionRepository.cs`, `IFormReader.cs`; suman sus claves a `UniqueKeys.cs` | `A/Modules/Assistant/Interfaces/Persistence/` |
| `A/Modules/Forms/Models/`, `Validation/`; suman sus claves a los recursos de la E12 | las mismas carpetas del Asistente |
| `A/Interfaces/Integrations/StructuredData/*.cs`, `A/Models/StructuredData/*.cs` | `IStructuredContactSource` de la E12 |
| `I/StructuredData/Disabled*.cs` | `DisabledStructuredContactSource` de la E12 |
| `I/Modules/Forms/Persistence/Configurations/*.cs`, `Repositories/*.cs`, `Readers/*.cs` | `I/Modules/Assistant/Persistence/` |
| `A/Modules/Forms/Services/FormsAccountDeletionParticipant.cs`, `A/Modules/Forms/Interfaces/Persistence/IFormsAccountDeletionCleanupRepository.cs`; `I/Modules/Forms/Persistence/Repositories/FormsAccountDeletionCleanupRepository.cs` | `A/Modules/Assistant/Services/AssistantAccountDeletionParticipant.cs`, su puerto de cleanup y `I/Modules/Assistant/Persistence/Repositories/AssistantAccountDeletionCleanupRepository.cs`; `AssistantMaintenanceLayerTests` |
| `I/Modules/Forms/FormPublicationWorker.cs` | `I/Modules/Assistant/AssistantTurnWorker.cs` (descubre, entra con actor y llama fases del servicio) |
| `I/Persistence/Migrations/<fecha>_FormDefinitions.cs` y `<fecha>_SystemRoleFormsPermissions.cs` | `20261010011328_AssistantModule.cs`, `20261010203618_SystemRoleAssistantPermissions.cs` |
| `P/Modules/Forms/Controllers/BusinessFormsController.cs`, `Contracts/*HttpRequest.cs`; `BusinessFormCatalogsController` suma `used-by` | `P/Modules/Assistant/...` |
| WhatsApp: `A/Modules/WhatsApp/Services/WhatsAppFlowBuilder.cs` (con el perfil con datos y el `CheckboxGroup` de catálogo), `WhatsAppFlowReplyVerifier.cs` (con la oferta), `WhatsAppFormSendService.cs` (con `ProtectedOffer`), `WhatsAppStructuredFormChannel.cs`; `I/Modules/WhatsApp/WhatsAppFlowClient.cs`, la fase de captura del procesador de entrada (T5), la copia de la tarjeta para «Mis mensajes» (T16) y las configuraciones de `WhatsAppFlowBindings` y `WhatsAppFlowSessions` | `I/Modules/WhatsApp/WhatsAppCloudClient.cs`, `WhatsAppInboundProcessor.cs` y lo que deje la E11 **[E11]** |
| WhatsApp: `A/Modules/WhatsApp/Interfaces/Services/IWhatsAppFlowPublicationService.cs`, `IWhatsAppFlowReplyService.cs` y sus implementaciones en `Services/` | `IAssistantTurnService.cs` y `AssistantTurnService.cs` (fases con una UoW cada una) |

**Front** (`F/` = `ArquitecturaBaseMutitenantFront/`):

| Archivo nuevo o cambiado | Modelo |
| --- | --- |
| `F/src/modules/forms/registration.ts` de la E12: suma las rutas `/org/formularios` (la lista «Formularios») y `/org/formularios/:id` (el formulario, con su pestaña «Diseño»), con `forms.read`, y «Formularios» en el panel de «Gestión de formularios» (79). Las pestañas del formulario son una pieza nueva del front, a la que cada etapa suma la suya | la misma registration |
| `F/src/modules/forms/{api,components,hooks,pages}/`: la lista, el formulario con sus pestañas, el editor con el selector de catálogo y la presentación en la pregunta, publicar y «Lo usan» en los catálogos. Las preguntas que se abren en el lugar son una excepción a las filas que no se expanden: se escribe en la ficha de tablas del front | `F/src/modules/assistant/` |
| `F/src/modules/forms/locales/{es,en}/forms.json` y `enums.json`: suman las claves, con paridad | los recursos de la E12 |
| WhatsApp **[E11]**: el diálogo «Enviar formulario», con los avisos de la oferta, y la tarjeta de respuesta, con el rechazo y «Mandar de nuevo», en el módulo de WhatsApp del front que deje la E11; en «Mis mensajes», la tarjeta copiada (T16) | — |

**Punteros:** cada carpeta nueva lleva su `AGENTS.md` de una a tres líneas («qué va acá · Leé ficha · Copiá de archivo») y un `CLAUDE.md` con `@AGENTS.md`, más su fila en `docs/architecture/arbol.md`. Se crean con la carpeta, no antes (`HarnessTests`). Al cerrar la etapa, `docs/features/formularios.md` (nacida en la E12) suma la sección de formularios.

## 9. Pantallas (tableros)

**Navegación (34, 78 y 79):** el menú principal dice «Gestión de formularios», y su panel ya tiene «Catálogos» (E12). Esta etapa suma «Formularios»: una sola lista, con «Nuevo formulario», y cada formulario se abre con su pestaña «Diseño». No existe «Diseñar formularios». Los tableros de la etapa muestran solo lo que existe hasta ella.

**Aprobados y publicados en el lienzo v141** (2026-10-25, front `93cb7d01`), con sus teléfonos, sin idioma en el contenido y con los botones de la decisión 43. Reemplazan a los de la v139:
- **`FRM-Formularios`:** la lista «Formularios» (79), una tabla con Formulario, Preguntas, En WhatsApp y Último cambio (fecha y hora completas), el filtro «Estado ▾» y «Nuevo formulario». El nombre abre el formulario con su pestaña «Diseño». Reemplaza a `FRM-Listado`;
- **`FRM-Editor` (con `CA4`):** el editor en una sola pantalla:
  - el formulario: nombre, «Para quién es» (empleados, clientes o los dos; 50), texto del botón final y prefijo, cada uno en su campo;
  - las preguntas, que se abren en el lugar, con ⋮ (Editar, Subir, Bajar y Quitar);
  - «elegir varias», con el mínimo y el máximo (en el ejemplo, «Fallas», de 1 a 3);
  - la pregunta «De un catálogo» (`CA4`): el selector, «Permitir elegir varias» con mínimo y máximo, «varias, hasta N» en la lista de preguntas, un catálogo de más de 20 y la presentación de la pregunta (la del catálogo o la suya);
  - el teléfono al costado.

  En el teléfono, la pregunta se edita en una hoja y los botones van fijos abajo.
- **`FRM-Catalogos-14`:** el listado de catálogos de la E12 con la columna «Lo usan».
- **`WA-Mensajes-14`:** «Mis mensajes», con la tarjeta de una respuesta propia de un cliente que vinculó su cuenta.
- **`FRM-Publicar`:** «Publicar la versión N en WhatsApp», con qué cambia y qué queda fijo.
- **`FRM-Publicando`:** cómo termina, arriba del editor, con los pasos Aceptada, Enviada a WhatsApp, WhatsApp la revisa y Lista para mandar. Al costado, la vista oficial con «Borrador | Versión N en WhatsApp».
- **`WA-Chats-Formulario` y `M-WA-Chats-Formulario`:** «Enviar formulario» al lado de «Enviar» en Internos (en el teléfono, en el ⋮ del chat). El diálogo «Enviar un formulario a …» tiene una tarjeta por formulario, con la versión y la cantidad de preguntas cada una en su lugar (con buscador y paginado si son muchos), la cantidad de opciones por pregunta de catálogo y los problemas de la oferta, el mensaje prellenado de hasta 1024 caracteres y hasta cuándo se puede mandar, con fecha y hora. Sin «Ya lo revisé» ni «Resolver el envío» (80).
- **`WA-Chats-Formulario-Cliente` y `M-WA-Chats-Formulario-Cliente`** (nuevos): «Enviar formulario» en un chat de Clientes, con solo los formularios para clientes o para los dos (49 y 50). En el ejemplo, «Entrega de pedido» es para los dos.
- **`WA-Chats-Respuesta` y su teléfono:** la tarjeta con las primeras 3 respuestas en grilla, «Ver las N respuestas» (en el teléfono, en una hoja) y «Guardada como VIS-000123»; el valor de catálogo con su código, y el rechazo de una opción dada de baja con «Mandar de nuevo».

**Sin rastro de etapas posteriores:** los tableros de la v141 no tienen «4 reglas · Revisión 3» ni «Más de 10 archivos». `Rol` suma los cinco permisos de formularios.

**Estados que se dibujan** (cada uno tiene que poder producirse; si no, es un bug):
- **Editor:** con cambios, guardando (el botón grisado con su mismo texto), guardado, no se guardó (lo escrito se conserva), otra persona guardó (con «Qué cambió | Guardado actualmente | Mis cambios»), prefijo ocupado, para quién es, quitar una pregunta, cambiar el tipo, descartar, solo lectura, cargando y no existe.
- **Publicar:** versión nueva, primera publicación, comprobando, con cambios sin guardar (publicar grisado, con el motivo en su tooltip), una pregunta que no se puede mostrar, un catálogo demasiado grande, cambió después de comprobar y ya se está publicando.
- **Cómo termina:** publicando, publicado, rechazada con «Ir a la pregunta», sin confirmar con «Consultar ahora» (nunca «Reintentar»), necesita revisión y la vista oficial.
- **Enviar:**
  - En cola, Leído, y No se pudo mandar (con ⓘ, sin reintento);
  - con el diálogo abierto, ventana cerrada y formulario que ya no está.
- **Al mandar una pregunta de catálogo:** la cantidad de opciones por pregunta; en rojo y con el envío grisado: más de 200 (o 20 en varias), menos que el mínimo, ninguna en una pregunta obligatoria, un catálogo que no se puede usar o una opción que no entra.
- **Respuesta:**
  - guardando, no se pudo guardar, no reconocida y sin permiso;
  - el mensaje ya no está;
  - con «elegir varias»;
  - una opción dada de baja no se guarda y ofrece «Mandar de nuevo»;
  - venció sin respuesta.

**Mandan sobre esta spec en:**
- **Las fichas del front** (pedido del usuario del 2026-10-25): un dato por celda, en un renglón; las fechas completas, con fecha y hora en la zona de quien mira, nunca «hoy», «ayer» ni «hace…»; sin rótulos que expliquen, así que la vista previa es el teléfono al costado; y «Dados de baja», cuando llegue, es una opción del filtro «Estado».
- **Textos:** la lista se llama «Formularios», la pestaña del editor «Diseño», y la acción del chat es «Enviar formulario» (79).
- **Botones (43):** «Guardar cambios» se habilita cuando hay cambios, sin «Cambios sin guardar». Mientras trabaja, queda grisado con su mismo texto. Si falta un dato obligatorio, el botón sigue habilitado y al tocarlo se marca el campo.
- **Si no se sabe si algo salió (43):** ante un corte de red al guardar o al mandar, la pantalla vuelve a leer sola y muestra lo que quedó, sin pisar lo escrito:
  - **al guardar:** relee el borrador y compara **solo el contenido**, sin las identidades que emite el servidor. Si es igual a lo mandado, quedó guardado y el editor toma la versión y las identidades de esa relectura; si no, Guardar sigue habilitado con lo escrito y la versión de antes. Nunca reintenta solo. Tomar la versión de una relectura es una excepción a [concurrencia](../../rules/concurrencia.md), que se escribe en la ficha de formularios del front;
  - **al mandar:** relee el chat. Si la sesión existe, se ve el envío; si no, se puede mandar de nuevo con la misma `Idempotency-Key`, que devuelve el resultado guardado si el primero sí llegó.
- **Envío pendiente:** un envío pendiente bloquea el chat (`WhatsApp.Chat.SendPending`). No bloquea que haya otro formulario sin responder.
- **Un envío que no se sabe si salió** lo reintenta la cola sola, como a los demás mensajes; no hay «sin confirmar» ni «Ya lo revisé» (80).
- **«Cambió después de comprobar»** es `Forms.Publication.DraftChanged` (T8).
- **«Necesita revisión»:** `FormPublication` tiene el estado `NeedsReview`. Es para cuando Meta no confirmó la creación de un Flow y la consulta no puede saber si existe (un Create sin id no se adopta por nombre). Una persona con `forms.publish` lo mira en Meta y lo resuelve.
- **«No reconocida»:** una respuesta de Flow que no corresponde a ninguna sesión se muestra en el chat como un mensaje sin valores y no crea nada en Forms.
- **La banda de conflicto** («Otra persona cambió esto mientras lo editabas.») lleva ⚠.
- **«Venció sin respuesta»:** se calcula al leer: la sesión sigue `Pending` y ya pasó `ExpiresAtUtc`. `Expired` queda para una respuesta que llegó tarde.
- **El código de la opción de un catálogo se ve en la tarjeta de la respuesta,** debajo del valor (nunca en WhatsApp).
- **Cualquier catálogo de la organización** sirve en cualquier formulario: el contenido no tiene idioma (35).

## 10. Tests

| Dónde | Qué |
| --- | --- |
| `tests/…Domain.UnitTests/Forms/` | identidades; tipo cambiado = identidad nueva; de «elegir una» a «elegir varias» = identidad nueva, y cambiar el rango la conserva; prefijo (formato, bloqueo); transiciones de `FormPublication`; numeración en `FormDefinitionState` |
| `tests/…Application.UnitTests/Modules/Forms/` | validadores (rango de «elegir varias»); reglas de publicar (textos obligatorios, «nada para publicar», un catálogo demasiado grande); guardar sin cambios compara la versión sin emitir identidades; armado del Flow (el `CheckboxGroup`, sin `data` cuando no hay nada que mandar, los textos del sistema en el idioma de quien recibe) y verificador de respuestas (cada tipo, punto y coma, decimales de más, «125.500» con 0 decimales, obligatoria vacía, opción ajena, ids repetidos o fuera del rango en «elegir varias», presupuesto de 16 KiB); el armado de la oferta (más de 200, más de 20 en varias, menos que el mínimo, ninguna en una obligatoria, nombres largos cortados); el perfil con datos y el verificador (`localId` fuera de la oferta) |
| `tests/…Api.IntegrationTests/Modules/Forms/` | cada ruta: 401, 403, feature apagada = 404, validación, 409, idempotencia y paginado; publicar con un canal de prueba (Published, Uncertain → verifica, Failed); captura idempotente y número sin saltos con dos capturas a la vez; «elegir varias» con una fila por opción y su posición; tarjeta con y sin `formrecords.read`; captura con una opción dada de baja = sin número, y la tarjeta con «Mandar de nuevo»; los campos sellados no cambian al editar el catálogo; un campo que usa la presentación de una pregunta no se quita; «Lo usan» con su segunda página; una respuesta o una publicación que termina mientras alguien edita no le da un 409 al guardar; publicar con un borrador que cambió después de comprobar = `DraftChanged`; baja de cuenta; ejemplos exportados de cada respuesta |
| `tests/…Api.IntegrationTests/Modules/Forms/*IsolationTests.cs` | otra organización recibe 404 en cada ruta; RLS de cada tabla; la captura no cruza organizaciones; la oferta nunca trae opciones de otra organización (modelo: `Modules/Assistant/AssistantConversationIsolationTests.cs`) |
| Volumen | ofrecer y capturar con un catálogo de 10.000 opciones y 20 campos, sin traer todo para quedarse con poco, con EXPLAIN |
| `tests/…Api.IntegrationTests/Modules/WhatsApp/` **[E11]** | botón y motivos, por bandeja; sesión vencida por la hora de Meta; un mensaje con error no frena a los demás; respuesta de más de 16 KiB rechazada y visible; la captura de un cliente sin cuenta, sin `ExecutionActor` y fuera de la transacción del procesador; con `forms` apagada, rechazada; un empleado que perdió el permiso o la membresía, `NotAllowed`; un `nfm_reply` nunca llega al bot; la tarjeta copiada en «Mis mensajes» de quien vinculó su cuenta |
| Guardas (partials del módulo) | `ExplicitRouteInventoryTests`, `ModuleControllersTests`, `ApplicationServicesTests`, `TenantScopeUsageTests`, `ResourceParityTests` (sumar los resx de Forms), `PermissionTextsTests` y `ModuleIsolationTests` en verde; `TransactionBoundaryTests` sin ampliar excepciones, y la guarda de capas de baja como `AssistantMaintenanceLayerTests` |
| Front | Vitest con MSW y axe en las páginas y el diálogo; los ejemplos exportados validados con zod estricto; `registration`, `navigation` y `composition` del módulo; paridad de sus recursos `forms` y `enums` compuestos desde `registration.ts`, también al retirar Forms; los botones según la decisión 43 |
| E2E real y recorrido | crear con los siete tipos, publicar contra el canal real o el de prueba, mandar a una cuenta en inglés (el formulario llega como está escrito y los textos del sistema, en inglés); responder y ver el número tras recargar; usar un catálogo en una pregunta de «elegir varias», mandar y responder; dar de baja una opción antes de responder y mandar de nuevo |
| Prueba con Meta | con el número de una organización de prueba: §15 |
| Retiro | una vez, con la base vacía: quitar `Forms` con sus catálogos (guía nueva, `docs/guides/quitar-formularios.md`, copiando `quitar-asistente.md`) y que el núcleo y WhatsApp sigan en verde |

## 11. Premisas aplicadas

Rige la tabla de la [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#premisas-del-repo-aplicadas-a-formularios). Lo propio de la E14:

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

Al planificar la E14 se verifican contra el código real.

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E11] | El modelo de números: `WhatsAppChannels`, la WABA de cada número y el token por organización. | Un Flow por versión y WABA. Se publica en todas las WABAs con números conectados. |
| [E11] | Conversaciones y mensajes: ids, tipos de mensaje y el contrato de la lista de mensajes. | Se suman un mensaje saliente «Formulario» y la tarjeta `form`. |
| [E11] | Reconocer al empleado y la bandeja Internos; «tomar» un chat. | Mandar cuenta como responder: toma el chat. |
| [E11] | La entrada de mensajes por organización: el contexto con `SetFromAccess`, la transacción del procesador y el paso por el bot. | La captura es una fase aparte, fuera de esa transacción y sin bot (T5). |
| [E11] | El outbox con el número de la organización. | `WhatsAppOutboundMessage` suma la variante Flow con el `PhoneNumberId` de la organización. |
| [E11] | Las rutas de WhatsApp por bandeja (`customers` e `internal`) y el módulo de WhatsApp del front. | Los nombres de esta spec son provisorios. |
| [E11] | La copia compartida de la conversación para «Mis mensajes». | Guarda la tarjeta armada al capturar (T16). |
| [E12] | El esqueleto del módulo, los catálogos, los lectores de las fuentes «Miembros» y «Contactos», y la presentación y los campos de la E13. | `FormOfferBuilder` reusa los lectores, con una fila más que el tope. |
| [Meta] | Los límites del `Dropdown` y del `CheckboxGroup` dinámicos: cuántas opciones, el largo del título y del detalle, y el tamaño de `data` en el mensaje. | 200 opciones en `Dropdown` y 20 en `CheckboxGroup`, título ≤30 y detalle ≤300. El piloto de ArquitecturaBase confirmó 200 opciones con 136 caracteres de detalle cada una. |
| [E9] | Las cuotas compartidas ([cuotas](../../rules/cuotas.md)). | Las rutas de Forms cuentan en las cuotas generales; no se definen perfiles propios. |
| [E9] | Redis. | La E14 no cachea nada nuevo. |
| #51 | `Enter` borra `TenantKind`. | El worker de publicación usa `ExecutionActor`, y la captura, `SetFromAccess` (T5). |

## 13. Decisiones de producto de la etapa

Están en la §5 de la hoja de ruta y no se vuelven a preguntar: 9 (la respuesta guarda todas las empresas activas de quien respondió), 10 (manda cualquiera con «Atender internos», y a un cliente, con «Atender clientes»), 11, 21 y 36 (un nombre de más de 30 caracteres se corta en 29 y se le agrega «…», y el completo va primero en el detalle), 12, 22 y 37 (un catálogo con filtros que da más de 200 opciones se guarda, y el tope frena al mandar), 49 y 50 (los clientes completan los formularios que son para clientes), 35 (el contenido no tiene idioma), 38 («elegir varias» va acá), 43 (botones y avisos), 78 (la E14 nace con la pregunta «Elegir de un catálogo»), 79 (una sola lista «Formularios», con la pestaña «Diseño») y 80 (lo que sale por la cola de WhatsApp se reintenta solo, sin «sin confirmar»).

## 14. Referencia de Meta (tomada de ArquitecturaBase)

Es lo único que se toma de ArquitecturaBase: lo que dice Meta y lo que ya se pagó. Los archivos están en `src/ArquitecturaBase.*/Modules/WhatsApp/` de `origin/main`. **Antes de programar, se verifica contra la documentación vigente de Meta.**

| Tema | Lo que se sabe |
| --- | --- |
| Perfil | `flow-static-7.3-r9`: Flow JSON `7.3`, la versión de Graph de la configuración del módulo (hoy `v26.0`, no la `v25.0` de ArquitecturaBase), sin endpoint ni `data_api_version`. Una pantalla `FORM` terminal: `SingleColumnLayout` > `Form` > controles + `Footer` con `on-click-action: complete` y un payload `field_<GuidN>: ${form.field_<GuidN>}`. Los textos del sistema son propiedades dinámicas de `data` (T6). |
| Componentes | Texto corto: `TextInput` con `max-chars` (80 por defecto). Texto largo: `TextArea` con `max-length` (600). Número: `TextInput` de texto con el patrón `^-?(0|[1-9][0-9]*)([.,][0-9]+)?$`, de hasta 60 caracteres. Fecha: `CalendarPicker` single. Sí/no: `RadioButtonsGroup` con ids `true` y `false`. Elegir una: `Dropdown` con el `ChoiceId` como id. Elegir varias: `CheckboxGroup` de 1 a 20 opciones, con `min-selected-items` y `max-selected-items` sellados; sin ayuda, dice «Elegí hasta N». `max-chars` y `max-length` van como **números** JSON (Meta rechazó la versión que los mandaba como texto). |
| Límites | Etiqueta: 20 caracteres (30 en sí/no y en elegir varias, 40 en fecha). Ayuda: 80 (300 en la descripción de sí/no y en elegir varias). Opción: 30. Título: 80. Botón: 35. Hasta 40 preguntas, 50 opciones y 50 componentes; JSON ≤1 MiB. Se cuentan en UTF-16. Sin recortar: si no entra, se informa con su `FieldId` y el texto. |
| Presupuesto de respuesta | 16 KiB. El peor caso es 785 + la suma de (42 + los bytes del valor): texto 2 + 6 × largo, número 62, fecha 12, sí/no 7, opción 38, elegir varias 38 por opción marcable. Si se pasa, no se publica (`ResponseTooLarge` sobre los largos de texto). |
| Publicar | (1) Comprobar que el número sea de la WABA: `GET {waba}/phone_numbers`. (2) `POST {waba}/flows` con `{name, categories:["OTHER"]}`. (3) `POST {flow}/assets` multipart con `asset_type=FLOW_JSON`; los `validation_errors[].pointers[].path` se mapean a la pregunta y se descarta el texto de Meta. (4) `POST {flow}/publish`. (5) Verificar: `GET` del Flow y de la salud del número, bajar el asset (solo de `*.fbcdn.net` o `mmg.whatsapp.net`, sin redirecciones, ≤1 MiB) y comparar el SHA-256. Nombre técnico del Flow: `form_<publicationId>_<wabaId>`. |
| Vista previa | `GET {flow}?fields=preview.invalidate(false)`. La URL tiene que ser de `business.facebook.com` y trae `expires_at`. |
| Errores | **Incierto** (no se repite solo, se verifica): 5xx, 429, timeout, cuerpo ilegible, Create 2xx sin id. **Definitivo:** otros 4xx y 2xx con `validation_errors`. Lo que falla antes de llamar a Meta termina en fallida, no en «Sin confirmar». **Sin medio de pago** (141006, visto en ArquitecturaBase; el repo todavía no lo conoce): el Flow aparece disponible pero la salud total queda BLOCKED; acá se muestra como `WhatsApp.Flow.AccountUnavailable`. |
| Mensaje | `type: interactive`, `interactive.type: flow`, cuerpo ≤1024, `action.name: flow`, `parameters: {flow_message_version: "3", flow_id, flow_token (≤128), flow_cta (≤20, «Abrir formulario»), flow_action: navigate, flow_action_payload: {screen: "FORM", data?}}`. `data` lleva solo los textos del sistema que el Flow usa; si no hay ninguno, no se manda (T14). |
| Respuesta | `messages[].interactive.type == "nfm_reply"`, `nfm_reply.name == "flow"`, `response_json` (texto) con `flow_token` y `field_<GuidN>`. Profundidad ≤4, ≤50 campos; «elegir varias» llega como array de ids. Una respuesta de más de 16 KiB se guarda como rechazada, visible, y no se descarta en silencio. |
| Valores | Texto: hasta el largo y sin NUL. Número: punto o coma, sin exponente ni separador de miles, sin redondear; se recortan los ceros a la derecha. Fecha: `yyyy-MM-dd`. Sí/no: solo `true` o `false`. Opción: un `ChoiceId` de la versión sellada. Elegir varias: ids distintos de la versión, en el orden recibido. Vacío: si es obligatoria, error; si no, ausente. Veinte selecciones no consumen el tope de diez archivos. |
| Lecciones | <ol><li>commitear `InFlight` antes de cada POST, y ante una duda verificar, no repetir;</li><li>un Create sin id confirmado nunca se adopta por nombre;</li><li>usar la hora firmada de Meta para vencer, y aislar los errores mensaje por mensaje;</li><li>el token no forma parte de la identidad de la cuenta (WABA + número): rotarlo no rompe nada;</li><li>no hashear arreglos sin orden fijo;</li><li>en el teléfono hay que probar: ausente contra `false`, el largo máximo de texto y la sesión de una versión anterior. Entran en la prueba con Meta.</li></ol> |
| No se toma | El número y la WABA globales; los permisos planos (`formtemplates.*`); el contacto atado 1:1 a un usuario con `LinkRevision`; el bloqueo global `forms:definitions` y el prefijo global; un Flow por idioma; los modos Manual, Bot y Asistente en esta etapa (la E23 los suma, 52); la pausa de 6 s por destinatario en Redis; los recibos de borrador de 30 días; los perfiles anteriores a r9. |

## 15. Cómo se cierra

La puerta de la etapa ([etapa en paralelo](../../guides/etapa-en-paralelo.md)) tiene las suites, el E2E real, la documentación en 0, la galería, el recorrido y la lista de control de `/puerta`. Además, hay una **prueba real con Meta** con el número de una organización de prueba, abriendo cada variante en un celular:
1. **Formulario de prueba:** con los siete tipos de pregunta, entre ellos «elegir varias» con mínimo 1 y máximo 3. Meta confirma `PUBLISHED`, con un solo Flow.
2. **Mandar a una cuenta en inglés:** el formulario llega como está escrito, y el botón, la ayuda de los números y «Elegí hasta N» llegan en inglés. Se abre en Android y en iPhone.
3. **Completar:** dejar vacía una opcional, poner `12.50` en el número y marcar dos opciones. En el chat aparece el número de la respuesta tras recargar, y las dos opciones quedan en su orden.
4. **Mirar lo que llega de Meta:** qué manda para una opcional vacía y para «elegir varias» sin marcar, y que `12.50` y `12,50` se guardan iguales.
5. **Una WABA sin medio de pago** (si se puede probar): queda `AccountUnavailable` visible, no «Sin confirmar» para siempre.
6. **Un catálogo a mano,** con y sin detalle, en un formulario publicado. Se manda, se responde, y la respuesta guarda el texto, el detalle, el código y el catálogo; se ven igual después de recargar.
7. **«Elegir varias» con un catálogo:** mínimo 1 y máximo 3; se marcan dos y quedan en su orden.
8. **Miembros y contactos:** un catálogo de miembros con filtros, vacío y con opciones, con «Sin incluir a quien lo recibe», y uno de los contactos para elegir a un cliente.
9. **Opción dada de baja después de mandar:** la respuesta se rechaza sin número. «Mandar de nuevo» da el número siguiente, sin saltos.
10. **El tope:** un catálogo de 200 opciones con detalle viaja y vuelve con los ids intactos.
11. **La presentación y el sellado:** «Proveedores» con el CUIT en la descripción (la del catálogo) y una pregunta con su presentación propia se ven así en el celular; al responder, los campos quedan sellados, y cambiar el CUIT en el catálogo no cambia lo respondido.
12. **Lo que no se verificó en Meta:** el tope de `data` en el mensaje, el `flow_token` de 128, el cuerpo de 1024 y un `Dropdown` con `${data.x}` sin endpoint.
13. **«Mis mensajes»:** un cliente que vinculó su cuenta ve la tarjeta de su respuesta.

## 16. Criterios

Salen de los reviews y los pilotos de ArquitecturaBase. El plan los toma como lista de control, junto con la de `/puerta`.

- **Captura:**
  - una falla técnica se reintenta hasta **5 veces y al menos 15 minutos** desde la primera, con intervalos 30, 90, 210, 450 y 930 s; recién ahí queda `Rejected` y visible;
  - un mensaje trabado no frena a los demás.
- **Catálogos al mandar:**
  - al operador le llegan solo la cantidad y los problemas, nunca los nombres de las personas ofrecidas;
  - las opciones vacías no viajan, y el texto de una opción sale de un campo que siempre tiene valor;
  - una pregunta de catálogo sin selección manda `null` y recibe la validación en su campo.
- **Estados inciertos:**
  - un error definitivo de Meta termina en «No se pudo», visible;
  - una publicación «Sin confirmar» se **consulta** y, si sigue incierta, una persona dice «salió» o «no salió»; nunca se ofrece «Reintentar» sobre lo incierto;
  - un envío del chat sale por la cola de WhatsApp, que lo reintenta sola (80).
- **Contratos:**
  - cada campo nuevo de un DTO se exporta, con su ejemplo de una corrida real, y el front lo consume con zod estricto en el mismo corte;
  - las rutas de error tienen la misma forma en los dos lados;
  - `""` no es `null`: lo que falta va como `null`, y una propiedad vacía se omite en lo que va a Meta;
  - una entrada inválida es 400, nunca 500;
  - un campo omitido no apaga nada.
- **Guardado:** ante un error de red o un 5xx, el editor relee y compara el contenido (§9); nunca reintenta solo. Solo el 409 de versión pide revisar.
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
  - 141006 cuando la WABA no tiene medio de pago, que se suma al cliente de Meta;
  - 131030 con el 9 argentino en un número de prueba.
