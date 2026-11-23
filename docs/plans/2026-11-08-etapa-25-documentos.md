# E25 · Documentos por WhatsApp · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/25-documentos.md`, con `25-parte-datos-y-contrato.md` y `25-parte-pasos.md`. Tableros: los 24 de la 25, aprobados en el lienzo v175, salvo los que la spec da como «Se redibujan» y los tres nuevos de la configuración, que se aprueban antes de F2. ADR [0056](../decisions/0056-lectura-de-documentos-con-ia.md), [0057](../decisions/0057-la-ia-propone-y-una-persona-crea.md), [0064](../decisions/0064-reglas-de-toda-la-ia.md) y [0068](../decisions/0068-acciones-del-asistente.md), y las decisiones 215 a 223 del traspaso 15 y 261 a 264, 266 a 272 y 275 a 278 de los traspasos 16 a 18. Antes de programar: la E24 cerrada y la prueba con Meta de K0.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0, K1 | Pendiente |
| Back | B1–B16 | Pendiente |
| Front | F1–F8 | Pendiente |
| Integración y puerta | I1–I4 | Pendiente |

## Forma de trabajo

Contrato primero; Back y Front en paralelo; una sola puerta al final. Un arreglo corre solo lo que toca, y la tabla de estado la actualiza la Guía.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [Meta] Archivo precargado | `PhotoPicker` y `DocumentPicker` «cannot be initialized using Form `init-values`», no entran en `If` ni en `Switch` y admiten `visible` con `${data…}` ([media_upload](https://developers.facebook.com/docs/whatsapp/flows/reference/media_upload), [components](https://developers.facebook.com/docs/whatsapp/flows/reference/components)) | B6, sin «Cambiar documento» (cuarta pantalla de `M-WA-Flow-Prellenado`); la prueba, en K0, y lo que Meta no devuelve (O38), en B6 |
| [Meta] Botones y listas | 3 botones de 20 caracteres; 10 filas de 24, con descripción de 72 ([botones](https://developers.facebook.com/docs/whatsapp/cloud-api/messages/interactive-reply-buttons-messages), [listas](https://developers.facebook.com/docs/whatsapp/cloud-api/messages/interactive-list-messages)) | Más de 24 en una fila: «…», y entero en la descripción (11) |
| ADR | La 0022 y la 0024 de allá (`origin/main`, `ae60b950`) | La 0056 y la 0057, que precisa la D14 de la 0048 |
| Asistente | `AssistantMessageResponse` sin `proposal` ni `audio`; `done` con `AssistantTurnResultResponse`; clip de imágenes y PDF, 10 MiB, hasta 5; 5 llamadas, 2048 de salida y 51200 de entrada | El turno que arma usa `AuthoringMaxOutputTokens` (8192) y reserva con él (0051) |
| Bibliotecas | ClosedXML 0.105.1 (con `DocumentFormat.OpenXml`) y PDFsharp-MigraDoc 6.2.4 | Ninguna nueva (B3) |
| Parecidos | — | Tres o más sin clasificar de 30 días con el tema de la clasificación; el número, de la configuración (270) |

## Contrato

### K0 · Lo que dejó la etapa anterior

El chat del contrato confirma en el código «Usa de las anteriores», `AssistantProposals` y `Message.proposal?` de «Acciones del asistente», el alcance de solo lectura con la entrada de canal de la E23, que `r13` (E23) es el último perfil y que OpenAI (0055) acepta WebM y MP4; lo que difiera va a «Cambios al contrato» antes de programar.

**La prueba con Meta, del usuario:** el chat deja en `.local/` el Flow JSON de prueba y le pasa los pasos de «Qué verificar al arrancar» de la [parte de pantallas](formularios/25-parte-pantallas-y-verificar.md#qué-verificar-al-arrancar).

### K1 · Publicar antes de programar las pantallas

El Back publica rutas con 501, cuerpos, textos y ejemplos exportados estrictos.

El acceso, las features y las cuotas, como la [parte de datos y contrato](formularios/25-parte-datos-y-contrato.md#contrato). «Atender» es atender la bandeja del documento (`IStructuredInboxAccess`).

| Ruta | Permiso | Idempotencia y versión | Errores |
| --- | --- | --- | --- |
| `POST /documents/search` y `/facets` | `formrecords.read` | — | 400 |
| `GET /documents/{id}` y `…/content` | `formrecords.read` | `no-store`; `content`, `nosniff` | 404 `Forms.Document.NotFound` |
| `PUT /documents/{id}` | `formrecords.read` y atender | `version` | 403 `Forms.Document.InboxForbidden`, 404, 409 `AlreadyResolved` y de versión |
| `POST /documents/{id}/reading` | lo mismo, y `assistant` | `version` | 403, 404, 409 `AlreadyResolved` y `BudgetExhausted` |
| `POST /documents/{id}/discard` y `…/reopen` | `formrecords.read` y atender | `version` | 403, 404 (también si ya se borró), 409 `AlreadyResolved` al descartar y `Forms.Document.NotDiscarded` al volver a abrir |
| `POST /documents/{id}/record` | `formrecords.read` y atender | `{version}`, o el multipart de la E20 con `[RequestSizeLimit]` de 101 MiB; `[Idempotent(Mode = IdempotencyMode.DurableReference)]`, `[IdempotencyDurable(typeof(FormEntryDurableAdapter))]` y `[ResponseVersion("Version")]` | 400 `ValidationFailed`, 403, 404, 409 `AlreadyResolved` y `Duplicate`, 413 `Forms.Entry.TooLarge` y los de la captura (E14 y E20) |
| `POST /documents/{id}/form-proposal` | `forms.manage` y `formrecords.read`; `assistant` | — | 404, 409 `BudgetExhausted` |
| `POST /documents/{id}/form-drafts` | lo mismo | la propuesta, o `{templateId, name, prefix}`; `[Idempotent]` | 400 `Forms.Proposal.NameInUse`, 404 y `Forms.Template.NotFound`, 409 `Forms.Definition.PrefixTaken` |
| `GET /{id}/reading/stats` | `forms.read` | — | 404 |
| `GET /{id}/reading/examples` | `forms.read` y `formrecords.read` | paginado | 404 |
| `POST /assistant-proposals` | `forms.read` y `forms.manage` | `[Idempotent]` | 400 `NameInUse` y `Forms.Draft.CatalogUnavailable`, 404 `Forms.Proposal.NotFound`, 409 `Replaced` y `PrefixTaken` |
| `POST /catalogs/assistant-proposals` | `formcatalogs.read` y `formcatalogs.manage` | `[Idempotent]` | 400 `NameInUse`, 404, 409 `Replaced` |
| `POST /catalogs/{id}/approval` | `formcatalogs.manage` | `version` | 409 `Forms.Catalog.NotProposed` y de versión; los de filtros de la E12 |
| `DELETE /catalogs/{id}?version=` | `formcatalogs.manage` | `version` | 409 `NotProposed` y de versión |
| `POST /audio` (asistente) | `assistant.use` | `requestId`; `[Idempotent(Mode = IdempotencyMode.DurableReference)]` con `AssistantAudioDurableAdapter` | 400 `Assistant.Audio.Unreadable` |
| `GET /audio/{requestId}` y `…/file` (asistente) | `assistant.use` | `file`, `no-store` y `nosniff` | 404 `Assistant.Audio.NotFound` |

Van en `BusinessFormDocumentsController`, `BusinessFormAuthoringController` (`assistant-proposals`), `BusinessFormCatalogsController` (E12), `BusinessFormsController` (E14, `reading/stats`) y `BusinessAssistantController` (audio).

**Rutas que cambian:** en la [parte de datos y contrato](formularios/25-parte-datos-y-contrato.md#contrato), todas juntas.

**Puertos:** los de la spec, en `Application/Interfaces/Integrations/StructuredData/`, con su apagado; `IStructuredFormChannel` suma botones, listas y el prellenado con documento.

**Campana:** sus dos avisos, con su `id` y sus textos, en la [parte de datos y contrato](formularios/25-parte-datos-y-contrato.md#contrato).

**Textos de error** (es / en): en la [parte de datos y contrato](formularios/25-parte-datos-y-contrato.md#contrato), debajo de sus errores.

**Texto para el chat de Contrato**

```text
Leé AGENTS.md de los dos repos, este plan y la spec de la E25, con las decisiones 261 a 264, 266 a 272 y 275 a 278 de la hoja de ruta. Hacé K0, con el Flow JSON y los pasos para el usuario, sin manejar su PC, y K1, con openapi.json y el aviso para npm run contracts. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español; nadie edita src con la app levantada; TDD; al terminar cada tarea, build y tests rápidos enteros, sin filtrar, más integración y seguridad del área si tocaste rutas, persistencia o aislamiento; el contrato cambia solo en «Cambios al contrato»; ninguna clave real; al usuario, solo lo de producto.
```

## Back

Cada tarea aplica Result, `OperationLog.RunAsync`, `[LoggerMessage]` sin contenidos, `TimeProvider`, resources es/en, una UoW por escritura, RLS y auditoría, con los tests de su fila en la spec y los que nombra. Migraciones: `<fecha>_FormDocuments.cs` (con `platform.BankEntities`), `<fecha>_AssistantAuthoring.cs` y `<fecha>_WhatsAppDocuments.cs`, con lo de la spec, los CHECK en una línea y un `Down` que se niega con datos; suman `FormDocuments.SourceRow?`, `SearchText`, `Read`, `UnreadReason`, `DiscardReason` y `ExpiresAtUtc`, `WhatsAppAssistantActions.ProposalId?` y `Proposal`, y en `AssistantProposals`, `Content`, sus clases, los nulos con su CHECK y el índice por `(ConversationId, Kind)`; cada una anota la excepción de la 0060 de la spec. Topes con `ValidateOnStart`: `Forms:DocumentReadsPerPersonPerDay` y `Forms:DocumentReadsPerOrganizationPerDay` (50, 217); los seis valores de la configuración, en B14 (270 y 272). Todo contador diario corta a la medianoche de la organización (271).

1. **B1 · Validaciones fijas y BCRA.** `D/Forms/FormFixedValidation.cs`, cerrado (`CbuCvu`, `TaxId`, `BankFromAccount`, `Amount` y `Date`), y `FormFixedValidationPolicy.cs`, con los textos de campo del tablero; `platform.BankEntities`, sembrada desde `I/Modules/Forms/ReferenceData/bcra-entidades-<fecha>.json`, que cita al BCRA, e `IFormBankEntityReader`. Copiá `ArgentineCuitValidator.cs`, `meta-rates-2026-10-01.json` e `IWhatsAppRateReader`. Cada validación sabe su forma: lo leído que no la tiene queda sin leer, y lo que la tiene y no pasa, marcado (277). Tests: CBU y CVU; la entidad del prefijo; CUIT; una cuenta de débito queda sin leer; `ReferenceDataHardcodeTests`.
2. **B2 · La lectura.** Suma a `FormDraft.cs`, `FormDraftEditor.cs`, `FormVersionCreator.cs` (sella `FormVersions.Reading`), `FormTemplateDraft` (`reading?`, mismo `format`), `FormTemplateCopyCreator` y `FormDefinitionCopyCreator`. `FormReadingPolicy.cs` valida también la plantilla al guardarla (`FormTemplateService`) y al publicarla (`FormTemplatePublicationPolicy`, con `issues[].path` y `changes[]`). `FormDefinitions.AutoLoad` no se sella, como `OfferWhen`, y los ejemplos suman, sin sellar, las correcciones de 30 días. Copiá `FormLogicPolicy.cs`. Tests: la plantilla; el candado; copiar lleva la lectura.
3. **B3 · Recibir y preparar.** `IStructuredDocumentSink.cs`, `A/Modules/Forms/Services/FormStructuredDocumentSink.cs`, `IFormDocumentTextExtractor.cs` e `I/Modules/Forms/FormDocumentTextExtractor.cs`: `.docx` con `DocumentFormat.OpenXml`, `.xlsx` y `.csv` por `IFormCatalogSpreadsheet` (E12), y el PDF con PDFsharp, que cuenta páginas y ve la contraseña: con `/Encrypt` queda sin clasificar, motivo `Protected`, salvo si solo restringe permisos; el que se infla o vence su plazo da «ilegible», con los intentos contados. La copia va a `IFileStore` (`forms`) y se confirma después del commit; `FormDocuments` se suma a `ListReferencedStorageKeysAsync` (E18). Copiá `FileContentInspector.cs` (E18). Tests: los de su fila en la spec, con `.docm`, `.xlsm` y un CSV por WhatsApp, que no se leen; la copia sigue a las 24 horas.
4. **B4 · Leer con la IA.** `A/Modules/Assistant/Services/AssistantDocumentReader.cs` implementa `IStructuredDocumentReader`, con el modelo calificado de `Assistant:Documents:ProviderModelId`; sin él, el documento queda sin leer. La foto y el PDF van como contenido, Word y Excel como texto y tablas; se registran hash y tamaño. Copiá `ConversationAgentService.cs` (E23). La primera lectura guarda `Read`; cambiar de formulario vuelve a extraer desde ahí. Tests: solo van el documento y las preguntas candidatas, nunca una corrección (216); instrucciones escondidas; la seguridad del modelo no decide.
5. **B5 · El circuito.** `FormDocumentService.cs` e `I/Modules/Forms/FormDocumentWorker.cs`, con `platform.FormDocumentWorks`; suma a `WhatsAppAssistantTurnService.cs` y `AssistantAgentPolicy.cs` (E23). En modo Asistente van primero los casos «Documento» del asistente que contesta (E24), con nombre, tipo y texto, sin el contenido. Sin un candidato que lea documentos, el archivo queda en el chat, sin IA. Con los dos topes del día (217), queda en la bandeja sin leer, con el aviso fijo; las cargas por archivo no gastan ofrecimientos. Si no aplica ningún caso, se clasifica entre los candidatos del filtro de disponibilidad de la E17 (`FormAvailabilityQueryFilters` en el reader y `FormAvailabilityPolicy` en memoria) para ese destinatario, incluidos los `AppOnly` (E20), que quedan en la bandeja sin ofrecerse (203); en atención manual, la oferta es una sugerencia al operador (199). La ubicación que contesta un pedido pendiente de la E20 la toma la E20 (230); sin pedido, la ubicación y el contacto completan, sin IA, el último formulario ofrecido y pendiente, que se vuelve a mandar, o quedan en el chat. Cada fila de una planilla, hasta 50, es un documento (`SourceRow`), con tantas ofertas por mensaje como «Formularios por planilla» y «1 de 3 · {resumen}»; una planilla con un pedido de armar va a B10 (221), y un sin clasificar, a `FormMissingTopics` (E23). Fijos: la oferta (165), «¿Qué es este documento?» y «No reconocí este documento. ¿Qué querés cargar con él?», con «Otro»; lo demás lo redacta la IA con el validador de la E23. Copiá `FormActionWorker.cs` (E21). Tests: un caso «Documento» no lee el contenido; uno `AppOnly` queda en la bandeja sin ofrecerse; la ubicación con un pedido pendiente de la E20 no se toma acá; los dos topes y su aviso; más de 50 filas; sin formularios que lean, no se lee.
6. **B6 · Proponer y confirmar.** `WhatsAppFlowBuilder.cs` suma `flow-static-7.3-r14`, sobre `r13`: una versión con lectura se publica con `r14`; lo sellado no se recompila. El selector que recibe el documento lleva `visible` por un dato del envío, y en su lugar van, visibles por otro, «Adjuntamos tu documento» y «<nombre> · lo mandaste por WhatsApp»; lo dudoso dice «Revisá», y lo vacío, «No se leyó». En `FormRecordService.cs` (E14), con `StructuredRecordCapture.SourceDocumentId`, esa pregunta toma la copia, que cuenta antes de exigir su mínimo porque Meta no devuelve el archivo (O38), y se revisan de nuevo las validaciones fijas y los duplicados. `SourceKey` es `document:<id>` en los tres caminos. Tests: un perfil anterior va sin el documento; una pregunta de archivo obligatoria y una respuesta sin archivo.
7. **B7 · La bandeja.** `FormDocumentQueryService.cs` e `I/Modules/Forms/Persistence/Readers/FormDocumentReader.cs`. Busca en SQL en `SearchText` (lo leído y sus dígitos: el monto, con o sin puntos), con `IX_FormDocuments_Search_SearchText`, como excepción en `paginado-y-busqueda.md` y `SearchIndexTests`. Fuentes: `FormDocumentsToReviewNotificationSource` y `FormDocumentsSuggestionNotificationSource`. `sort` por columnas (264), y la sugerencia, desde el número de la configuración. Copiá `FormRecordReader.cs` (E15). Tests: EXPLAIN con 10.000 documentos.
8. **B8 · Revisar.** Suma a `FormDocumentService.cs`; `IStructuredInboxAccess.cs` y `WhatsAppStructuredInboxAccess.cs`; corregir escribe `FormDocumentCorrections`. `FormEntryDurableAdapter` (E20) suma el prefijo `document:` y su dueño, quien revisó, con su permiso; `FormEntrySubmitService`, el canal `whatsapp`, quien mandó el documento como respondente y quién revisó. Pasar un repetido sin `confirmRepeated` da 409 `Forms.Document.Duplicate`; «Volver a abrir» (`reopen`) sirve para un descartado, también vencido. Copiá `FormRecordCorrectionService.cs` (E21). `FormApprovalService` y la cola de «Por aprobar» (E21) dejan afuera a quien pasó la respuesta, como al autor y a quien la corrigió: `FormApprovalTasks` suma `PassedByUserId?`, guardado en la tarea (238); `RoleWithoutMembers` tampoco lo cuenta (239), salvo con «Puede decidir quien cargó» (276), y `Forms.Approval.OwnRecord` suma «o pasaste desde un documento». La actividad suma «La pasó desde un documento» (267). Tests: el reintento y el permiso perdido; el repetido; quien la pasó no la decide (403) ni la ve en la cola; un rol cuyo único miembro la pasó da `RoleWithoutMembers`.
9. **B9 · Armar.** `FormProposalService.cs`, `IStructuredAuthoring.cs`, `IAssistantProposalLookup.cs`, `FormAuthoringOutcomes` y las rutas de crear; en `A/Modules/Assistant/Tools/Business/`, `read_forms`, `read_catalogs`, `propose_form`, `propose_catalog` y `decline_authoring`. Arma el modelo calificado para armar, con la clave que dice la spec (218), y las herramientas corren en el alcance de solo lectura, como dice la spec. `propose_form` nunca propone una Ubicación (230). `catalogImport` propone importar en un catálogo existente, y el import de la E12 suma `source: {turnId, proposalId}` (222). «Se parece a una plantilla»: el modelo elige entre las publicadas (`FormTemplateLibraryService`) y Forms la verifica; «Usar la plantilla» crea por `form-drafts` con `FormTemplateCopyCreator`. Al publicar (`CompleteAsync` o `ResolveAsync` de la E14, o solo para la app, de la E20), los documentos con `BuiltDefinitionId` se releen y quedan «Propuesto», dentro de «Relecturas por persona y día» de quien lo armó; los demás esperan con `RereadLimit` (270 y 271). La propuesta es una clase de `AssistantProposals`: el reemplazo es por clase, y `applied` y `unavailable` salen de Forms (268). Copiá `FormDefinitionCopyCreator.cs` (E17) y `OrganizationUsersTool`. Tests: los de «Lo que ya se pagó allá»; importar en uno existente no importa; el modelo con la clave propia; `form-proposal` con el modelo de armar; una Ubicación propuesta vuelve al modelo como inválida.
10. **B10 · Armar por WhatsApp.** `IStructuredAuthoringCreator.cs` y su implementación en Forms, solo con un vínculo `Linked` (E21). El resumen y sus botones, cada uno con su propuesta, son una acción de la E23, y vale la última. «Cambiar algo» contesta «Decime qué cambio, por escrito o con un audio.». Crear revisa antes el chat y, si no crea, contesta el motivo (`Forms.Proposal.ChatUnavailable`); preguntar por un pedido vago no gasta cupos. Copiá la conversación de la E23. Tests: un vínculo `Unlinked` no arma; el chat que pasó a manual no crea y lo dice; una planilla con pedido arma.
11. **B11 · El catálogo propuesto.** Suma a `FormCatalog.cs` (`ProposalState`, `Proposal`, `Approve` y `DiscardProposal`) y a `IFormCatalogService` (E12). `formcatalogs.manage` toma en `Permissions.resx` y `.en.resx` el texto final de la §2 de la hoja de ruta («…y aprobar los que propone el asistente»). Tests: aprobar devuelve mapas vacíos; `PermissionTextsTests`.
12. **B12 · El chat del Inicio.** Suma a los turnos, los adjuntos y el historial de Assistant lo de K1, con los tres tipos de a uno y `forms.manage` o `formcatalogs.manage`; `AssistantAudioService.cs`, `AssistantAudios`, `AssistantAudioDurableAdapter` y `AuthoringMaxOutputTokens`. El audio se confirma después del commit; el micrófono depende de `IFileStorageStatus` (E18) y del transcriptor. Sin cupo no se guarda el audio; el mismo `requestId` con el mismo archivo da la misma transcripción; borrar la conversación borra sus audios y lecturas (223, 0064 §2, §4 y §5). Copiá `AssistantAttachmentService.cs`. Tests: WebM y MP4 por sus bytes; el audio sigue a las 24 horas; sin cupo; dos veces; borrar.
13. **B13 · Cargar solo si es seguro.** `FormAutoLoadPolicy.cs`: las tres señales y el aviso fijo con el número. Copiá la captura de `FormRecordService.cs`.
14. **B14 · Precisión, retención, configuración y baja.** La Configuración de Formularios (E18) suma sus seis valores, con la migración de Forms (270 y 272). `FormDocumentRetentionWorker.cs`, con `TenantJobRunner`, en lotes y también al arrancar: con los plazos vigentes, sin tocar la copia de una respuesta ni un archivo de otra fila, y lo propuesto sin revisar vence con «Nadie lo revisó en {n} días: venció.» (215). «Ver los ejemplos» (`GET /{id}/reading/examples`) y la precisión, de 30 días. Suma a `FormsAccountDeletionParticipant.cs` y, por los audios, a `AssistantAccountDeletionParticipant.cs`. Copiá `InvitationExpiryWorker.cs`.
15. **B15 · Rutas y cierre.** Los controllers de K1, sus contratos (un archivo por pedido) y `FormsContractContributor.cs`; suma a `TenantScopeUsageTests`, `ExplicitRouteInventoryTests` y `ModuleIsolationTests`, y los artículos de ayuda (D25 de la 0048). Tests: 401, 403, feature apagada (404) y 409 en cada ruta; aislamiento; ejemplos exportados.
16. **B16 · Acciones del asistente (R: efectos fuera del sistema).** Las de la spec, con la receta y los archivos de su fila B16. Copiá `RoleActionSource.cs`. Tests: `AssistantActionDeclarationTests` y la batería genérica.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan y la spec de la E25 con sus fichas, con las decisiones 261 a 264, 266 a 272 y 275 a 278 de la hoja de ruta. Después de K1, hacé B1 a B16 en orden, copiando los moldes, y la (R) con su revisión adversarial antes del commit; no toques el front ni la tabla de estado. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español; nadie edita src con la app levantada; TDD; al terminar cada tarea, build y tests rápidos enteros, sin filtrar, más integración y seguridad del área si tocaste rutas, persistencia o aislamiento; el contrato cambia solo en «Cambios al contrato»; ninguna clave real; al usuario, solo lo de producto.
```

## Front

Cada pantalla copia su tablero y sus textos, con i18n, tipos generados, `shared/format`, TanStack Query y axe, y los tests de su fila en la spec y los que nombra.

1. **F1 · Tipos y llamadas.** `src/modules/forms/documents/api.ts` y `types.ts`, más lo nuevo del asistente y los catálogos. Copiá la API de la E21. Tests: zod estricto con los ejemplos.
2. **F2 · La bandeja.** `/org/formularios/documentos`: `documents/pages/DocumentsPage.tsx`, `components/DocumentPanel.tsx` y `DocumentPreview.tsx`. Con un bloque con mínimo, «Pasar al formulario» abre `LoadResponsePage.tsx` (E20) con lo leído, que suma el destino `POST /documents/{id}/record`, su confirmación y su «Enviada». Suma «Venció», «Protegido», el tope del día y pasar un repetido; revisar, descartar y volver a abrir en el ⋮ (261), los filtros en la pieza (262), «Se borra» en su columna (263) y el orden por columnas (264). La (i) de «Puede decidir quien cargó» de `components/rules/` [E21] toma el texto de la spec (276), y suman los adaptadores de descartar y de «Pasar al formulario». Tablero: `FRM-Documentos` y su teléfono. Tests: lo privado fuera de la dirección; el repetido.
3. **F3 · La lectura.** `documents/components/DocumentReadingSection.tsx` y `ReadingFields.tsx`, en el editor, en la plantilla y en su vista, con el dato y las validaciones en la pieza (262), «Lo escribió la IA» y «Ver los ejemplos» solo con `formrecords.read`. Tableros: `FRM-Editor-Lectura` y `FRM-Plantilla-25`, con sus teléfonos, y `Plat-FRM-Plantilla-Lectura`.
4. **F4 · Armar con la IA.** `/org/formularios/documentos/:id/armar`: `documents/pages/BuildFormPage.tsx`, con «Se parece a una plantilla» y «Usar la plantilla», que pide nombre y prefijo como la E22. Tablero: `FRM-Armar-Con-IA` y su teléfono. Tests: crear, la plantilla, el bloque y la falla.
5. **F5 · La tarjeta, el clip, el micrófono y las entradas.** `ModuleRegistration` suma `assistantProposal`, que Forms llena con `forms/assistant/ProposalCard.tsx` y `ProposalPreviewDialog.tsx`. En `src/modules/assistant`, `components/AudioRecorder.tsx`, y suma a `AttachmentComposer.tsx`, `AssistantThread.tsx` y `lib/assistantFiles.ts`. «Armar con la IA», con `assistant`, `assistant.use` y el permiso de armar, abre el Inicio con el pedido en el estado de la navegación. El Inicio vacío suma las dos sugerencias del tablero. La tarjeta toma los estados de «Acciones del asistente», con «Ya no disponible» (268), y «Armar con la IA» de cada tema va en su ⋮ (261). Tableros: `Asistente-Org-25` y su teléfono, `FRM-Formularios-25`, `FRM-Catalogos-25` y `FRM-Faltan-25`. Tests: un `.xls` dice por qué no se manda; «Revisar la importación» abre el import.
6. **F6 · El catálogo propuesto.** Suma a `CatalogEditorPage` (E12), con el adaptador de «Descartar propuesta»; después de importar, la cabecera dice su estado sin recargar (O24). Tablero: `FRM-Catalogo-Propuesta-25`.
7. **F7 · El chat, el detalle y la baja.** Suma a `MessageThread.tsx` («Por qué lo propuso» y «Por qué preguntó»), `RecordValues.tsx`, `QuickView.tsx` y `RecordFullPage.tsx`, con el renglón «La pasó desde un documento» en `RecordActivity.tsx` (266 y 267). `LifecycleDialogs.tsx` (E17) conserva la fila «App · No se carga más» de la E20. Tableros: `WA-Chats-Documento` y `FRM-Respuesta-25`, con sus teléfonos.
8. **F8 · Los Casos, la campana y la configuración.** La pantalla «Asistente» (E23 y E24) suma el tipo «Documento» y el bloque fijo «Armar formularios y catálogos». Las dos páginas de la Configuración de Formularios (E18) suman la zona «Documentos recibidos», con su máximo y la (i) de los plazos (270 y 272), y sus adaptadores pasan a `@2`; tableros `FRM-Config-Org-25`, su teléfono y `FRM-Config-Plat-25`. Los dos avisos van con `notifications` en `registration.ts`. Tableros: `WA-Asistente-Casos-25` y `Campana-25`, con su teléfono. Tests: el bloque no se edita; los avisos, con y sin `formrecords.read`.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, este plan (../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-25-documentos.md) y la spec de la E25, con las decisiones 261 a 264, 266 a 272 y 275 a 278 de la hoja de ruta. Después de K1 y npm run contracts, hacé F1 a F8 contra los tipos generados, copiando los tableros de la v175 y, los que la spec da como «Se redibujan», su redibujo aprobado; assistant no importa forms. No toques el lienzo, las specs, el back ni la tabla de estado. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español; nadie edita src con la app levantada; TDD; al terminar cada tarea, npx tsc -b y npm test enteros, sin filtro, más integración y seguridad del área si tocaste permisos o aislamiento; el contrato cambia solo en «Cambios al contrato»; ninguna clave real; al usuario, solo lo de producto.
```

## Integración y puerta

1. **I1 · Integrar** contra la API real. **Pendiente para la I1 de la 10:** la prueba real con la IA y Meta de la spec, con `M-WA-Flow-Prellenado` y `M-WA-Armar-Con-IA` en un celular, que confirma que la respuesta llega sin el archivo (O38); la nube y el antivirus.
2. **I2 · Calificar.** Antes de la puerta, los perfiles de leer y de armar, con los casos ficticios de la spec, en `Modules/Assistant/Evaluation/` de `Api.IntegrationTests`, como la E23: `FakeChatClient` en los tests, y el proveedor real en una corrida que habilita el usuario.
3. **I3 · Puerta.** `/puerta`: build sin advertencias y `dotnet test`; front `build`, `lint`, `test` y `contracts:check`; el E2E real con los tres recorridos de la 25, en `F/scripts/test-e2e-real-forms.mjs` [E12]; `realinear-docs --detectar` en 0; la galería de lo nuevo o cambiado (`F/scripts/capturas/etapa-25.json`), a 1440, 390 y 320, con `M-WA-Flow-Prellenado` contra su cuarta pantalla; `/recorrido` y la lista de control, que suma los riesgos de la [prueba integral de ArquitecturaBase](../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase): los puntos 13 a 15 de «Lo que ya se pagó allá» de la spec (O38, O34 y O24), el repetido (O37 y O46), el banco que se ve solo en el logo (O45) y el monto negativo de un débito (O47). Al cerrar nacen la documentación de la parte de pasos y los `AGENTS.md` nuevos.
4. **I4 · Recorrido.** La prueba de «Cómo se cierra» de la spec, con Meta simulado, y los tres recorridos en `F/scripts/recorrido-usuario-forms.mjs` [E12].

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan y /puerta. Integrá contra K1, devolvé cada diferencia a su dueño y corré la puerta una vez sobre copias del corte; acá va solo su tabla. Meta con un número conectado y la nube quedan para la I1 de la 10; al usuario, pasos clic por clic, sin manejar su PC. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con git commit -- <rutas>, en español; nadie edita src con la app levantada; TDD; cada arreglo, con build y tests rápidos enteros de su repo, más integración y seguridad del área si tocó rutas, persistencia o aislamiento; el contrato cambia solo en «Cambios al contrato»; ninguna clave real; al usuario, solo lo de producto.
```

## Relación con otras etapas

**Usa de las anteriores** (la lista cerrada de K0, más lo que el contrato y las tareas marcan con su etapa y lo que las tareas suman o copian):
- **Base:** `INotificationSource`, con el sufijo `NotificationSource`, y `notifications` en `registration.ts`; de su segunda parte (214), `Picker` (262), el orden por columnas (264), el conversor del chat (0065) y, de la 0064, `providerIssue`, el lugar de subida y `AssistantAttachmentService` con el cupo y el borrado.
- **E12:** `IFormCatalogImportService`, `FormCatalogFiltersPolicy`, los topes de `IFormCatalogSpreadsheet`, `IFormWorkLease` (el carril `Catalogs` de la E15, al importar) e `ImportDialog`.
- **E13:** `FormCatalogField` y `FieldValuesJson`, para el cruce.
- **E14:** `FormPublicationService` (`CompleteAsync` y `ResolveAsync`), `FormRecordSink`, `FormStructuredSource`, `Check` de `IStructuredFormChannel`, `WhatsAppFlowSessions`, `WhatsAppFlowBindings.Profile`, `FormRecords.SourceKey`, `Forms.Definition.PrefixTaken`, `Forms.Draft.CatalogUnavailable` y `FormsContractContributor`.
- **Acciones del asistente:** `AssistantProposals`, `Message.proposal?`, su tarjeta (`src/modules/assistant/components/AssistantActionCard.tsx`) y sus adaptadores (`src/shared/assistant-actions/`).
- **E15:** `BusinessFormRecordsController`, los filtros en el cuerpo, `RecordForm.tsx` y `RecordActivity.tsx` (266 y 267).
- **E16:** los filtros por destinatario de `FormOfferBuilder`.
- **E18:** la Configuración de Formularios (`FormSettingsService` y sus páginas), `IFileContentInspector`, `WhatsAppChatFile`, `WhatsAppChatFileService`, `StructuredRecordCapture.Files`, `FormRecordFiles` y el perfil `r10`.
- **E19:** el bloque (`rows`) y el perfil `r11`.
- **E20:** `POST /{id}/records` multipart, `FormVersions.AppOnly`, el tipo `location` y su pedido antes del Flow, y `Forms.Entry.TooLarge`.
- **E21:** `platform.FormActionWorks`, `FormApprovalTask`, la falla `RoleWithoutMembers` y `WhatsAppConversationLink.State`.
- **E22:** el `format: 1` de `FormTemplateDraft`, con `titles[]`; `FormTemplateLibraryService`, `FormDefinitions.SourceTemplateId` y `SourceTemplateVersion`, y las rutas de plantillas.
- **E23:** `IConversationAgent`, `ConversationTurn`, `AssistantChannelContext`, `WhatsAppAssistantActions` y su ruta del porqué, las sugerencias al operador, la oferta, `ISpeechTranscriber` (0055) y `MissingFormsPage.tsx`.
- **E24:** `ConversationTurn.AgentId`, `WhatsAppAgentResolver` y `/org/whatsapp/asistentes/:assistantId`, con sus casos.

**Deja para las siguientes:** cierra el bloque; queda lo que crean sus tareas, con sus seis puertos.

## Cambios al contrato


## Para decidir

- **El texto de un documento repetido** (O37 y O46 de allá, que siguen sin decidir: allá dice siempre «Este comprobante ya se cargó el {fecha} en {número}. No lo cargo de nuevo.»). Recomendada: si solo se propuso, «Ya me lo mandaste el {fecha}: completá el formulario que te mandé.», y si ya se cargó, el de `Forms.Document.Duplicate`, «Ya se cargó en {number} el {date}.». La otra: un solo texto para los dos casos, «Ya me mandaste este documento el {fecha}.».
- **El monto negativo de un débito** (O47 de allá, importante): un comprobante de débito muestra el importe con signo menos, y en una transferencia el monto es siempre positivo; un negativo resta en el tablero. Recomendada: la validación fija `Amount` pide un monto positivo, así un negativo queda marcado y no se carga solo.
