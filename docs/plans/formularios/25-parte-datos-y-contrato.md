# E25 · Documentos por WhatsApp: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E25](25-documentos.md), que dice qué leer y cómo se cierra.

## Contrato

**Los documentos** son rutas de `/api/organization/forms`, con `[FeatureGate("forms")]`; `form-proposal` y `form-drafts`, que usan la IA, suman `[FeatureGate("assistant")]`.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `POST /documents/search` | `formrecords.read` | `{form?, senders?, received?, states?, companies?, search?, page, pageSize, sort}` | `PagedResult<{id, fileName, mediaType, formName?, sender, receivedAtUtc, state, summary, record?{id, displayNumber}, deleteAtUtc?}>` |
| `POST /documents/facets` | `formrecords.read` | los filtros | conteos por estado, formulario y empresa |
| `GET /documents/{id}` y `…/content` | `formrecords.read` | — | los datos con su seguridad, las validaciones, el porqué y el original, `no-store` |
| `PUT /documents/{id}` | `formrecords.read` y atender esa bandeja | `{values, version}` | 200 `{version}` |
| `POST /documents/{id}/reading` | `formrecords.read` y atender esa bandeja | `{formId, version}` | 200 `{version}`; la extracción sigue por su trabajo, con lo ya leído (`Read`), sin mandar otra vez el documento |
| `POST /documents/{id}/discard` y `…/reopen` | `formrecords.read` y atender esa bandeja | `{version}` | 200; «Volver a abrir» un descartado, también uno vencido, mientras no se borró: vuelve a su estado anterior y su vencimiento corre de nuevo (215). Uno que no está descartado da 409 `Forms.Document.NotDiscarded` |
| `POST /documents/{id}/record` | `formrecords.read` y atender esa bandeja | `{version, confirmRepeated?}`: sin `confirmRepeated`, un documento que repite una respuesta guardada da 409 `Forms.Document.Duplicate`, con la respuesta; desde la carga de la web, en multipart, además `answers` y los archivos, como el `POST /{id}/records` de la E20. Con `[Idempotent(Mode = IdempotencyMode.DurableReference)]` y `[IdempotencyDurable(typeof(FormEntryDurableAdapter))]`, porque el número es privado | 200 `{recordId, displayNumber, version}`, con `[ResponseVersion("Version")]` |
| `POST /documents/{id}/form-proposal` | `forms.manage` y `formrecords.read` | — | la propuesta de un formulario, la misma del asistente, con `reading` y `similarTemplate?` |
| `POST /documents/{id}/form-drafts` | `forms.manage` y `formrecords.read` | la propuesta, con `[Idempotent]` | 201 `FormDetail` |
| `GET /{id}/reading/stats` | `forms.read` | — | `{confirmedRun, precision[]}`, de los últimos 30 días |
| `GET /{id}/reading/examples` | `forms.read` y `formrecords.read` | `page`, `pageSize` | `PagedResult<{documentId, fieldId, proposed, confirmed, correctedBy, correctedAtUtc}>`: «Ver los ejemplos», las últimas correcciones; nunca van a la IA (216) |

Atender esa bandeja se revisa en el servicio, con `IStructuredInboxAccess`, porque depende de dónde llegó el documento, y Forms no puede nombrar un permiso de WhatsApp: retirar ese módulo lo saca del catálogo. `search` ordena por sus columnas con `sort`, con lo vacío al final y el estado por el circuito (264); no ordenan las de varios valores.

**La Configuración de Formularios** (270 y 272) es la de la E18, que suma la zona «Documentos recibidos». Cada valor tiene su valor por defecto, su mínimo y su máximo; la organización lo elige dentro del máximo que fija la plataforma en la suya, y en cada uso vale el menor de los dos:

