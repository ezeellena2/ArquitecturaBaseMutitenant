# E18 · Fotos y documentos · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/18-fotos-y-documentos.md`, en un solo archivo. Tableros aprobados: lienzo v175. No queda nada para decidir.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0, K1 | Pendiente |
| Back | B1–B14 | Pendiente |
| Front | F1–F6 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: el contrato primero; después Back y Front en paralelo; una sola puerta. Cada chat sigue las reglas de su texto; un arreglo corre solo lo que toca. Se desarrolla con `LocalFileStore`; Meta y la nube quedan para la E10 (123).

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md)): la llevan las tareas **(R)**, completa y antes de su commit; un subagente nuevo recibe el código y los escenarios, sin la conclusión de quien programó.

## Qué se verificó al arrancar

| Fila | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [E10] el adaptador | solo `LocalFileStore`, con claves opacas y un diario por categoría; la 10 deja el disco solo en Development y Testing | se usa, con las categorías `forms` y `whatsapp` |
| [E10] sin almacenamiento | no hay cómo saberlo: `IFileStore` resuelve siempre | nace en el núcleo `IFileStorageStatus.IsConfigured`, verdadero en Development y Testing; la E10 lo prende con su adaptador |
| [E14] y [E15] | todavía no existen | van a K0 |
| [E11] entrada y salida | `WhatsAppWebhookReader` guarda solo el tipo y descarta el id, `mime_type`, `sha256`, `filename` y el epígrafe (D7); la salida maneja solo texto y plantillas | B10 y B11 |
| [E11] la reserva de la cola | `WhatsApp:InboundLeaseSeconds` es 120 | alcanza para 90 s |
| [Meta] selectores | uno por pantalla; el valor, solo en `complete`, y entre pantallas con `${screen.<ID>.form.<campo>}`; hasta 10 archivos y 100 MiB; cada archivo trae `file_name`, `mime_type`, `sha256` e `id` | confirma la spec; las pantallas con `data` se prueban en I3 |
| [Meta] medios | `GET /{media-id}` da una `url` de 5 minutos, que se baja con el token; los topes de la spec. **El id de un webhook vence a los 7 días** | confirma los topes, en bytes decimales |
| Piezas de hoy | `FileSignaturePolicy` reconoce PNG, JPEG, WebP y PDF; las descargas van adjuntas; faltan `SkiaSharp` y `OpenMcdf` | B1 amplía; B7 y B12, en línea |
| Chat | las tres bandejas salen de `WhatsAppConversationsControllerBase`; WhatsApp no borra mensajes | las rutas van en la base; el archivo vive con su mensaje |

## Contrato

### K0 · Lo que dejó la etapa anterior

El chat del contrato la confirma contra el código; lo que difiera va a «Cambios al contrato» antes de programar.

- **Base de la 12:** `InfoTip`, `RowAction.disabledReason`, `ConfirmDialog` y `PageLoading`.
- **Base de la 12, segunda parte (214):** el motivo de lo apagado con un toque (211); `OutboxStatus.Uncertain`, las fallas que clasifica `WhatsAppCloudClient` y la revisión de «Sin confirmar» (0066); `WhatsAppChatContentConverter` (0065); `PersonalDataPurgeTests` (0062).
- **E14:** `FormQuestionType`, `FormDraft`, `FormDraftEditor` y `FormVersionCreator`; `WhatsAppFlowBuilder`, con `flow-static-7.3-r9` y el presupuesto de 16 KiB; `WhatsAppFlowReplyVerifier`; `IWhatsAppFlowReplyService`; `WhatsAppFlowSession` (estados, `ErrorCode`, `ErrorFieldLabel`); `ProtectedFlowResponse` de 24 KiB; `IWhatsAppInboundService.FailAsync` con 30, 90, 210, 450 y 930 s; `StructuredRecordCapture` y `StructuredRecordCard`; `FormRecordWriter` y `FormRecordService.CaptureAsync`; la tarjeta `form` con `errorFieldLabel`; los `issues[]` de `publication-checks`, con `limit` y `length`. En el front, `QuestionEditor.tsx`, `FormPreviewPhone.tsx`, `PublishDialog.tsx` y `FormMessageCard.tsx`.
- **E15:** `FormRecordColumnsBuilder`, `FormRecordReader`, `FormRecordSheetBuilder`, `FormRecordCondition`, `FormRecordFiltersPolicy`, `FormRecordQueryFilters`, `BusinessFormRecordsController`, `FormRecordFileResults.cs` y el mapa de valores `{kind, …}`; en el front, `lib/values.ts`, `FormRecordsPage.tsx`, `QuickView.tsx`, `RecordFullPage.tsx`, `RecordValues.tsx` y `ExportDialog.tsx`.
- **E17:** `FormDefinitionCopyCreator`; `GET /{id}/lifecycle`, con `openSends`; `GetRetirementAsync` y `IWhatsAppFlowDeprecationService.ReserveAsync`, que cuentan los envíos abiertos; `LifecycleDialogs.tsx`, con «Se conserva».

