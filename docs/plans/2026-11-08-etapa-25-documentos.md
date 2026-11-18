# E25 · Documentos por WhatsApp · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/25-documentos.md`, con `25-parte-datos-y-contrato.md` y `25-parte-pasos.md`. Tableros: los 24 de la 25, aprobados en el lienzo v169. ADR [0056](../decisions/0056-lectura-de-documentos-con-ia.md), [0057](../decisions/0057-la-ia-propone-y-una-persona-crea.md) y [0064](../decisions/0064-reglas-de-toda-la-ia.md), con las decisiones 215 a 223 del traspaso 15 (sus estados, en el lienzo v175). Sin decisiones pendientes. Antes de programar: la E24 cerrada y la prueba con Meta de K0.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0, K1 | Pendiente |
| Back | B1–B15 | Pendiente |
| Front | F1–F8 | Pendiente |
| Integración y puerta | I1–I4 | Pendiente |

## Forma de trabajo

Contrato primero; Back y Front en paralelo; una sola puerta al final. Las reglas van en cada texto de chat; un arreglo corre solo lo que toca, y la tabla de estado la actualiza la Guía.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [Meta] Archivo precargado | `PhotoPicker` y `DocumentPicker` «cannot be initialized using Form `init-values`», no entran en `If` ni en `Switch` y admiten `visible` con `${data…}` ([media_upload](https://developers.facebook.com/docs/whatsapp/flows/reference/media_upload), [components](https://developers.facebook.com/docs/whatsapp/flows/reference/components)) | «Adjuntamos tu documento», sin «Cambiar documento» (cuarta pantalla de `M-WA-Flow-Prellenado`), en B6; la prueba, en K0 |
| [Meta] Botones y listas | 3 botones de 20 caracteres; 10 filas de 24, con descripción de 72 ([botones](https://developers.facebook.com/docs/whatsapp/cloud-api/messages/interactive-reply-buttons-messages), [listas](https://developers.facebook.com/docs/whatsapp/cloud-api/messages/interactive-list-messages)) | Más de 24 en una fila: «…», y entero en la descripción (11) |
| [E18] a [E23] | No existen todavía | K0 |
| ADR | La 0022 y la 0024 de allá (`origin/main`, `ae60b950`) | La 0056 y la 0057, que precisa la D14 de la 0048 |
| Asistente | `AssistantMessageResponse` sin `proposal` ni `audio`; `done` con `AssistantTurnResultResponse`; clip de imágenes y PDF, 10 MiB, hasta 5; 5 llamadas, 2048 de salida y 51200 de entrada | 40 preguntas con opciones no entran en 2048: el turno que arma usa `AuthoringMaxOutputTokens` (8192) y reserva con él (0051) |
| Bibliotecas | ClosedXML 0.105.1 (con `DocumentFormat.OpenXml`) y PDFsharp-MigraDoc 6.2.4 | Ninguna nueva (B3) |
| Parecidos | — | Tres o más sin clasificar de 30 días con el tema de la clasificación |
| Allá, «La IA arma, etapas 2 y 3» | Llegaron con el traspaso 15 (`be88b2c9`), sin la prueba integral de allá | 215 a 223 y la 0064; al planificar, contrastar con el traspaso 16 |

## Contrato

### K0 · Lo que dejó la etapa anterior

El chat del contrato confirma en el código «Usa de las anteriores», el perfil `r13` (E23) como el último y que OpenAI (0055) acepta WebM y MP4. Lo que difiera va a «Cambios al contrato» antes de programar.

**La prueba con Meta, pendiente del usuario antes de programar,** con la cuenta de desarrollo de la 11 (`WhatsApp:BusinessAccountId`). El chat deja en `.local/` un Flow JSON de `r13`: un `DocumentPicker` obligatorio, visible por `${data.mostrar_selector}`, y un `TextBody` «Adjuntamos tu documento», visible por `${data.adjunto}`. Los pasos:
1. En «WhatsApp Manager» de esa cuenta: «Herramientas de la cuenta» › «Flows» › «Crear Flow», nombre «Prueba documento adjunto», categoría «Otro», plantilla predeterminada › «Crear».
2. Reemplazá el JSON del editor por el de prueba y tocá «Guardar»: sin errores.
3. Sumale al `Form` un `init-values` para el selector y guardá: da «"init-values" property should not contain a value…». Deshacelo.
4. En la vista previa interactiva, con `adjunto: true` y `mostrar_selector: false` en el `__example__`: se ve «Adjuntamos tu documento», sin selector, y el botón final termina. Al revés, el selector pide un archivo.
5. Sin publicarlo, borrá el borrador desde el ⋮ y mandá una captura de cada caso.

### K1 · Publicar antes de programar las pantallas

El Back publica rutas con 501, cuerpos, textos y ejemplos exportados estrictos, y genera `openapi.json`; el Front corre `npm run contracts`.

Todas llevan `[Access(Business)]` y cuentan en las cuotas generales. Van en `/api/organization/forms`, con `[FeatureGate("forms")]`, y las que usan la IA suman `assistant`; el audio, en `/api/organization/assistant`, con `[FeatureGate("assistant")]`. «Atender» es atender la bandeja del documento, que revisa el servicio con `IStructuredInboxAccess`.

| Ruta | Permiso | Idempotencia y versión | Errores |
| --- | --- | --- | --- |
| `POST /documents/search` y `/facets` | `formrecords.read` | — | 400 |
| `GET /documents/{id}` y `…/content` | `formrecords.read` | `no-store`; `content`, `nosniff` | 404 `Forms.Document.NotFound` |
| `PUT /documents/{id}` | `formrecords.read` y atender | `version` | 403 `Forms.Document.InboxForbidden`, 404, 409 `AlreadyResolved` y de versión |
| `POST /documents/{id}/reading` | lo mismo, y `assistant` | `version` | 403, 404, 409 `AlreadyResolved` y `BudgetExhausted` |
| `POST /documents/{id}/discard` | `formrecords.read` y atender | `version` | 403, 404, 409 `AlreadyResolved` |
| `POST /documents/{id}/record` | `formrecords.read` y atender | `{version}`, o el multipart de la E20 con `[RequestSizeLimit]` de 101 MiB; `[Idempotent(Mode = IdempotencyMode.DurableReference)]`, `[IdempotencyDurable(typeof(FormEntryDurableAdapter))]` y `[ResponseVersion("Version")]` | 400 `ValidationFailed`, 403, 404, 409 `AlreadyResolved` y `Duplicate`, 413 `Forms.Entry.TooLarge` y los de la captura (E14 y E20) |
| `POST /documents/{id}/form-proposal` | `forms.manage` y `formrecords.read`; `assistant` | — | 404, 409 `BudgetExhausted` |
| `POST /documents/{id}/form-drafts` | lo mismo | la propuesta, o `{templateId, name, prefix}`; `[Idempotent]` | 400 `Forms.Proposal.NameInUse`, 404 y `Forms.Template.NotFound`, 409 `Forms.Definition.PrefixTaken` |
| `GET /{id}/reading/stats` | `forms.read` | — | 404 |
| `POST /assistant-proposals` | `forms.read` y `forms.manage` | `[Idempotent]` | 400 `NameInUse` y `Forms.Draft.CatalogUnavailable`, 404 `Forms.Proposal.NotFound`, 409 `Replaced` y `PrefixTaken` |
| `POST /catalogs/assistant-proposals` | `formcatalogs.read` y `formcatalogs.manage` | `[Idempotent]` | 400 `NameInUse`, 404, 409 `Replaced` |
| `POST /catalogs/{id}/approval` | `formcatalogs.manage` | `version` | 409 `Forms.Catalog.NotProposed` y de versión; los de filtros de la E12 |
| `DELETE /catalogs/{id}?version=` | `formcatalogs.manage` | `version` | 409 `NotProposed` y de versión |
| `POST /audio` (asistente) | `assistant.use` | `requestId`; `[Idempotent(Mode = IdempotencyMode.DurableReference)]` con `AssistantAudioDurableAdapter` | 400 `Assistant.Audio.Unreadable` |
| `GET /audio/{requestId}` y `…/file` (asistente) | `assistant.use` | `file`, `no-store` y `nosniff` | 404 `Assistant.Audio.NotFound` |

Van en `BusinessFormDocumentsController`, `BusinessFormAuthoringController` (`assistant-proposals`), `BusinessFormCatalogsController` (E12), `BusinessFormsController` (E14, `reading/stats`) y `BusinessAssistantController` (audio).

**Rutas que cambian:**
- `PUT /catalogs/{id}` (E12) guarda uno por aprobar sin activarlo (`isActive: true` da 409 `Forms.Catalog.ApprovalPending`); el detalle suma `proposal?{requestedBy, channel, requestedAtUtc}`, y la lista, `isProposal`.
- `GET /{id}` y `PUT /{id}/draft` (E14) suman `reading?` y `autoLoad` (400 `Forms.Draft.ReadingInvalid`), y `FormDetail`, `autoLoadBlockedReason?`.
- El borrador de una plantilla y su vista (E22) suman `reading?`, con las validaciones fijas y su candado.
- El detalle de una respuesta (E15 y E21) suma `sourceDocument?{id, fileName, reviewedBy?}`.
- Asistente (E8b): `Message` suma `proposal?` y `audio?`; enviar, `audioRequestId?`, con 404 `Assistant.Audio.NotFound` y 409 `NotReady`; el adjunto, `.xlsx`, `.csv` y `.docx`.
- Un caso (E23 y E24) suma el tipo de entrada `Document`; el porqué de un documento sale por `GET …/conversations/{id}/actions/{actionId}`.

**Puertos:** los de la spec, en `Application/Interfaces/Integrations/StructuredData/`, con su apagado; `IStructuredFormChannel` suma botones, listas y el prellenado con documento.

**Campana**, como la E14, a quien tiene `formrecords.read`: `documentsToReview` (`{count}`, `link` `/org/formularios/documentos`, `id` `documentsToReview:{count}:{el más nuevo}`) y `documentsSuggestion` (`{count, topic}`, el mismo `link`, `id` `documentsSuggestion:{tema}:{count}`). Textos: «{count} documentos por revisar» / “{count} documents to review” y «Llegaron {count} documentos parecidos: {topic}» / “{count} similar documents arrived: {topic}”.

**Textos de error** (es / en):

| Código | es | en |
| --- | --- | --- |
| `Forms.Document.NotFound` | No encontramos ese documento. | We couldn't find that document. |
| `Forms.Document.AlreadyResolved` | Ese documento ya se confirmó o se descartó. | Already confirmed or discarded. |
| `Forms.Document.ValidationFailed` | Hay datos que no pasan las validaciones. | Some values don't pass the validations. |
| `Forms.Document.Duplicate` | Ya se cargó en {number} el {date}. | Already loaded in {number} on {date}. |
| `Forms.Document.BudgetExhausted` | Se llegó al tope de lectura; queda para revisar. | Reading limit reached; it stays for review. |
| `Forms.Document.InboxForbidden` | No atendés la bandeja de donde llegó. | You don't handle the inbox it came from. |
| `Forms.Draft.ReadingInvalid` | Revisá la lectura de documentos. | Check the document reading. |
| `Forms.Proposal.NotFound` | Esa propuesta ya no está disponible. | That proposal is no longer available. |
| `Forms.Proposal.Replaced` | Esa propuesta se reemplazó por una más nueva. | A newer proposal replaced it. |
| `Forms.Proposal.NameInUse` | Ya hay uno con ese nombre. | There's already one with that name. |
| `Forms.Catalog.NotProposed` | Ese catálogo no espera aprobación. | That catalog isn't waiting for approval. |
| `Forms.Catalog.ApprovalPending` | Para activarlo, usá «Aprobar y activar». | To activate it, use “Approve and activate”. |
| `Assistant.Audio.NotFound` | No encontramos ese audio. | We couldn't find that audio. |
| `Assistant.Audio.Unreadable` | No pudimos usar ese audio. Grabalo de nuevo. | We couldn't use that audio. Record it again. |
| `Assistant.Audio.NotReady` | El audio todavía se está pasando a texto. | The audio is still being transcribed. |
| `Assistant.Attachment.TooMany` (cambia) | Podés adjuntar hasta 5 imágenes o PDF, o un solo Excel, CSV o Word. | You can attach up to 5 images or PDFs, or a single Excel, CSV or Word file. |

**Texto para el chat de Contrato**

```text
Leé AGENTS.md de los dos repos, este plan y la spec de la E25. Hacé K0, con el Flow JSON y los pasos para el usuario, sin manejar su PC, y K1, con openapi.json y el aviso para npm run contracts. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español; nadie edita src con la app levantada; TDD; al terminar cada tarea, build y tests rápidos enteros, sin filtrar, más integración y seguridad del área si tocaste rutas, persistencia o aislamiento; el contrato cambia solo en «Cambios al contrato»; ninguna clave real; al usuario, solo lo de producto.
```

## Back

Cada tarea aplica Result, `OperationLog.RunAsync`, `[LoggerMessage]` sin contenidos, `TimeProvider`, resources es/en, una UoW por escritura, RLS y auditoría, con los tests de su fila en la spec y los que nombra. Migraciones: `<fecha>_FormDocuments.cs` (con `platform.BankEntities`), `<fecha>_AssistantAuthoring.cs` y `<fecha>_WhatsAppDocuments.cs`, con lo de la spec, los CHECK en una línea y un `Down` que se niega con datos; suman `FormDocuments.SourceRow?` y `SearchText`, y `WhatsAppAssistantActions.ProposalId?` y `Proposal`. Topes con `ValidateOnStart`: `Forms:DocumentReadsPerPersonPerDay`, `Forms:DocumentReadsPerOrganizationPerDay` (50, 217) y `Forms:DocumentMaxPdfPages` (10). Suman `FormDocuments.Read`, `UnreadReason`, `DiscardReason` y `ExpiresAtUtc`.

1. **B1 · Validaciones fijas y BCRA.** `D/Forms/FormFixedValidation.cs`, cerrado (`CbuCvu`, `TaxId`, `BankFromAccount`, `Amount` y `Date`), y `FormFixedValidationPolicy.cs`, con los textos de campo del tablero; `platform.BankEntities`, sembrada desde `I/Modules/Forms/ReferenceData/bcra-entidades-<fecha>.json`, que cita al BCRA, e `IFormBankEntityReader`. Copiá `ArgentineCuitValidator.cs`, `meta-rates-2026-10-01.json` e `IWhatsAppRateReader`. Tests: CBU y CVU; la entidad del prefijo; CUIT; `ReferenceDataHardcodeTests`.
2. **B2 · La lectura.** Suma a `FormDraft.cs`, `FormDraftEditor.cs`, `FormVersionCreator.cs` (sella `FormVersions.Reading`), `FormTemplateDraft` (`reading?`, mismo `format`), `FormTemplateCopyCreator` y `FormDefinitionCopyCreator`. `FormReadingPolicy.cs` valida también la plantilla al guardarla (`FormTemplateService`) y al publicarla (`FormTemplatePublicationPolicy`, con `issues[].path` y `changes[]`). `FormDefinitions.AutoLoad` no se sella, como `OfferWhen`, y los ejemplos suman, sin sellar, las correcciones de 30 días. Copiá `FormLogicPolicy.cs`. Tests: la plantilla; el candado; copiar lleva la lectura.
3. **B3 · Recibir y preparar.** `IStructuredDocumentSink.cs`, `FormDocumentReceiver.cs`, `IFormDocumentTextExtractor.cs` e `I/Modules/Forms/FormDocumentTextExtractor.cs`: `.docx` con `DocumentFormat.OpenXml` (la versión de ClosedXML), `.xlsx` y `.csv` por `IFormCatalogSpreadsheet` (E12), y el PDF con PDFsharp, que cuenta páginas y ve la contraseña: con `/Encrypt` queda sin clasificar con el motivo `Protected`, salvo el que solo restringe permisos, y el que se infla o vence su plazo da «ilegible», con los intentos contados. La copia va a `IFileStore` (`forms`) y se confirma después del commit; `FormDocuments` se suma a `ListReferencedStorageKeysAsync` (E18). Copiá `FileContentInspector.cs` (E18). Tests: cada formato; no se leen `.doc`, `.xls`, `.docm`, `.xlsm`, un CSV por WhatsApp, uno con contraseña ni un PDF de más de 10 páginas; la copia sigue a las 24 horas.
4. **B4 · Leer con la IA.** `A/Modules/Assistant/Services/AssistantDocumentReader.cs` implementa `IStructuredDocumentReader`, con el modelo calificado de `Assistant:Documents:ProviderModelId`; sin él, el documento queda sin leer. La foto y el PDF van como contenido, Word y Excel como texto y tablas, y se registra el hash y el tamaño. Copiá `ConversationAgentService.cs` (E23). La primera lectura guarda `Read`, y cambiar de formulario vuelve a extraer desde ahí. Tests: solo van el documento y las preguntas candidatas, nunca una corrección (216); instrucciones escondidas; la seguridad del modelo no decide.
5. **B5 · El circuito.** `FormDocumentService.cs` e `I/Modules/Forms/FormDocumentWorker.cs`, con `platform.FormDocumentWorks`; suma a `WhatsAppAssistantTurnService.cs` y `AssistantAgentPolicy.cs` (E23). En modo Asistente, primero van los casos «Documento» del asistente que contesta (E24), con el nombre, el tipo y el texto, sin el contenido. Sin un candidato que lea documentos, el archivo queda en el chat, sin IA. Con los dos topes del día (217), en el día UTC, queda en la bandeja sin leer, con el aviso fijo, y las cargas por archivo no gastan ofrecimientos. Si no aplica ningún caso, se clasifica entre los candidatos de `FormAvailabilityPolicy` (E17) para ese destinatario, también los solo para la app (`AppOnly`, E20): un documento de esos queda «Propuesto» en «Documentos recibidos», para «Pasar al formulario» desde la web, y la oferta por WhatsApp lo saltea (203). En atención manual, la oferta es una sugerencia al operador (199). La ubicación y el contacto, sin IA, completan el último formulario ofrecido y pendiente, que se vuelve a mandar, o quedan en el chat. Cada fila de una planilla es un documento (`SourceRow`), hasta 50 filas, con hasta 3 ofertas por mensaje y «1 de 3 · {resumen}»; una planilla con un pedido de armar va a B10 (221); un sin clasificar va a `FormMissingTopics` (E23). Fijos: la oferta (165), «¿Qué es este documento?» y «No reconocí este documento. ¿Qué querés cargar con él?», con «Otro»; lo demás lo redacta la IA con el validador de la E23. Copiá `FormActionWorker.cs` (E21). Tests: un caso «Documento» no lee el contenido; uno solo para la app queda en la bandeja y no se ofrece; los dos topes y su aviso; más de 50 filas; sin formularios que lean, no se lee.
6. **B6 · Proponer y confirmar.** `WhatsAppFlowBuilder.cs` suma `flow-static-7.3-r14`, sobre `r13`: una versión con lectura se publica con `r14`, y lo sellado no se recompila. El selector que recibe el documento lleva `visible` por un dato del envío, y en su lugar van «Adjuntamos tu documento» y «<nombre> · lo mandaste por WhatsApp», visibles por otro; lo dudoso dice «Revisá», y lo vacío, «No se leyó». En `FormRecordService.cs` (E14), con `StructuredRecordCapture.SourceDocumentId`, esa pregunta toma la copia, y se revisan otra vez las validaciones fijas y los duplicados. `SourceKey` es `document:<id>` en los tres caminos. Tests: un perfil anterior va sin el documento.
7. **B7 · La bandeja.** `FormDocumentQueryService.cs` e `I/Modules/Forms/Persistence/Readers/FormDocumentReader.cs`. Busca en SQL en `SearchText` (lo leído y sus dígitos: el monto, con o sin puntos), con `IX_FormDocuments_Search_SearchText`, como excepción en `paginado-y-busqueda.md` y `SearchIndexTests`. Fuentes: `FormDocumentsToReviewNotificationSource` y `FormDocumentsSuggestionNotificationSource`. Copiá `FormRecordReader.cs` (E15). Tests: EXPLAIN con 10.000 documentos.
8. **B8 · Revisar.** Suma a `FormDocumentService.cs`; `IStructuredInboxAccess.cs` y `WhatsAppStructuredInboxAccess.cs`; corregir escribe `FormDocumentCorrections`. `FormEntryDurableAdapter` (E20) suma el prefijo `document:` y su dueño, quien revisó, con su permiso; `FormEntrySubmitService`, el canal `whatsapp`, quien mandó el documento como respondente y quién revisó. Pasar un repetido da 409 `Forms.Document.Duplicate` sin `confirmRepeated`; «Volver a abrir» (`reopen`) un descartado, también uno vencido. Copiá `FormRecordCorrectionService.cs` (E21). Tests: el reintento y el permiso perdido; el repetido.
9. **B9 · Armar.** `FormProposalService.cs`, `IStructuredAuthoring.cs`, `IAssistantProposalLookup.cs`, `FormAuthoringOutcomes` y las rutas de crear; en `A/Modules/Assistant/Tools/Business/`, `read_forms`, `read_catalogs`, `propose_form`, `propose_catalog` y `decline_authoring`. Arma un modelo calificado para armar (218): en el Inicio, `Assistant:Authoring:ProviderModelId`, con la clave que paga el turno; por WhatsApp, `Assistant:Channel:ProviderModelId` (E23). `catalogImport` propone importar en un catálogo que existe, y el import de la E12 suma `source: {turnId, proposalId}` (222). Un prefijo mal escrito se normaliza; con un documento de ejemplo, la propuesta lleva `reading`; una propuesta que sola no entra da `ProposalTooLarge`. «Se parece a una plantilla»: el modelo elige entre las publicadas (`FormTemplateLibraryService`) y Forms la verifica; «Usar la plantilla» crea por `form-drafts` con `FormTemplateCopyCreator`. Al publicar (`CompleteAsync` o `ResolveAsync` de la E14, o solo para la app, de la E20), los documentos con `BuiltDefinitionId` se vuelven a leer y quedan «Propuesto». Copiá `FormDefinitionCopyCreator.cs` (E17) y `OrganizationUsersTool`. Tests: los de «Lo que ya se pagó allá»; importar en uno que existe no importa; el modelo con la clave propia.
10. **B10 · Armar por WhatsApp.** `IStructuredAuthoringCreator.cs` y su implementación en Forms, solo con un vínculo `Linked` (E21). El resumen y sus botones, cada uno con su propuesta, son una acción de la E23; vale la última. «Cambiar algo» contesta «Decime qué cambio, por escrito o con un audio.». Crear revisa antes el chat y, si no crea, contesta el motivo (`Forms.Proposal.ChatUnavailable`); la pregunta de un pedido vago no gasta cupos. Copiá la conversación de la E23. Tests: un vínculo `Unlinked` no arma; el chat que pasó a manual no crea y lo dice; una planilla con pedido arma.
11. **B11 · El catálogo propuesto.** Suma a `FormCatalog.cs` (`ProposalState`, `Proposal`, `Approve` y `DiscardProposal`) y a `IFormCatalogService` (E12). `formcatalogs.manage` toma en `Permissions.resx` y `.en.resx` su texto final, el de la §2 de la hoja de ruta («…y aprobar los que propone el asistente»). Tests: aprobar devuelve mapas vacíos; `PermissionTextsTests`.
12. **B12 · El chat del Inicio.** Suma a los turnos, los adjuntos y el historial de Assistant: `proposal`, `audioRequestId` y los tres tipos, de a uno, con `forms.manage` o `formcatalogs.manage`; `AssistantAudioService.cs`, `AssistantAudios`, `AssistantAudioDurableAdapter` y `AuthoringMaxOutputTokens`. El audio se confirma después del commit, y el micrófono depende de `IFileStorageStatus` (E18) y del transcriptor. Sin cupo no se guarda el audio, el mismo `requestId` con el mismo archivo da la misma transcripción, y borrar la conversación borra sus audios y lecturas (223, 0064 §2, §4 y §5). Copiá `AssistantAttachmentService.cs`. Tests: WebM y MP4 por sus bytes; el audio sigue a las 24 horas; sin cupo; dos veces; borrar.
13. **B13 · Cargar solo si es seguro.** `FormAutoLoadPolicy.cs`: las tres señales y el aviso fijo con el número. Copiá la captura de `FormRecordService.cs`.
14. **B14 · Precisión, retención y baja.** `FormDocumentRetentionWorker.cs`, con `TenantJobRunner`, en lotes y también al arrancar: a los 90 días, sin tocar la copia de una respuesta ni un archivo de otra fila, y lo propuesto sin revisar vence con «Nadie lo revisó en 90 días: venció.» (215). «Ver los ejemplos» (`GET /{id}/reading/examples`) y la precisión, de 30 días. Suma a `FormsAccountDeletionParticipant.cs` y, por los audios, a `AssistantAccountDeletionParticipant.cs`. Copiá `InvitationExpiryWorker.cs`.
15. **B15 · Rutas y cierre.** Los controllers de K1, `Contracts/*` y `FormsContractContributor.cs`; suma a `TenantScopeUsageTests`, `ExplicitRouteInventoryTests` y `ModuleIsolationTests`, y los artículos de ayuda (D25 de la 0048). Tests: 401, 403, feature apagada (404) y 409 en cada ruta; aislamiento; ejemplos exportados.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan y la spec de la E25 con sus fichas. Después de K1, hacé B1 a B15 en orden, copiando los moldes; no toques el front ni la tabla de estado. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español; nadie edita src con la app levantada; TDD; al terminar cada tarea, build y tests rápidos enteros, sin filtrar, más integración y seguridad del área si tocaste rutas, persistencia o aislamiento; el contrato cambia solo en «Cambios al contrato»; ninguna clave real; al usuario, solo lo de producto.
```

## Front

Cada pantalla copia su tablero de la v169 y sus textos, con i18n, tipos generados, `shared/format`, TanStack Query y axe, y los tests de su fila en la spec y los que nombra.

1. **F1 · Tipos y llamadas.** `src/modules/forms/documents/api.ts` y `types.ts`, más lo nuevo del asistente y los catálogos. Copiá la API de la E21. Tests: zod estricto con los ejemplos.
2. **F2 · La bandeja.** `/org/formularios/documentos`: `documents/pages/DocumentsPage.tsx`, `components/DocumentPanel.tsx` y `DocumentPreview.tsx`. Con un bloque con mínimo, «Pasar al formulario» abre `LoadResponsePage.tsx` (E20) con lo leído, que suma el destino `POST /documents/{id}/record`, su confirmación y su «Enviada». Suma «Venció», «Protegido», el tope del día y pasar un repetido. Tablero: `FRM-Documentos` y su teléfono. Tests: lo privado fuera de la dirección; el repetido.
3. **F3 · La lectura.** `documents/components/DocumentReadingSection.tsx` y `ReadingFields.tsx`, en el editor, en la plantilla y en su vista, con «Lo escribió la IA» y «Ver los ejemplos» solo con `formrecords.read`. Tableros: `FRM-Editor-Lectura`, `Plat-FRM-Plantilla-Lectura` y `FRM-Plantilla-25`, con sus teléfonos.
4. **F4 · Armar con la IA.** `/org/formularios/documentos/:id/armar`: `documents/pages/BuildFormPage.tsx`, con «Se parece a una plantilla» y «Usar la plantilla», que pide nombre y prefijo como la E22. Tablero: `FRM-Armar-Con-IA` y su teléfono. Tests: crear, la plantilla, el bloque y la falla.
5. **F5 · La tarjeta, el clip, el micrófono y las entradas.** `ModuleRegistration` suma `assistantProposal`, que Forms llena con `forms/assistant/ProposalCard.tsx` y `ProposalPreviewDialog.tsx`. En `src/modules/assistant`, `components/AudioRecorder.tsx`, y suma a `AttachmentComposer.tsx`, `AssistantThread.tsx` y `lib/assistantFiles.ts`. «Armar con la IA», con `assistant`, `assistant.use` y el permiso de armar, abre el Inicio con el pedido en el estado de la navegación. El Inicio vacío suma las dos sugerencias del tablero. Tableros: `Asistente-Org-25` y su teléfono, `FRM-Formularios-25`, `FRM-Catalogos-25` y `FRM-Faltan-25`. Suman la tarjeta de importar en uno que existe, «Leyendo el documento…» y «Lee documentos», la propuesta que no entra y borrar con sus audios. Tests: un `.xls` dice por qué no se manda; «Revisar la importación» abre el import.
6. **F6 · El catálogo propuesto.** Suma a `CatalogEditorPage` (E12). Tablero: `FRM-Catalogo-Propuesta-25`.
7. **F7 · El chat, el detalle y la baja.** Suma a `MessageThread.tsx` («Por qué lo propuso» y «Por qué preguntó»), `RecordValues.tsx`, `QuickView.tsx` y `RecordFullPage.tsx`. En `LifecycleDialogs.tsx` (E17), «Qué deja de pasar» suma que ya no se le proponen documentos y conserva «No se carga más desde la app.» (E20). Tableros: `WA-Chats-Documento` y `FRM-Respuesta-25`, con sus teléfonos.
8. **F8 · Los Casos y la campana.** La pantalla «Asistente» (E23 y E24) suma el tipo «Documento» y el bloque fijo «Armar formularios y catálogos». Los textos de los dos avisos van con `notifications` en `registration.ts`. Tableros: `WA-Asistente-Casos-25` y `Campana-25`, con su teléfono. Tests: el bloque no se edita; los avisos, con y sin `formrecords.read`.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, este plan (../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-25-documentos.md) y la spec de la E25. Después de K1 y npm run contracts, hacé F1 a F8 contra los tipos generados, copiando los tableros de la v169; assistant no importa forms. No toques el lienzo, las specs, el back ni la tabla de estado. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español; nadie edita src con la app levantada; TDD; al terminar cada tarea, npx tsc -b y npm test enteros, sin filtro, más integración y seguridad del área si tocaste permisos o aislamiento; el contrato cambia solo en «Cambios al contrato»; ninguna clave real; al usuario, solo lo de producto.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real; lo que no encaja vuelve a su dueño. **Pendiente para la I1 de la 10:** la prueba real con la IA y Meta de la spec, con `M-WA-Flow-Prellenado` y `M-WA-Armar-Con-IA` en un celular; la nube y el antivirus.
2. **I2 · Calificar.** Antes de la puerta, los perfiles de leer y de armar, con los casos ficticios de la spec: `FakeChatClient` en los tests, y el proveedor real en una corrida que habilita el usuario.
3. **I3 · Puerta.** `/puerta` una vez, sobre copias del corte: build sin advertencias y `dotnet test`; front `build`, `lint`, `test` y `contracts:check`; el E2E real con los tres recorridos de la 25; `realinear-docs --detectar` en 0; la galería de lo nuevo o cambiado, a 1440, 390 y 320, con `M-WA-Flow-Prellenado` contra su cuarta pantalla; `/recorrido` y la lista de control. Al cerrar nacen las secciones de `formularios.md` y `asistente.md`, la tabla de `datos-de-referencia.md` y los `AGENTS.md` nuevos.
4. **I4 · Recorrido.** La prueba de la spec, con Meta simulado: los tres reconocimientos, un CBU mal leído, un repetido y una corrección; en el Inicio, el bloque con un audio y el catálogo con un Excel, aprobado; por WhatsApp, el resumen, «Cambiar algo», un botón viejo y crear.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan y /puerta. Integrá contra K1, devolvé cada diferencia a su dueño y corré la puerta una vez sobre copias del corte; acá va solo su tabla. Meta con un número conectado y la nube quedan para la I1 de la 10; al usuario, pasos clic por clic, sin manejar su PC. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español; nadie edita src con la app levantada; TDD; cada arreglo, con build y tests rápidos enteros de su repo, más integración y seguridad del área si tocó rutas, persistencia o aislamiento; el contrato cambia solo en «Cambios al contrato»; ninguna clave real; al usuario, solo lo de producto.
```

## Relación con otras etapas

**Usa de las anteriores** (la lista cerrada de K0):
- **Base:** `INotificationSource`, con el sufijo `NotificationSource`, y `notifications` en `registration.ts`.
- **E12:** `FormCatalog`, `IFormCatalogService`, `IFormCatalogImportService`, `FormCatalogFiltersPolicy`, `IFormCatalogSpreadsheet` y sus topes, `IFormWorkLease` (el carril `Catalogs` de la E15, al importar), `BusinessFormCatalogsController`, `CatalogEditorPage` e `ImportDialog`.
- **E13:** `FormCatalogField` y `FieldValuesJson`, para el cruce.
- **E14:** `FormDraft`, `FormDraftEditor`, `FormVersionCreator`, `FormPublicationService` (`CompleteAsync` y `ResolveAsync`), `FormRecordService`, `FormRecordSink`, `FormStructuredSource`, `WhatsAppFlowBuilder`, `IStructuredFormChannel` con `Check`, `StructuredRecordCapture`, `WhatsAppFlowSessions`, `WhatsAppFlowBindings.Profile`, `FormRecords.SourceKey`, `FormDetail`, `BusinessFormsController`, `Forms.Definition.PrefixTaken`, `Forms.Draft.CatalogUnavailable`, `FormsAccountDeletionParticipant` y `FormsContractContributor`.
- **E15:** `FormRecordReader`, `BusinessFormRecordsController`, los filtros en el cuerpo, `SearchIndexTests`, `RecordValues.tsx`, `QuickView.tsx` y `RecordFullPage.tsx`.
- **E16:** los filtros por destinatario de `FormOfferBuilder`.
- **E17:** `FormAvailabilityPolicy`, `FormDefinitionCopyCreator` y `LifecycleDialogs.tsx`.
- **E18:** `IFileContentInspector`, `FileContentInspector`, `IFileStorageStatus`, `WhatsAppChatFile`, `WhatsAppChatFileService`, `StructuredRecordCapture.Files`, `FormRecordFiles`, `ListReferencedStorageKeysAsync` y el perfil `r10`.
- **E19:** `FormLogicPolicy`, el bloque (`rows`) y el perfil `r11`.
- **E20:** `POST /{id}/records` multipart, `FormEntrySubmitService`, `FormEntryDurableAdapter`, `LoadResponsePage.tsx`, `FormVersions.AppOnly`, `Forms.Entry.TooLarge` y la línea de «Qué deja de pasar».
- **E21:** `platform.FormActionWorks`, `FormActionWorker`, `FormRecordCorrectionService` y `WhatsAppConversationLink.State` (`Linked`).
- **E22:** `FormTemplateDraft` (`format: 1`, con `titles[]`), `FormTemplateCopyCreator`, `FormTemplateService`, `FormTemplatePublicationPolicy`, `FormTemplateLibraryService`, `FormDefinitions.SourceTemplateId` y `SourceTemplateVersion`, y las rutas de plantillas.
- **E23:** `IConversationAgent`, `ConversationTurn`, `ConversationAgentService`, `AssistantChannelContext`, `WhatsAppAssistantTurnService`, `AssistantAgentPolicy`, el tipo de entrada de un caso, `WhatsAppAssistantActions` y su ruta del porqué, las sugerencias al operador, el perfil `r13`, la oferta, `Assistant:Channel:ProviderModelId`, `ISpeechTranscriber` (0055), `FormMissingTopics` y `MissingFormsPage.tsx`.
- **E24:** `ConversationTurn.AgentId`, `WhatsAppAgentResolver` y `/org/whatsapp/asistentes/:assistantId`, con sus casos.

**Deja para las siguientes:** cierra el bloque; quedan `FormDocuments`, `platform.BankEntities`, sus seis puertos, el perfil `r14`, `assistantProposal`, la 0056 y la 0057, y lo pendiente de I1, para la I1 de la 10.

## Cambios al contrato


## Para decidir

Nada. La P4, un documento que se reconoce como un formulario solo para la app, la resolvió el usuario con la decisión 203: queda en «Documentos recibidos» y no se ofrece por WhatsApp (B5).