| Valor | Campo | Por defecto | Rango | Qué hace |
| --- | --- | --- | --- | --- |
| «Formularios por planilla» | `loadsPerSpreadsheet` | 3 | 1 a 10 | cuántas cargas propone por mensaje una planilla con varias filas |
| «Páginas de un PDF» | `pdfPages` | 10 | 1 a 10 | el proveedor no lee más; uno más largo no se lee |
| «Relecturas por persona y día» | `rereadsPerPersonPerDay` | 5 | 1 a 100 | lo que se relee al publicar un formulario armado con documentos, por quien lo armó |
| «Parecidos para sugerir un formulario» | `similarDocumentsToSuggest` | 3 | 2 a 20 | desde cuántos sin clasificar del mismo tema la campana dice «Llegaron N documentos parecidos» |
| «Vencer los propuestos sin revisar» | `proposedExpiryDays` | 90 | 1 a 3650 | días desde el último cambio o el fin del envío (215) |
| «Borrar los descartados» | `discardedRetentionDays` | 90 | 1 a 3650 | días desde el descarte, con su lectura (41) |

`GET` y `PUT /settings` (E18) los suman, y el `GET`, el máximo de la plataforma de cada uno, como `platformMaxFileMiB`; `/api/platform/forms/settings` suma los seis máximos. Revisión y 409, como en la E18. Los textos que nombran un número lo reciben como parámetro («Nadie lo revisó en {n} días: venció.», «más de {n} páginas»), y la (i) de los dos plazos avisa que la política de privacidad tiene que prometer lo mismo (272). Los dos topes de lecturas por día siguen en `appsettings`, porque son de costo (217).

**Armar con la IA** (154, 155 y 167): crear y el catálogo propuesto van en `/api/organization/forms`, con `[FeatureGate("forms")]`; el audio, en `/api/organization/assistant`, con `[FeatureGate("assistant")]`. Todas llevan `[Access(Business)]`.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `POST /api/organization/forms/assistant-proposals` | `forms.read` y `forms.manage` | `{turnId, proposalId}`, con `[Idempotent]` | 201 `FormDetail` + Location; si ya se creó, lo mismo |
| `POST /api/organization/forms/catalogs/assistant-proposals` | `formcatalogs.read` y `formcatalogs.manage` | lo mismo | 201 el detalle del catálogo + Location; si ya se creó, lo mismo |
| `POST /api/organization/forms/catalogs/{id}/approval` | `formcatalogs.manage` | `{version}` | 200 `{version}`: «Aprobar y activar» |
| `DELETE /api/organization/forms/catalogs/{id}` | `formcatalogs.manage` | `version`, solo si espera aprobación | 204: «Descartar propuesta» |
| `POST /api/organization/assistant/audio` | `assistant.use` | multipart con `requestId` y `file`, con `[Idempotent]` | 202 `{state}`; repetir el `requestId` con el mismo archivo devuelve la misma transcripción, y con otro, 409. Sin cupo, 429 sin guardar nada, y toma un lugar de subida ([0064](../../decisions/0064-reglas-de-toda-la-ia.md) §2 y §4) |
| `GET /api/organization/assistant/audio/{requestId}` | `assistant.use` | — | el estado y, al terminar, la transcripción; una falla trae su texto. Solo a su persona |
| `GET /api/organization/assistant/audio/{requestId}/file` | `assistant.use` | — | la grabación, `no-store` y `nosniff`, solo a su persona |

**Crear** relee la propuesta del turno por `IAssistantProposalLookup`: solo un turno terminado del chat del Inicio de la misma persona, con sus permisos de ahora. Rechaza una que otra posterior de la misma clase ya reemplazó: un catálogo no reemplaza a un formulario. Crea una sola vez: repetir devuelve lo creado, aunque la propuesta haya vencido. **Rutas que cambian:** `PUT /catalogs/{id}` de la E12 guarda uno que espera aprobación sin activarlo (`isActive: true` da 409 `Forms.Catalog.ApprovalPending`), y su detalle suma `proposal?` `{requestedBy, channel, requestedAtUtc}`, que la pantalla muestra como «La pidió … en el chat del Inicio el …»; la lista de catálogos suma `isProposal`. **El import de la E12** (`ImportDialog` y su ruta) suma, en lugar del archivo, `source: {turnId, proposalId}`: lo abren «Crear catálogo» con una planilla y «Revisar la importación» (222), y relee el adjunto del turno por `IAssistantProposalLookup`, con los permisos de ese momento; importar sigue siendo de la persona.