### Rutas

**Formularios,** en `/api/organization/forms`, con `[Access(Access.Business)]`, `[FeatureGate("forms")]` y las cuotas generales, en `BusinessFormRecordsController`:

| Método y ruta | Permiso | Devuelve | Errores |
| --- | --- | --- | --- |
| GET `/{id}/records/{recordId}/files/{fileId}/thumbnail` | `formrecords.read` | 200 JPEG, en línea | 404 `Forms.Record.FileNotFound` |
| GET `/{id}/records/{recordId}/files/{fileId}/content` | `formrecords.read` | 200 el original: en línea si es una foto; si no, adjunto | 404 `Forms.Record.FileNotFound` |

**Chat,** en `WhatsAppConversationsControllerBase`: valen para `customers`, `internal` y Plataforma, con el permiso de cada bandeja y sin la feature de formularios.

| Método y ruta | Pide → devuelve | Errores |
| --- | --- | --- |
| POST `conversations/{id}/attachments` | multipart `{file, text?}`, `text` hasta 1024, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]` y `[IdempotencyDurable(typeof(WhatsAppChatFileDurableAdapter))]` → 202 `{messageId}` | 400 `WhatsApp.Chat.FileTooLarge` (con `limit`) y `FileUnsupported`, en `file`; 409 los del envío de la E11 (`WindowClosed`, `TakenByOther`, `PaymentRequired`, `ReadOnly`); 404 |
| GET `conversations/{id}/files/{fileId}/thumbnail` | → 200 JPEG, en línea | 404 `WhatsApp.Chat.FileNotFound` |
| GET `conversations/{id}/files/{fileId}/content` | → 200 el original: foto, audio y video en línea; documento adjunto | 404 `WhatsApp.Chat.FileNotFound` |

Toda descarga responde `no-store` y `nosniff`, y revisa el permiso antes y después de abrir el archivo.

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| GET `/{id}` y PUT `/{id}/draft` | preguntas `Photo` y `Document`, con `photoSource?` (`CameraOrGallery` o `CameraOnly`), `minFiles?` y `maxFiles?`; `FormDetail` suma `maxFileMiB`, el tope vigente que dice la (i) |
| POST `/{id}/publication-checks` | `Forms.Draft.TooManyFiles` (`questions`, con `limit` 10 y `length`, la suma de los máximos) y `Forms.Publication.FilesUnavailable`; `ResponseTooLarge` mide contra 64 KiB con una pregunta de archivo |
| GET `/{id}/records/columns` | columnas `Photo` y `Document` |
| POST `…/records/search` y GET `…/records/{recordId}` | el valor `{kind: "files", files[{id, fieldId, name, kind, mediaType, size, hasThumbnail}]}`, en orden |
| POST `…/records/search` y `…/facets` | `answered`, `unanswered` y `notAsked` cuentan un archivo; sin faceta (143) |
| POST `…/records/export` | la celda lleva la cantidad y los nombres: `2: frente-local.jpg; gondola.jpg` |
| GET `/{id}/lifecycle` [E17] | `hasFiles`, si alguna respuesta tiene archivos |
| GET `/api/me` | la capacidad `form-files`: `available` si hay almacenamiento y `forms` está encendida |
| GET `conversations/{id}` | cada mensaje suma `file?{id, name, kind, mediaType, size, state, hasThumbnail}`; `kind` es `Image`, `Audio`, `Video` o `Document`, y `state`, `Pending`, `Stored`, `Failed` o `Skipped` |
| La tarjeta `form` [E14] | `formId?` y `recordId?`, para las rutas de sus archivos; `files[]`, como en la planilla; `fileCount?`, los anunciados mientras `Receiving`; `attemptsLeft?`, y `state: Receiving` |

`size` va en bytes, y `name` es el nombre de descarga. La lista de chats nunca trae nombres de archivos. Las columnas de archivos no se ordenan, y `lastForm` (E14) muestra `Receiving` como `Saving`.

**Puertos:** los de la spec; `StructuredRecordCard` suma también `DefinitionId`, y `Application/Interfaces/Integrations/Files/` suma `IFileStorageStatus` (`bool IsConfigured`).

**Errores**, en `FormsTexts` y en los textos del módulo WhatsApp:

| Código | Tipo | es | en |
| --- | --- | --- | --- |
| `Forms.Record.FileNotFound` | NotFound | No encontramos el archivo. | We couldn't find the file. |
| `Forms.Draft.TooManyFiles` | Validation (`questions`) | WhatsApp acepta 10 archivos por respuesta. | WhatsApp accepts 10 files per response. |
| `Forms.Publication.FilesUnavailable` | ServiceUnavailable | Falta configurar dónde se guardan los archivos. | File storage isn't set up yet. |
| `Forms.Record.FileRejected` | Validation (`fieldId`) | Un archivo no es lo que dice. | A file isn't what it claims to be. |
| `Forms.Record.FileDownloadFailed` | Failure (`fieldId`) | No se pudo bajar un archivo de WhatsApp. | A file couldn't be downloaded from WhatsApp. |
| `WhatsApp.Flow.TooManyFiles` | Validation | La respuesta trae más de 10 archivos o más de 100 MiB. | The response has more than 10 files or more than 100 MiB. |
| `WhatsApp.Chat.FileNotFound` | NotFound | No encontramos el archivo. | We couldn't find the file. |
| `WhatsApp.Chat.FileTooLarge` | Validation (`file`) | El archivo pasa el tope de WhatsApp para su tipo. | The file is over WhatsApp's limit for its type. |
| `WhatsApp.Chat.FileUnsupported` | Validation (`file`) | WhatsApp no acepta este tipo de archivo. | WhatsApp doesn't accept this file type. |

`Forms.Draft.QuestionInvalid` (E14) suma los archivos y el origen de la foto. **Permisos:** ninguno nuevo.

### K1 · Publicar el contrato

El Back publica con 501 lo de esta sección y genera `docs/contracts/openapi.json`; el Front corre `npm run contracts` y no escribe tipos a mano. Los ejemplos reales salen en B13.

**Texto para el chat del Contrato**

```text
Leé AGENTS.md, este plan y la spec docs/plans/formularios/18-fotos-y-documentos.md. Primero K0: confirmá contra el código cada nombre de la lista y avisá a la Guía lo que difiera antes de seguir. Después K1: publicá con 501 las rutas, los DTO, los errores con sus textos es/en y la capacidad form-files de «Contrato»; generá openapi.json y avisá para que el front corra npm run contracts. No programes la lógica ni toques el front. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits solo tuyos, con git commit -- <rutas>, en español y conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros del back, sin Docker y sin filtrar por módulo; si tocaste rutas, persistencia, autenticación o aislamiento, también la integración y la seguridad del área; el contrato cambia solo anotándolo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Back