**La propuesta en el chat del Inicio** (268) es la de «Acciones del asistente»: `Message` ya trae `proposal?` `{id, kind, action, version, state}`, y esta etapa suma a `kind` sus clases `form`, `catalog` y `catalogImport` (importar en uno que existe, con su `catalogId`, 222). En esas clases no van `action` ni `version`, y van `content`, `unavailableReason?` y, aplicada, `createdId?`, porque la tarjeta se dibuja sin el `GET` de la propuesta. `content` es la presentación de Forms: el nombre, el prefijo, el texto del botón y las preguntas, con el bloque y las suyas y, con un documento de ejemplo, `reading` y una muestra por pregunta; o el tipo de valor, los campos, las columnas y tres filas de muestra con el total. Llega en `done`, dentro de `TurnResult.message`, y en el historial; `delta` trae solo la explicación. Un turno sin propuesta conserva la forma de siempre. **Los estados** son los de esa etapa:
- `new` y `replaced`: el reemplazo sigue siendo por clase (0057): una propuesta nueva reemplaza a la anterior sin aplicar de su clase en esa conversación, y las clases son los formularios (`form`), los catálogos (`catalog` y `catalogImport`) y las acciones (`action`);
- `applied` reemplaza a `created` y a `imported`: lo pide el asistente a Forms (`GetOutcomesAsync`), en una sola lectura por página, y en `catalogImport` lo pone el import;
- `unavailable` se calcula en cada lectura, con el motivo `permission` (sin `forms.manage` o `formcatalogs.manage`), `module` (sin `forms`) o `target` (el catálogo de `catalogImport` ya no está);
- `unconfirmed` no aplica: Forms guarda lo creado y `FormAuthoringOutcomes` en la misma transacción, así que siempre sabe si se creó.

**Las demás rutas que cambian:**
- `GET /{id}` y `PUT /{id}/draft` (E14) suman `reading?` y `autoLoad` (400 `Forms.Draft.ReadingInvalid`), y `FormDetail`, `autoLoadBlockedReason?`.
- El borrador de una plantilla y su vista (E22) suman `reading?`, con las validaciones fijas y su candado.
- El detalle de una respuesta (E15 y E21) suma `sourceDocument?{id, fileName, reviewedBy?}`, y su actividad, el renglón «La pasó desde un documento» (267).
- Asistente (E8b): `Message.proposal?` suma las clases de arriba (268), y `Message`, `audio?`; enviar, `audioRequestId?`, con 404 `Assistant.Audio.NotFound` y 409 `NotReady`; el adjunto, `.xlsx`, `.csv` y `.docx`.
- Un caso (E23 y E24) suma el tipo de entrada `Document`; el porqué de un documento sale por `GET …/conversations/{id}/actions/{actionId}`.

**El clip** de la E8b suma, para armar, `.xlsx`, `.csv` y `.docx`, de a uno por mensaje: un mensaje con uno de esos lleva solo ese archivo (`Assistant.Attachment.TooMany`). Los acepta solo con `forms.manage` o `formcatalogs.manage`, con el tipo verificado por el contenido y los topes de la lectura. De una planilla, al modelo van los encabezados y unas filas de muestra.

**El audio del Inicio** (156 y 171): WebM o MP4, reconocido por sus bytes (la cabecera EBML o la caja `ftyp`), de hasta 1.000.000 bytes. Se guarda como copia privada, con una clave que sale del `requestId`, y se transcribe con `ISpeechTranscriber`, en el tope de IA de la organización. El mensaje lo nombra con `audioRequestId`, y entonces `text` puede ir vacío; el turno toma la transcripción del servidor, nunca la del navegador. `Message` suma `audio?` `{requestId, transcript}`. **El audio vive mientras un turno con contenido lo nombre,** y se borra con su conversación, en la misma operación (223); uno que ningún mensaje nombró se limpia a las 24 horas, como un adjunto pendiente de la E8b. No hay ruta en `/api/me` ni en `/api/platform` (171).

**Los topes del turno** son los de la E8b: 5 llamadas, contando el título, y 2048 tokens de salida, con 51200 de entrada; el turno que ofrece armar tiene 8192 de salida (`AuthoringMaxOutputTokens`, 0057 §8). Leer lo que existe, proponer, corregir un rechazo y contestar entran en esas cinco.

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
// Asistente → Forms (implementa Forms): lo que existe, validar y presentar una propuesta,
// comprobar por qué no se arma y qué propuestas ya se crearon
public interface IStructuredAuthoring
{
    Task<Result<StructuredAuthoringSnapshot>> ReadAsync(StructuredAuthoringRead read, CancellationToken ct);
    Task<Result<StructuredAuthoringProposal>> ProposeAsync(StructuredAuthoringRequest request, CancellationToken ct);
    Task<Result> CheckDeclineAsync(StructuredAuthoringDecline decline, CancellationToken ct);
    Task<Result<IReadOnlyList<StructuredAuthoringOutcome>>> GetOutcomesAsync(IReadOnlyCollection<Guid> proposalIds, CancellationToken ct);
}
// Forms → Asistente (implementa el módulo Assistant): la propuesta de un turno propio, para crearla
public interface IAssistantProposalLookup
{
    Task<Result<StructuredAuthoringProposal>> FindAsync(Guid turnId, Guid proposalId, CancellationToken ct);
}
// WhatsApp → Forms (implementa Forms): crear desde el botón del último resumen
public interface IStructuredAuthoringCreator
{
    Task<Result<StructuredAuthoringCreated>> CreateAsync(StructuredAuthoringCreate request, CancellationToken ct);
}
```

Van en `A/Interfaces/Integrations/StructuredData/`, con sus modelos en `A/Models/StructuredData/` y sus apagados en `I/StructuredData/`, como `DisabledAssistantProposalLookup.cs`. Los implementan `A/Modules/Forms/Services/FormStructuredDocumentSink.cs`, `A/Modules/Assistant/Services/AssistantDocumentReader.cs`, `WhatsAppStructuredInboxAccess.cs`, `A/Modules/Forms/Services/FormStructuredAuthoring.cs`, `A/Modules/Assistant/Services/AssistantProposalLookup.cs` y `A/Modules/Forms/Services/FormStructuredAuthoringCreator.cs`, en ese orden.

`IStructuredFormChannel` suma mandar un mensaje con botones o una lista y un formulario prellenado. **Las herramientas de armar** (154): dos lecturas de lo que existe, `read_forms` y `read_catalogs`, con `forms.read` y `formcatalogs.read`, de hasta 16 KiB cada una, y dos propuestas, `propose_form` y `propose_catalog`, con `forms.manage` y `formcatalogs.manage`, que validan con las reglas del editor sin recortar: un dato inválido vuelve al modelo con la ruta de cada argumento, para que lo corrija en el mismo turno. Por WhatsApp suma `decline_authoring` (allá, `forms.authoring.decline`), que dice por qué no arma: falta un permiso, o lo pedido ya existe y la persona lo puede leer. Forms comprueba el motivo, y uno equivocado vuelve al modelo. Ninguna escribe, y todas corren en el alcance de solo lectura de «Acciones del asistente» (0068 §5): por WhatsApp, con la entrada del turno de canal de la E23 y, para armar, con la cuenta vinculada revalidada. Se ofrecen en el chat del Inicio de la organización y, por WhatsApp, a una cuenta vinculada, antes de los casos (171); el resumen lo arma WhatsApp con la presentación de la propuesta.

**La campana:** suma `documentsToReview` y `documentsSuggestion`, a quien tiene `formrecords.read`, por `INotificationSource` (base de la 12). Como la E14: `documentsToReview` (`{count}`, `link` `/org/formularios/documentos`, `id` `documentsToReview:{count}:{el más nuevo}`) y `documentsSuggestion` (`{count, topic}`, el mismo `link`, `id` `documentsSuggestion:{tema}:{count}`). Textos: «{count} documentos por revisar» / “{count} documents to review” y «Llegaron {count} documentos parecidos: {topic}» / “{count} similar documents arrived: {topic}”.

**Errores:**

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Document.NotFound` | NotFound | ajeno o inexistente |
| `Forms.Document.AlreadyResolved` | Conflict | ya está confirmado o descartado |
| `Forms.Document.ValidationFailed` | Validation (`values.<id>`) | no pasa una validación fija al confirmar |
| `Forms.Document.Duplicate` | Conflict | dice en qué respuesta |
| `Forms.Document.BudgetExhausted` | Conflict | tope de IA, por persona o contacto, o por organización (217) |
| `Forms.Document.NotDiscarded` | Conflict | volver a abrir uno que no está descartado |
| `Forms.Draft.ReadingInvalid` | Validation (`reading…`) | un dato sin pregunta de ese tipo, en una de «elegir varias», del bloque, con «Mostrar si» o que fija una marca, o una validación que no existe |
| `Forms.Proposal.NotFound` | NotFound | la propuesta no es de un turno propio, no existe o venció sin crearse |
| `Forms.Proposal.Replaced` | Conflict | otra posterior de la misma clase ya la reemplazó |
| `Assistant.Turn.ProposalTooLarge` | Conflict | la última propuesta sola no entra en la historia del turno ([0064](../../decisions/0064-reglas-de-toda-la-ia.md) §9) |
| `Forms.Proposal.ChatUnavailable` | Conflict | crear desde un resumen de WhatsApp cuando el chat ya no está en modo Asistente, o la cuenta ya no está vinculada a ese número en esa organización; WhatsApp contesta el motivo |
| `Forms.Proposal.NameInUse` | Validation (`name`) | ya hay un formulario o un catálogo con ese nombre, sin distinguir mayúsculas: lo dice la tarjeta y no se crea nada |
| `Forms.Definition.PrefixTaken` | Conflict | el de la E14: el prefijo está en uso, y lo dice la tarjeta |
| `Forms.Catalog.NotProposed` | Conflict | aprobar o descartar uno que no espera aprobación |
| `Forms.Catalog.ApprovalPending` | Conflict | activar con el guardado común uno que espera aprobación |
| `Assistant.Audio.NotFound` | NotFound | un audio ajeno, de otro canal o desconocido |
| `Assistant.Audio.Unreadable` | Validation (`file`) | no es WebM ni MP4 por sus bytes, o pasa 1.000.000 bytes |
| `Assistant.Audio.NotReady` | Conflict | el mensaje nombra un audio que todavía no terminó de pasarse a texto |