Todas aplican Result y ProblemDetails, `OperationLog.RunAsync`, `[LoggerMessage]`, `TimeProvider`, resources es/en, una UoW por escritura y RLS. Bajar, verificar y subir, fuera de la UoW; confirmar el diario, después del commit. Nombres y contenidos, nunca en logs ni en la lista de chats. Cada migración agrega y escribe en el mismo despliegue (`Photo`, `Document` y `Receiving`): la excepción de la 0060 va anotada en cada una, porque todavía no hay despliegue (123).

1. **B1 · Verificar un archivo.** `A/Interfaces/Integrations/Files/IFileContentInspector.cs` e `I/Files/FileContentInspector.cs`; suma a `D/Common/FileSignaturePolicy.cs`. El tipo sale del contenido (spec): Office viejo con `OpenMcdf` y OOXML con `ZipArchive`. Una foto se mide con `SKCodec` antes de decodificar, y la miniatura es JPEG de 320 de lado. `SkiaSharp`, su nativo de Linux y `OpenMcdf` entran en `Directory.Packages.props`, con versión exacta de al menos dos semanas, solo en Infrastructure. Copiá `AssistantAttachmentPolicy.cs`. Tests: un archivo de cada tipo; dañado, cifrado y con la extensión cambiada; la bomba de píxeles, sin decodificar (lección 5); U+202E; Application no usa esos paquetes.
2. **B2 · Preguntas, opción y sellado (R: datos, migración).** Suma a `FormQuestionType.cs` (`Photo`, `Document`), `FormDraft.cs` (`photoSource`, `minFiles`, `maxFiles`), `FormDraftEditor.cs` (`0 ≤ mínimo ≤ máximo`, máximo de al menos 1; `required` sale del mínimo) y `FormVersionCreator.cs`, que sella `FormVersions.MaxFileBytes`, con la columna y su migración `<fecha>_FormVersionMaxFileBytes.cs`. `A/Configuration/Forms/FormsFilesOptions.cs`: `Forms:MaxFileSizeMiB`, de 1 a 25 y 25 por omisión, con `ValidateOnStart`. `publication-checks` suma `TooManyFiles` y, sin almacenamiento, `FilesUnavailable`; el borrador se guarda igual. `IFileStorageStatus` e `I/Files/FileStorageStatus.cs`. Copiá esos mismos y `WhatsAppOptions.cs`. Tests: mínimo y máximo; la suma de 10; sin almacenamiento; el tope sellado no cambia con la opción; copiar un formulario copia sus preguntas de Foto y Documento.
3. **B3 · El Flow con pantallas.** Suma a `WhatsAppFlowBuilder.cs` el perfil `flow-static-7.3-r10`: cada pregunta de archivo abre su pantalla y las siguientes la comparten; los IDs de pantalla, solo letras y guiones bajos (lección 1); las intermedias navegan sin payload, y el `complete` de la última toma cada pregunta con `${screen.<ID>.form.field_<N>}` y los `data` de la primera con `${screen.<primera>.data.<x>}`. `DocumentPicker` lleva los tipos de la 133. Copiá el mismo. Tests: una pantalla por pregunta; los IDs; un catálogo en otra pantalla; 64 KiB con archivos y 16 sin ellos; lo sellado con r9 no cambia.
4. **B4 · Bajar de Meta (R: Meta).** `A/Modules/WhatsApp/Interfaces/Integrations/IWhatsAppMediaClient.cs` e `I/Modules/WhatsApp/WhatsAppMediaClient.cs`: `GET /{media-id}` con el token de la WABA del número, y la descarga solo por HTTPS desde `WhatsApp:MediaHosts` (`lookaside.fbsbx.com`, `*.fbcdn.net` y `mmg.whatsapp.net`), sin redirecciones y hasta el tope; también `UploadAsync`, para B11. Copiá `WhatsAppGraphClient.cs`. Tests: hosts, HTTPS y redirecciones; un cuerpo más largo que el tope; el token de la organización; el id como número JSON conserva su texto (lección 2).
5. **B5 · La captura con archivos (R: Meta y archivos).** Suma a `WhatsAppFlowReplyVerifier.cs` los arreglos de archivos (la opcional vacía llega omitida, lección 3) y a la fase de captura: la sesión pasa a `Receiving` y, fuera de la UoW y en 90 s, se baja, se verifica, se arma la miniatura, se guarda en `forms` y se llama a la captura de Forms; si algo falla, se borra lo subido. La entrada admite 64 KiB con archivos; la migración `<fecha>_WhatsAppFlowSessionFiles.cs` suma `Receiving`, `Attempts` y `ProtectedFlowResponse` de 96 KiB. `Receiving` cuenta como envío abierto en `GetRetirementAsync`, `ReserveAsync` y `lifecycle.openSends` [E17]. Una falla técnica reintenta con esperas que crecen hasta una hora y da `FileDownloadFailed` solo con cinco fallas y un día cumplido (H11); lo mismo un archivo que llega al chat (B10); lo que no coincide, `FileRejected` enseguida, y los dos nombran la pregunta. Copiá `AssistantAttachmentService.cs`. Tests: la opcional omitida; hash distinto; reintentos y rechazo, sin número ni valores; la misma sesión dos veces no duplica; más de 64 KiB; no se borra ni se depreca mientras baja.
6. **B6 · Guardar en Forms (R: datos y aislamiento).** `D/Forms/FormRecordFile.cs` y su configuración; suma a `FormRecordWriter.cs` y `FormRecordService.cs`: los archivos van en la UoW de la respuesta, y sus claves se confirman después del commit. La repetición compara posición, hash, tamaño y nombre, nunca la clave. La migración `<fecha>_FormRecordFiles.cs` suma la tabla, que se inscribe en `PersonalDataPurgeTests`: sigue a su respuesta (23). Copiá `FormRecordWriter.cs`. Tests: todo o nada; aislamiento; repetir con otras claves da el mismo número y WhatsApp borra las suyas.
7. **B7 · Ver y descargar (R: permisos y archivos).** `A/Modules/Forms/Services/FormRecordFileService.cs`, con su interfaz; suma a `BusinessFormRecordsController.cs` y `FormRecordFileResults.cs` [E15]. Copiá `AssistantAttachmentService.DownloadAsync` y `AssistantFileResults.cs`. Tests: el permiso antes y después de abrir; falta el archivo = 404; `no-store`, `nosniff` y adjunto; de otra organización.
8. **B8 · Planilla, conteos, detalle y Excel.** Suma a `FormRecordColumnsBuilder.cs`, `FormRecordReader.cs` y `FormRecordSheetBuilder.cs` [E15], con los archivos de una página en una sola consulta, y a `FormRecordCondition.cs` (`answered`, `unanswered` y `notAsked` en `Photo` y `Document`), `FormRecordFiltersPolicy.cs` y `FormRecordQueryFilters.cs`, la función única de la E15. Copiá esos mismos. Tests: `answered` con un archivo; la cantidad y los nombres en el Excel; sin consultas por fila.
9. **B9 · Limpiar lo no confirmado (R: archivos y aislamiento).** `I/Modules/Forms/FormFileCleanupWorker.cs` e `I/Modules/WhatsApp/WhatsAppFileCleanupWorker.cs`, cada hora: de `ListUnconfirmedAsync` toman solo las claves sin confirmar de más de 24 horas, de a 100, y le preguntan a cada organización, y a Plataforma en `whatsapp`, cuáles usa, en una consulta por página (`ListReferencedStorageKeysAsync`); confirman las usadas y borran las demás. Lo confirmado nunca se toca. Copiá `AssistantRetentionWorker.cs`. Tests: a las 24 horas; una clave en uso se confirma; nunca una consulta por archivo (lección 4).
10. **B10 · Lo que llega al chat (R: Meta, cifrado y aislamiento).** `D/WhatsApp/WhatsAppChatFileBase.cs`, `WhatsAppChatFile.cs` y `PlatformWhatsAppChatFile.cs`, con sus configuraciones y la migración `<fecha>_WhatsAppChatFiles.cs`; su `FileName` va con `WhatsAppChatContentConverter`, con el propósito de la organización o el de Plataforma (0065), y las dos tablas se inscriben en `PersonalDataPurgeTests`: siguen al chat (D4). `WhatsAppWebhookReader.cs` y `WhatsAppMessage.cs` suman `MediaId` como texto, `MediaMimeType`, `MediaSha256` y `EncryptedFileName`; el epígrafe va cifrado, como el texto. `WhatsAppOrganizationInboundWriter.cs` y `PlatformWhatsAppInboundWriter.cs` crean el archivo `Pending` de una foto, un audio, un video o un documento admitido; otro documento queda `Skipped`. `A/Modules/WhatsApp/Services/WhatsAppChatFileService.cs` e `I/Modules/WhatsApp/WhatsAppChatFileWorker.cs`, por organización con `TenantJobRunner`, cada una en su `try/catch`, y Plataforma aparte, con los intervalos de B5: lo que no baja queda `Failed`, y lo que no es lo que dice, `Skipped`. Copiá `WhatsAppBroadcastWorker.cs` y `WhatsAppChatMessageBase.cs`. Tests: cada tipo; lo que no se guarda; reintentos; un mensaje trabado no frena a los demás; organización y Plataforma; el nombre no se ve en el SQL crudo y, copiado a otra organización, se lee vacío.
11. **B11 · Mandar un archivo (R: Meta y archivos).** `IssueFileAsync` en `WhatsAppReplyIssuer.cs`, con las reglas del texto; la variante de archivo en `WhatsAppOutboundMessage.cs` (clave, tipo, nombre y epígrafe, dentro del contenido cifrado del outbox); en `WhatsAppCloudClient.cs`, subir a `/media` y mandar con el id, con la clasificación de la 0066: lo que no salió se reintenta, lo dudoso queda «Sin confirmar» y lo definitivo, «No se pudo mandar» con su motivo; una falla al subir a `/media` no llega al cliente y se reintenta. La ruta, en `WhatsAppConversationsControllerBase.cs`, con `P/Modules/WhatsApp/WhatsAppChatFileDurableAdapter.cs`. Fuera de la UoW se verifica, se mide y se guarda; en la UoW, el mensaje, su archivo `Stored` con `IdempotencyKey` y el outbox. Un audio no admite epígrafe: el texto sale después, en otro mensaje. Copiá `AssistantControllerBase.Upload`, `AssistantDurableAdapter.cs` y `WhatsAppReplyIssuer.IssueAsync`. Tests: ventana cerrada; topes por tipo; idempotencia; falla al subir a Meta; un corte al mandar queda «Sin confirmar» y no se repite; un archivo por mensaje.
12. **B12 · Ver (R: permisos).** Suma `file` a la lectura de mensajes de la organización y de Plataforma, y las rutas de archivos del chat, con `WhatsAppFileResults` en línea para foto, audio y video, estos dos con rangos. `FormRecordSource` suma a la tarjeta `DefinitionId` y los archivos. Copiá `AssistantAttachmentService.DownloadAsync`. Tests: sin el permiso de la bandeja; de otra organización; falta el archivo = 404; la lista de chats sin nombres; la tarjeta sin `formrecords.read`, sin archivos.
13. **B13 · Capacidad, contratos y ejemplos.** `A/Modules/Forms/Services/FormsCapabilityContributor.cs` (`form-files`); suma a `FormsContractContributor.cs` y al de WhatsApp. Copiá `AssistantCapabilityContributor.cs`. Tests: cada ruta con 401, 403 y feature apagada = 404; `RouteIsolationMatrixTests` de Forms y WhatsApp; `ModuleIsolationTests`; los ejemplos exportados de una corrida real, con cada estado.
14. **B14 · Los medios de prueba.** En `tests/…Api.IntegrationTests/TestFeatures/WhatsAppBrowser/`, solo en Testing: un `IWhatsAppMediaClient` de prueba, que sirve y recibe archivos, y las órdenes para responder un formulario con archivos y para que entre una foto al chat. Copiá `WhatsAppBrowserControl.cs` y `RecorridoWhatsAppModule.cs`. Tests: el E2E y el recorrido; fuera de Testing no arranca.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan, la spec docs/plans/formularios/18-fotos-y-documentos.md y las fichas de su «Qué leer». Después de K1, hacé B1 a B14 en orden, contra ese contrato, copiando los moldes de cada tarea; las tareas (R) llevan la revisión adversarial antes del commit. No toques el front ni edites la tabla de estado. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits solo tuyos, con git commit -- <rutas>, en español y conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros del back, sin Docker y sin filtrar por módulo; si tocaste rutas, persistencia, autenticación o aislamiento, también la integración y la seguridad del área; el contrato cambia solo anotándolo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Front