**Sus textos** (es / en), que suman `Forms.Document.InboxForbidden` (403, no atiende la bandeja de donde llegó) y el cambio de `Assistant.Attachment.TooMany`:
- `Forms.Document.NotFound`: «No encontramos ese documento.» / "We couldn't find that document."
- `Forms.Document.AlreadyResolved`: «Ese documento ya se confirmó o se descartó.» / "Already confirmed or discarded."
- `Forms.Document.ValidationFailed`: «Hay datos que no pasan las validaciones.» / "Some values don't pass the validations."
- `Forms.Document.Duplicate`: «Ya se cargó en {number} el {date}.» / "Already loaded in {number} on {date}."
- `Forms.Document.BudgetExhausted`: «Se llegó al tope de lectura; queda para revisar.» / "Reading limit reached; it stays for review."
- `Forms.Document.InboxForbidden`: «No atendés la bandeja de donde llegó.» / "You don't handle the inbox it came from."
- `Forms.Document.NotDiscarded`: «Ese documento no está descartado.» / "That document isn't discarded."
- `Forms.Draft.ReadingInvalid`: «Revisá la lectura de documentos.» / "Check the document reading."
- `Forms.Proposal.NotFound`: «Esa propuesta ya no está disponible.» / "That proposal is no longer available."
- `Forms.Proposal.Replaced`: «Esa propuesta se reemplazó por una más nueva.» / "A newer proposal replaced it."
- `Forms.Proposal.NameInUse`: «Ya hay uno con ese nombre.» / "There's already one with that name."
- `Forms.Proposal.ChatUnavailable`: «No lo creé: este chat ya no está con el asistente o tu cuenta ya no está vinculada a este número.» / "I didn't create it: this chat is no longer with the assistant, or your account is no longer linked to this number."
- `Assistant.Turn.ProposalTooLarge`: «Empezá una conversación nueva y pedila con los cambios.» / "Start a new conversation and ask for it with the changes."
- `Forms.Catalog.NotProposed`: «Ese catálogo no espera aprobación.» / "That catalog isn't waiting for approval."
- `Forms.Catalog.ApprovalPending`: «Para activarlo, usá «Aprobar y activar».» / "To activate it, use “Approve and activate”."
- `Assistant.Audio.NotFound`: «No encontramos ese audio.» / "We couldn't find that audio."
- `Assistant.Audio.Unreadable`: «No pudimos usar ese audio. Grabalo de nuevo.» / "We couldn't use that audio. Record it again."
- `Assistant.Audio.NotReady`: «El audio todavía se está pasando a texto.» / "The audio is still being transcribed."
- `Assistant.Attachment.TooMany` (cambia): «Podés adjuntar hasta 5 imágenes o PDF, o un solo Excel, CSV o Word.» / "You can attach up to 5 images or PDFs, or a single Excel, CSV or Word file."

## Datos

Tres migraciones, una por módulo: Forms, Asistente y WhatsApp. Todo es `ITenantOwned`, con RLS, salvo lo global. **Sin permisos nuevos:** `formcatalogs.manage` completa su texto, con aprobar los catálogos que propone el asistente.

| Tabla | Qué guarda | Regla |
| --- | --- | --- |
| **FormDocuments** (`IVersioned`) | `Source`, quién lo mandó, `FileName`, `StorageKey`, `MediaType`, `ByteLength`, `Sha256`, `State`, `DefinitionId?`, `VersionId?`, `BuiltDefinitionId?`, `Read` jsonb (lo leído: el texto, si se leyó entero, en parte o nada, y la causa), `Extracted` jsonb, `Validation` jsonb, `DuplicateOfId?`, `RecordId?`, `ReviewedByUserId?`, `DiscardReason?` (`Manual` o `Expired`), `ExpiresAtUtc?` (el vencimiento de uno propuesto, 215), `DeleteAtUtc?`, `Attempts`, `NextAttemptAtUtc` | GIN `jsonb_path_ops` en `Extracted`; índice por `Sha256`. `BuiltDefinitionId` es el formulario que se armó con ese documento: al publicarlo, sus documentos sin clasificar vuelven a leerse para él, dentro del cupo de relecturas, y quedan propuestos. `ExpiresAtUtc` y `DeleteAtUtc` se guardan con 90 días, y el trabajo y la fila los corren por la diferencia con el plazo vigente (272), como allá: bajarlo vence o borra en la próxima pasada lo que ya lo pasó |
| **FormDocumentCorrections** | `DocumentId`, `FieldId`, propuesto, confirmado, quién y cuándo | de ahí salen «Ver los ejemplos» y la precisión; nunca van a la IA (216) |
| **FormDocumentCounters** | quién (persona, contacto u organización), qué (`Read` o `Reread`), día y cantidad | clave por persona o contacto, qué y día, y una fila por organización y día; el día es el de la zona horaria de la organización, que corta a su medianoche (271, que cambia la §3 de la [0064](../../decisions/0064-reglas-de-toda-la-ia.md)) |
| **FormAuthoringOutcomes** | `ProposalId`, `Kind`, `CreatedId` y `Provenance` jsonb: el canal, quién lo pidió y cuándo, el perfil y lo que se le ofreció | único por `ProposalId`: crear una sola vez y devolver lo mismo |
| **AssistantAudios** | `RequestId`, `OwnerUserId`, `StorageKey`, `MediaType`, `ByteLength`, `Sha256`, `State` y `Transcript?` | único por `RequestId`; vive mientras un turno con contenido lo nombre, y se borra con su conversación (223) |
| **platform.BankEntities** | prefijo y nombre | dato de referencia, con su seed |
| **platform.FormDocumentWorks** | `TenantId`, `NextAtUtc`, reserva y las versiones selladas del pedido | registro técnico, sin datos de negocio; una extracción en curso sobrevive a un despliegue (0064 §6) |