Las tareas siguen los tableros v175 y las fichas del front, con i18n en es y en, los tipos generados y axe donde cambia una pantalla; los tamaños, en MB, con el formato compartido.

1. **F1 · Tipos y llamadas.** Suma a `src/modules/forms/api/` y a `src/modules/whatsapp/chats/api/`. Copiá la API de la E17. Tests: zod estricto con los ejemplos de B13.
2. **F2 · El editor y publicar.** Suma a `QuestionEditor.tsx` [E14] Foto y Documento, con «Cuántas fotos» o «Cuántos documentos» y su (i), con `maxFileMiB`; sin `form-files`, los dos grisados con su motivo (43); `FormPreviewPhone.tsx`, por pantallas. Suma a `PublishDialog.tsx` [E14] «Archivos: hasta 12 de 10». Copiá `FRM-Editor-18`, `M-FRM-Editor-18` y `FRM-Publicar-18`. Tests: el tope en la (i); sin almacenamiento; las pantallas; el problema de los 10.
3. **F3 · El visor común.** `src/shared/ui/PhotoViewer.tsx`: anterior, siguiente, la tira de miniaturas y, al pie, el nombre, el tamaño y «Descargar» (194). El original se baja al abrir. Copiá `WA-Chats-Visor` y `M-WA-Chats-Visor`. Tests: teclado y foco; solo las miniaturas visibles; un 403 lo cierra con «Ya no tenés permiso para ver estas respuestas.».
4. **F4 · La tarjeta del chat.** Suma a `FormMessageCard.tsx` [E14] «Bajando los archivos» y «Reintentando»; la guardada, con miniaturas, «+N» y documentos; y los dos rechazos, con «Mandar de nuevo». Relee mientras está `Receiving`. Copiá `WA-Chats-Respuesta-18` y `M-WA-Conversacion-Respuesta-18`. Tests: cada estado; «Mandar de nuevo».
5. **F5 · Planilla, detalle, Excel y dar de baja.** Suma a `lib/values.ts` el valor `{kind: "files"}`, a `FormRecordsPage.tsx` la columna (la miniatura y «2 fotos», «1 documento» o «2 documentos», sin los tipos, 206; «Sin respuesta» o «—»), a `QuickView.tsx` la pestaña «Fotos», a `RecordFullPage.tsx` «Descargar ▾», a `RecordValues.tsx` las miniaturas y los documentos, a `ExportDialog.tsx` su (i), y a `LifecycleDialogs.tsx` [E17] «con sus fotos y documentos», con `hasFiles`. Copiá `FRM-Respuestas-18` y `FRM-Respuesta-18`, con sus teléfonos, `FRM-Respuesta-Completa-18` y `FRM-Dar-De-Baja-18`. Tests: la columna, con «1 documento» y sin el tipo; la pestaña y «Descargar ▾»; sin filtro de archivos (143).
6. **F6 · Los archivos del chat.** Suma a `ChatComposer.tsx` el clip y «Mandar un archivo a …», con el tope de su tipo, el clip grisado fuera de las 24 horas y una clave de idempotencia por diálogo; a `MessageThread.tsx`, que corrige su comentario de la D7, cada tipo con su epígrafe y «Descargar», «Bajando la foto…», «Mandó un documento», «No se pudo mandar» con su motivo, «Sin confirmar» con «Marcar como revisado», como un mensaje de texto (0066), y «No pudimos bajar la foto de WhatsApp.» (lo decidió la Guía). Copiá `WA-Chats-Adjuntos` y `M-WA-Chats-Adjuntos`. Tests: adjuntar, grisado, subiendo y error; un archivo «Sin confirmar» y su revisión; un archivo recibido se ve y se baja; organización y Plataforma.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, este plan y la spec ../ArquitecturaBaseMutitenant/docs/plans/formularios/18-fotos-y-documentos.md. Esperá K1, corré npm run contracts y hacé F1 a F6 contra los tipos generados y los tableros v175 de cada tarea; nombres de archivos nunca en la URL ni en el storage. No toques el lienzo, las specs ni el back, ni edites la tabla de estado. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits solo tuyos, con git commit -- <rutas>, en español y conventional commits; con la app levantada, nadie edita el src; TDD donde hay lógica; al terminar cada tarea, npx tsc -b y npm test enteros, sin filtrar, y al cerrar la parte, build, lint y contracts:check; si tocaste rutas, autenticación o aislamiento, también sus pruebas de integración y seguridad; el contrato cambia solo anotándolo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real de B13, con los ejemplos validados con zod; una diferencia vuelve a su dueño.
2. **I2 · Puerta.** `/puerta` una vez, sobre copias del corte:
   - back: build sin advertencias y `dotnet test` completo; front: build, lint, test y `contracts:check`;
   - E2E real con el canal de prueba y B14: una respuesta con dos fotos, un PDF y una opcional vacía, vista en el chat, la planilla y el visor, y bajada íntegra; un rechazo y «Mandar de nuevo», sin saltos de número; en Clientes, recibir una foto y mandar un PDF;
   - `realinear-docs --detectar` y `estado-docs --detectar` en 0;
   - la galería a 1440, 390 y 320, solo de los tableros de la tabla de la spec, con sus teléfonos;
   - `/recorrido`, con `fotos-y-documentos` y `archivos-en-el-chat`, y la lista de control de `/puerta`;
   - al cerrar: `docs/features/formularios.md` y `docs/features/whatsapp.md` suman su sección, y `docs/architecture/backend.md`, la de `IFileContentInspector` e `IFileStorageStatus`.

   El retiro del módulo no corresponde: la etapa no cambia la composición.