**Entidades:** `D/Forms/FormDocument.cs`, `FormDocumentCorrection.cs`, `FormDocumentCounter.cs`, `FormAuthoringOutcome.cs`, `FormBankEntity.cs` (se lee con `IFormBankEntityReader`) y `FormDocumentWork.cs` (como `FormActionWork` de la E21), con su configuración en `I/Modules/Forms/Persistence/Configurations/`, y `D/Assistant/AssistantAudio.cs`, con la suya en `I/Modules/Assistant/Persistence/Configurations/`.

**Columnas nuevas:** `FormVersions` suma `Reading` jsonb. `FormRecords` suma `SourceDocumentId?`, y la aprobación de la E21 deja afuera también a quien la pasó desde la bandeja (238), salvo con «Puede decidir quien cargó» (276): `FormApprovalTasks` suma `PassedByUserId?`, que toma de `ReviewedByUserId` al abrirse, y `RoleWithoutMembers` tampoco lo cuenta (239); «Por aprobar» y su conteo no la muestran, y decidir da 403 `Forms.Approval.OwnRecord`, cuyo texto pasa a «No podés decidir una respuesta que cargaste, corregiste o pasaste desde un documento.» / “You can't decide a response you submitted, corrected or passed from a document.”. La (i) de «Puede decidir quien cargó» (E21) pasa a «Quien cargó, corrigió o pasó desde un documento la respuesta también puede aprobarla o rechazarla.» / “Whoever submitted, corrected or passed the response from a document can also approve or reject it.” (276). `WhatsAppFlowSessions` suma `SourceRef?`, opaco: el id del documento, que se propuso con ese Flow, y `StructuredRecordCapture` suma `SourceDocumentId?`, así la respuesta del Flow vuelve a su documento; Meta no devuelve el archivo, así que la captura cuenta la copia en la pregunta de archivo antes de exigir su mínimo (O38). **El `SourceKey` es `document:<id>` en los tres caminos** (el Flow, «Pasar al formulario» y «Cargar solo si es seguro»), y por eso un documento no se carga dos veces. `FormTemplateDraft` suma `reading`. `FormCatalogs` suma `ProposalState?` (`Pending` o `Approved`) y `Proposal` jsonb, con quién lo pidió, dónde y cuándo, que se conservan después de aprobar. La propuesta va en `AssistantProposals` de «Acciones del asistente», que suma las clases `Form`, `Catalog` y `CatalogImport` y `Content` jsonb. `ActionId` y `ActionVersion` pasan a admitir nulos, con un CHECK que los exige en `Action` y los deja vacíos en las demás. El índice parcial de reemplazo (`State = 'New'`) pasa a `(ConversationId, Kind)`, y cada clase busca sus `Kind` (`Catalog` y `CatalogImport` son una). Sigue habiendo una propuesta por turno entre todas las clases: una segunda vuelve al modelo como `OnePerTurn`. Los mensajes del asistente de la E8b suman `AudioRequestId?`. **Dos versiones:** cada migración agrega y escribe en el mismo despliegue (lo de `AssistantProposals`, el tipo `Document` de los casos y los `Kind` nuevos de `WhatsAppAssistantActions`): es la excepción de la [0060](../../decisions/0060-dos-versiones-conviven.md), anotada en cada una, porque todavía no hay despliegue (la E10 va después del bloque, 123) y volver a la imagen anterior no aplica. **Estados de un documento:** `Received`, `Proposed`, `Unclassified` (con `UnreadReason?`: `Protected`, un PDF con contraseña; `DailyLimit`, el tope del día; `TooManyPages`, más páginas que las de la configuración; `RereadLimit`, que espera la relectura del otro día, o `Unreadable`), `Confirmed`, `AutoLoaded` y `Discarded`. **Topes:** `Forms:DocumentReadsPerPersonPerDay` y `Forms:DocumentReadsPerOrganizationPerDay` (50), con `ValidateOnStart`; lo demás, en la configuración: `FormOrganizationSettings` y `FormPlatformSettings` (E18) suman sus seis columnas, y una fila guardada antes, o lo que escribe la versión anterior mientras conviven, lee los de por defecto (0060).

## En el multitenant

- **Datos de la organización:** documentos, lecturas, correcciones, lo creado desde una propuesta y los audios del Inicio, con RLS. Cada ruta nueva lleva su test de aislamiento.
- **Workers:** leer es una operación durable, descubierta en un registro técnico; el archivo ya lo bajó el chat, con los reintentos de la E18, y un documento recibido aguanta un día de caída de Meta: esperas que crecen hasta una hora, y se abandona solo con cinco fallas y el día cumplido (H11 de ArquitecturaBase). Lo que el chat guarda de un documento (lo leído que muestra «Por qué lo propuso» y las filas de una planilla) nace cifrado ([0065](../../decisions/0065-contenido-del-chat-cifrado.md)); `FormDocuments` es de Forms, se busca en SQL (B7) y queda como las respuestas, con RLS. Abren un scope por organización con `SetFromAccess(tenantId, TenantKind.Business)`, como la E21, sin `ExecutionActor`, porque quien manda puede ser un cliente, leen las features con `ITenantFeatureCache` y se suman a la lista de `TenantScopeUsageTests`. El borrado y el vencimiento, con los plazos de la configuración (272), no tocan la copia compartida, recorren las organizaciones con `TenantJobRunner`, en lotes con su transacción y su tope de tiempo, y corren también al arrancar.
- **Features:** las rutas llevan `forms`; leer y armar piden además `assistant`. Sin ella no se lee ningún documento, y la «Lectura de documentos» y «Armar con la IA» no aparecen, como «Cuándo lo ofrece» en la E23.
- **Accesos:** armar y el micrófono son solo del acceso de la organización; el asistente personal y el de Plataforma quedan como en la E8b (171).
- **WhatsApp:** la bajada de la E18 usa la WABA y el token de la organización.
- **Almacenamiento:** copias privadas en `IFileStore`, y el antivirus de la lista de lanzamiento.
- **Baja de la cuenta:** el participante borra los documentos sin usar de esa persona.
- **Cuotas:** las rutas cuentan en las generales; la lectura, armar y el audio del Inicio, en el tope de IA de la organización, y la lectura, además, en los dos topes del día (217), que cortan a la medianoche de la organización (271). Lo que venció sin llegar al proveedor devuelve el cupo, y la cola de subidas no la llena una sola organización (0064 §1 y §4).
- **El modelo:** leer y armar usan modelos que calificó la plataforma, con la clave que paga el turno; con la clave propia, el modelo que eligió la organización queda para las consultas del chat (218).