3. **I3 · Meta y la nube, pendiente para la E10.** Los cinco pasos de «Cómo se cierra» de la spec, con el adaptador de la E10; el usuario hace lo de su cuenta y nadie pega secretos.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |
| Meta y la nube (I3) | Pendiente para la E10 |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan y /puerta. Integrá el front contra la API real, devolvé cada diferencia a su dueño y corré la puerta una sola vez sobre copias del corte; después de un arreglo, solo lo que toca. No toques el lienzo. La prueba con Meta y la nube queda para la E10, con los pasos clic por clic para el usuario y sin manejar su PC. Dejá solo la tabla final corta en este plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commits solo tuyos, con git commit -- <rutas>, en español y conventional commits; con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada arreglo, build y tests rápidos enteros de su repo, sin Docker y sin filtrar; si tocó rutas, persistencia, autenticación o aislamiento, también la integración y la seguridad del área; el contrato cambia solo anotándolo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Relación con otras etapas

**Usa de las anteriores:**
- **Núcleo, Asistente y E11:** `IFileStore`, `IFileStoreMaintenance`, `FileSignaturePolicy`, `IModuleCapabilityContributor` y los moldes de cada tarea.
- **Base de la 12, E14, E15 y E17:** los nombres de K0.

**Deja para las siguientes:**
- `IFileContentInspector`, `FileContentInspector`, `FileSignaturePolicy` ampliada, `IFileStorageStatus` y la capacidad `form-files`.
- Los tipos `Photo` y `Document`, con `photoSource`, `minFiles` y `maxFiles`; `Forms:MaxFileSizeMiB`; `FormVersions.MaxFileBytes`; el perfil `flow-static-7.3-r10`.
- `FormRecordFile` y `FormRecordFiles`, `FormRecordFileService`, las rutas de archivos de una respuesta y el valor `{kind: "files"}`.
- `IWhatsAppMediaClient` (con `UploadAsync`) y su doble de prueba, `WhatsAppReplyIssuer.IssueFileAsync`, `WhatsAppChatFile`, `PlatformWhatsAppChatFile`, `WhatsAppChatFileService`, `WhatsAppChatFileWorker`, la ruta `attachments`, `WhatsAppChatFileDurableAdapter`, la variante de archivo del outbox y `file` en los mensajes.
- `FormFileCleanupWorker`, `WhatsAppFileCleanupWorker` y `ListReferencedStorageKeysAsync`: cada etapa que guarde en `forms` o `whatsapp` suma ahí su tabla y confirma su clave después del commit.
- La tarjeta `form` con `files`, `Receiving`, `fileCount` y `attemptsLeft`; `PhotoViewer.tsx`, y los errores de la tabla.

## Cambios al contrato


## Para decidir

Nada: el archivo del chat que no se pudo bajar (`Failed`) lo decidió la Guía el 2026-11-08 (F6).
