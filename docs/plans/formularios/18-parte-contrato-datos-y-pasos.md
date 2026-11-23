# E18 · Fotos y documentos: contrato, datos y pasos (spec, parte)

**Estado:** pendiente · parte de la [spec de la E18](18-fotos-y-documentos.md), que dice qué leer y cómo se cierra.

## Contrato

**Formularios,** en `/api/organization/forms`, con `[FeatureGate("forms")]`:

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /{id}/records/{recordId}/files/{fileId}/thumbnail` | `formrecords.read` | — | 200 JPEG, en línea |
| `GET /{id}/records/{recordId}/files/{fileId}/content` | `formrecords.read` | — | 200 el original: en línea si es una foto; si no, adjunto |
| `GET /settings` | `forms.manage` | — | 200 `{maxFileMiB, platformMaxFileMiB, version}`; sin fila, los de por defecto con `version` 0 |
| `PUT /settings` | `forms.manage` | `{maxFileMiB, version}` | 200 `VersionResponse`; el primero crea la fila; 409 con otra versión |

**La Configuración de Formularios de la plataforma** (270), en `/api/platform/forms/settings`, con `platform.settings.manage`: `GET` y `PUT`, con `{maxFileMiB, version}` y el 409, como las de la organización. Bajar el máximo no toca lo que guardó cada organización: en cada uso vale el menor de los dos.

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| `GET /{id}` y `PUT /{id}/draft` | `questions[]`: tipos `Photo` y `Document`, con `photoSource?`, `minFiles?` y `maxFiles?`; `GET /{id}` suma `maxFileMiB`, el tope vigente que dice la (i) (270) |
| `POST /{id}/publication-checks` | `Forms.Draft.TooManyFiles` y `Forms.Publication.FilesUnavailable`; `ResponseTooLarge` (E14) mide contra 64 KiB si hay una pregunta de archivo |
| `GET /{id}/records/columns` | columnas de tipo `Photo` y `Document`, con `sortable` en falso: tienen varios valores (264) |
| `POST …/records/search` y el detalle | el valor `{kind: "files", files[{id, fieldId, name, kind, mediaType, size, hasThumbnail}]}`, con todos los archivos de la pregunta, en orden: la planilla muestra la miniatura del primero y la cantidad, y la respuesta, cada uno en su control (266) |
| `POST …/records/search` | `answered`, `unanswered` y `notAsked` de las preguntas de archivo, para las condiciones; sin faceta, porque la planilla no las filtra (143) |

`GET /api/me` suma la capacidad `form-files`, por `IModuleCapabilityContributor`: dice si hay almacenamiento.

**Chat,** en `WhatsAppConversationsControllerBase`: valen para `customers`, `internal` y Plataforma, con el permiso de cada bandeja, sin la feature de formularios:

| Ruta | Pide | Devuelve |
| --- | --- | --- |
| `POST conversations/{id}/attachments` | multipart: el archivo y `text?`, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]` y `[IdempotencyDurable(typeof(WhatsAppChatFileDurableAdapter))]` | 202 `{messageId}` |
| `GET conversations/{id}/files/{fileId}/thumbnail` | — | 200 JPEG, en línea |
| `GET conversations/{id}/files/{fileId}/content` | — | 200 el original |

Los mensajes de la conversación suman `file?{id, name, kind, mediaType, size, state, hasThumbnail}`, con `state` `Pending`, `Stored`, `Failed` o `Skipped`, el documento de un tipo que no se guarda («Mandó un documento», sin descarga). La tarjeta `form` suma `files[]`, `attemptsLeft?` y `state: Receiving`.

**Puertos:**
- `StructuredRecordCapture` suma `Files[FieldId, Position, StorageKey, ThumbnailKey?, FileName, MediaType, ByteLength, Sha256]`, ya verificados, y `StructuredRecordCard`, los archivos;
- `IFileContentInspector`, en `Application/Interfaces/Integrations/Files/`: `Inspect(stream, declaredKind)` devuelve el tipo real y, si es una foto, sus dimensiones; `CreateThumbnailAsync` arma la miniatura.

**Errores.** Los de Forms van en `Domain/Forms/`: `Forms.Record.File*` en `FormRecordErrors.cs`, `Forms.Draft.*` en `FormDraftErrors.cs` y `Forms.Publication.FilesUnavailable` en `FormPublicationErrors.cs`, de la E14, y `Forms.Settings.Invalid` en `FormSettingsErrors.cs`, nuevo, con sus textos en `FormsTexts{,.en}.resx`. Los `WhatsApp.*` van en `Domain/WhatsApp/WhatsAppErrors.cs`, con sus textos en `WhatsAppTexts{,.en}.resx`.

| Código | Tipo | Cuándo | es | en |
| --- | --- | --- | --- | --- |
| `Forms.Record.FileNotFound` | NotFound | ajeno, inexistente, o falta el archivo guardado | No encontramos el archivo. | We couldn't find the file. |
| `Forms.Draft.TooManyFiles` | Validation (`questions`, con `limit` 10 y `length`, la suma de los máximos) | la suma de los máximos pasa de 10 | WhatsApp acepta 10 archivos por respuesta. | WhatsApp accepts 10 files per response. |
| `Forms.Draft.QuestionInvalid` (E14) | Validation | suma mínimo y máximo de archivos, u origen fuera de regla | el de la E14 | — |
| `Forms.Publication.FilesUnavailable` | ServiceUnavailable | una pregunta de archivo, sin almacenamiento | Falta configurar dónde se guardan los archivos. | File storage isn't set up yet. |
| `Forms.Record.FileRejected` | Validation (`fieldId`) | no coinciden hash, tamaño o tipo | Un archivo no es lo que dice. | A file isn't what it claims to be. |
| `Forms.Record.FileDownloadFailed` | Failure (`fieldId`) | no bajó tras los reintentos | No se pudo bajar un archivo de WhatsApp. | A file couldn't be downloaded from WhatsApp. |
| `Forms.Settings.Invalid` | Validation (`maxFileMiB`) | un tamaño fuera de 1 y el tope de la organización (0069), o en la de la plataforma, fuera de 1 a 25; el texto recibe el máximo como parámetro (270) | Elegí un tamaño entre 1 y {0} MB. | Choose a size between 1 and {0} MB. |
| `WhatsApp.Flow.TooManyFiles` | Validation | más archivos o más de 100 MiB en una respuesta | La respuesta trae más de 10 archivos o más de 100 MiB. | The response has more than 10 files or more than 100 MiB. |
| `WhatsApp.Chat.FileNotFound` | NotFound | ajeno, inexistente, o falta el archivo guardado | No encontramos el archivo. | We couldn't find the file. |
| `WhatsApp.Chat.FileTooLarge` | Validation (`file`, con `limit`) | pasa el tope de Meta para su tipo | El archivo pasa el tope de WhatsApp para su tipo. | The file is over WhatsApp's limit for its type. |
| `WhatsApp.Chat.FileUnsupported` | Validation (`file`) | un tipo que Meta no admite o que no es lo que dice | WhatsApp no acepta este tipo de archivo. | WhatsApp doesn't accept this file type. |

Al adjuntar valen los del envío: `WhatsApp.Conversation.WindowClosed` y los demás de la E11.

## Datos

Cuatro migraciones: `<fecha>_FormSettings.cs`, con la configuración y `FormVersions.MaxFileBytes`, `<fecha>_FormRecordFiles.cs`, `<fecha>_WhatsAppChatFiles.cs` y la de la sesión. Los paquetes nuevos, `SkiaSharp`, sus nativos de Linux y `OpenMcdf`, van en `Directory.Packages.props`, y solo los usa Infrastructure. Cada migración agrega y escribe en el mismo despliegue (`Photo`, `Document` y `Receiving`): es la excepción de la [0060](../../decisions/0060-dos-versiones-conviven.md), anotada en cada una, porque todavía no hay despliegue (la E10 va después del bloque, 123) y volver a la imagen anterior no aplica.

**La Configuración de Formularios** (270), como la del Asistente: `FormOrganizationSettings` (`ITenantOwned`, esquema `tenant`, con RLS, `IAuditable` e `IVersioned`) y `FormPlatformSettings` (esquema `platform`, `IGlobalSecurityAudited` con `PlatformSettingsChanged`, [0050](../../decisions/0050-auditoria-global-de-modulos.md), e `IVersioned`), una fila cada una, con `MaxFileMiB` (smallint, de 1 a 25, 25 por defecto). Se leen de la base en cada uso, sin caché al arrancar; una versión sin `MaxFileBytes`, como la que publica la versión anterior mientras conviven, lee el de por defecto.

**FormRecordFiles** (`ITenantOwned`, esquema `tenant`, con RLS, sin auditoría: es parte de la respuesta)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `Id` | uuid | — |
| `RecordId`, `FieldId`, `Position` | uuid, uuid, smallint | FK compuesta a `FormRecords`; único por los tres |
| `Kind` | texto | `Photo` o `Document` |
| `StorageKey`, `ThumbnailKey` | texto, texto? | opacas, con índice para la limpieza |
| `FileName` | texto ≤255 | como llegó |
| `MediaType`, `ByteLength`, `Sha256` | texto, bigint, texto 64 | los verificados |

**WhatsAppChatFiles** (`ITenantOwned`) y **PlatformWhatsAppChatFiles** (esquema `platform`), con una base común, como los mensajes:

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id`, `ChatId`, `MessageId` | uuid | FK al chat y al mensaje; uno por mensaje |
| `State` | texto | `Pending`, `Stored`, `Failed` o `Skipped` |
| `MetaMediaId` | texto? | el de entrada, como texto |
| `Attempts`, `NextAttemptAtUtc` | int, timestamptz? | los mismos intervalos; índice parcial en `Pending` |
| `StorageKey`, `ThumbnailKey`, `MediaType`, `ByteLength`, `Sha256` | — | como `FormRecordFiles` |
| `FileName` | bytea | cifrado con el conversor del contenido del chat ([0065](../../decisions/0065-contenido-del-chat-cifrado.md)): con el propósito de la organización o el de Plataforma |
| `IdempotencyKey` | texto? | la clave del envío de un operador, única por chat: es el vínculo del modo durable, y se confirma en la misma UoW que el mensaje |

**Otras columnas:** `FormVersions` suma `MaxFileBytes`, el tope sellado al publicar. `WhatsAppFlowSessions` suma el estado `Receiving` y `Attempts`, que muestra la tarjeta. `ProtectedFlowResponse`, del mensaje entrante, guarda hasta 96 KiB, por la respuesta de 64 KiB. Los reintentos los corre la cola de entrada, con el intento en el mensaje, como en la E14, con las esperas de un día de H11. Las categorías de `IFileStore` son `forms` y `whatsapp`. **Las claves no llevan la organización** (decisión de la E10): el aislamiento lo dan estas tablas y la Api.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` y `[E15]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Verificar: tipo, dimensiones, nombre y miniatura | `A/Interfaces/Integrations/Files/IFileContentInspector.cs`, `I/Files/FileContentInspector.cs`; suma a `D/Common/FileSignaturePolicy.cs` | `AssistantAttachmentPolicy.cs` | un archivo de cada tipo, también PowerPoint y texto plano; dañado, cifrado y con la extensión cambiada; bomba de píxeles rechazada sin decodificar; U+202E; arquitectura: Application no usa SkiaSharp |
| B2 | Preguntas, configuración y sellado | suma a `FormQuestionType.cs`, `FormDraft.cs`, `FormDraftEditor.cs` y `FormVersionCreator.cs` [E14]; `D/Forms/FormOrganizationSettings.cs` y `FormPlatformSettings.cs`, `FormSettingsService.cs` y sus dos controllers | los mismos; `AssistantOrganizationSettings.cs`, `AssistantPlatformSettings.cs` y `AssistantSettingsService.cs` | mínimo y máximo; suma de 10; sin almacenamiento; el 409; vale el menor de los dos topes; publicar lo sella y cambiar la configuración no toca lo sellado |
| B3 | El Flow con pantallas | suma a `WhatsAppFlowBuilder.cs` [E14] | el mismo | una pantalla por pregunta; IDs; catálogo en otra pantalla; el tope por archivo sellado en cada selector; el tope de 64 KiB con archivos y de 16 KiB sin ellos |
| B4 | Bajar de Meta | `A/Modules/WhatsApp/Interfaces/Integrations/IWhatsAppMediaClient.cs`, `I/Modules/WhatsApp/WhatsAppMediaClient.cs` | `WhatsAppGraphClient.cs` | hosts, HTTPS, sin redirecciones; 90 s; id como número; token de la organización |
| B5 | La captura con archivos, fuera de la UoW | suma a `WhatsAppFlowReplyVerifier.cs`, a la fase de captura, a `IWhatsAppFlowReplyService` y al tope de la entrada [E14] | `AssistantAttachmentService.cs`: guardar, UoW, confirmar o borrar | opcional omitida; hash distinto; reintentos y rechazo; sin número ni valores en un rechazo; la misma sesión dos veces no duplica; una respuesta con archivos de más de 64 KiB, rechazada y visible |
| B6 | Guardar en Forms | `D/Forms/FormRecordFile.cs`; suma a `FormRecordWriter.cs` y `FormRecordService.cs` [E14]; la configuración y la migración | `FormRecordWriter.cs` | todo o nada; aislamiento |
| B7 | Ver y descargar | `A/Modules/Forms/Services/FormRecordFileService.cs`; suma a `BusinessFormRecordsController.cs` y `FormRecordFileResults.cs` [E15] | `AssistantAttachmentService.DownloadAsync` y `AssistantFileResults.cs` | permiso antes y después de abrir; falta el archivo = 404; `no-store`, `nosniff` y adjunto; de otra organización |
| B8 | Planilla, conteos, detalle y Excel | suma a `FormRecordColumnsBuilder.cs`, `FormRecordReader.cs` y `FormRecordSheetBuilder.cs` [E15] | esos mismos | `answered` con un archivo; cantidad y nombres en el Excel; la columna no ordena (264); sin consultas por fila |
| B9 | Limpiar lo no confirmado, cada hora | `I/Modules/Forms/FormFileCleanupWorker.cs` e `I/Modules/WhatsApp/WhatsAppFileCleanupWorker.cs`; `ListReferencedStorageKeysAsync` | `AssistantRetentionWorker.cs` | a las 24 horas; un archivo referenciado se confirma |
| B10 | Los archivos que llegan al chat, y su purga con los mensajes (272) | `D/WhatsApp/WhatsAppChatFile*.cs`; suma a `WhatsAppOrganizationInboundWriter.cs` y `PlatformWhatsAppInboundWriter.cs`; `A/Modules/WhatsApp/Services/WhatsAppChatFileService.cs`, `I/Modules/WhatsApp/WhatsAppChatFileWorker.cs`; suma al worker que purga los chats (B18 de la base 2) la fila del archivo y, después del commit, su original y su miniatura | `WhatsAppBroadcastWorker.cs`, `WhatsAppChatMessageBase.cs` | cada tipo; lo que no se guarda; reintentos; un mensaje trabado no frena a los demás; organización y Plataforma; el nombre cifrado, que otra organización lee vacío; un mensaje vencido no deja fila ni archivo guardado, y uno vigente no se toca |
| B11 | Mandar un archivo | suma a `WhatsAppReplyIssuer.cs`, `WhatsAppOutboundMessage.cs` y `WhatsAppCloudClient.cs`; la ruta en `WhatsAppConversationsControllerBase.cs`; `P/Modules/WhatsApp/WhatsAppChatFileDurableAdapter.cs` | `AssistantControllerBase.Upload`, `AssistantDurableAdapter.cs` y `WhatsAppReplyIssuer.IssueAsync` | ventana cerrada; topes por tipo; idempotencia; falla al subir a Meta; lo dudoso queda «Sin confirmar» (0066); un archivo por mensaje |
| B12 | Ver | suma a la lectura de mensajes; rutas de archivos | `AssistantAttachmentService.DownloadAsync` | sin permiso de la bandeja; la lista de chats no trae nombres |
| B13 | Capacidad, contratos y ejemplos | `FormsCapabilityContributor.cs`; suma a `FormsContractContributor.cs` y al de WhatsApp | `AssistantCapabilityContributor.cs` | cada ruta con 401, 403, feature apagada = 404; `RouteIsolationMatrixTests`; ejemplos exportados |
| B15 | Acciones del asistente: `forms.settings.edit@1` (`Page`, la Configuración de Formularios de la organización, con `forms.manage`), con `maxFileMiB` (`Integer`, de 1 al máximo). La de Plataforma no se declara: Plataforma todavía no tiene acciones y pide la prueba reciente (0068). | `A/Modules/Forms/Services/FormSettingsActionSource.cs`, registrada en `FormsModule.cs`; sus textos `Action.*` en `FormsTexts{,.en}.resx`; su escenario | `RoleActionSource.cs` | los de arquitectura de las acciones y la batería genérica |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` y `F/src/modules/whatsapp/chats/` | la API de la E17 | zod estricto con los ejemplos |
| F2 | El editor y la Configuración de Formularios | suma a `QuestionEditor.tsx` [E14]; las páginas de la configuración, de la organización y de la plataforma | su tablero; `F/src/modules/assistant/settings/` | el tope en la (i) de «Cuántas fotos» y «Cuántos documentos»; el origen de la foto en la pieza (262); sin almacenamiento; el 409 y el máximo de la plataforma |
| F3 | El visor común | `F/src/shared/ui/PhotoViewer.tsx` | su tablero | teclado y foco; solo las miniaturas visibles; perder el permiso lo cierra; axe |
| F4 | La tarjeta del chat | suma a la tarjeta [E14] | el tablero | los estados; «Mandar de nuevo» |
| F5 | Planilla, respuesta y Excel | suma a `FormRecordsPage.tsx`, `lib/recordForm.ts`, `RecordForm.tsx`, `RecordValues.tsx`, `RecordFullPage.tsx` y `ExportDialog.tsx` [E15] | su tablero | la columna, que no ordena (264); la zona «Fotos y documentos» al final, con cada archivo en su control (266); «Descargar ▾» |
| F6 | Los archivos del chat | suma a `ChatComposer.tsx` y `MessageThread.tsx` | los tableros | adjuntar, grisado fuera de las 24 horas, subiendo y error; un archivo recibido se ve y se baja |
| F7 | Las propuestas del asistente (B15) | `useAssistantProposal` en las dos páginas de la configuración | el adaptador de Roles | todo lo propuesto se ve, y se manda lo que se ve |

**Al cerrar:** `docs/features/formularios.md` y `docs/features/whatsapp.md` suman su sección, y el núcleo, la de `IFileContentInspector`.

## Archivos que los pasos no nombran

- **Dominio y Application:** `D/Forms/FormOrganizationSettings.cs` y `FormPlatformSettings.cs`; `A/Modules/Forms/Interfaces/Services/IFormSettingsService.cs`, `A/Modules/Forms/Interfaces/Persistence/IFormSettingsReader.cs` e `IFormSettingsRepository.cs`, con sus configuraciones, su reader y su repositorio en `I/Modules/Forms/Persistence/`; `A/Modules/WhatsApp/Interfaces/Services/IWhatsAppChatFileService.cs`.
- **`ListReferencedStorageKeysAsync`,** en `A/Modules/Forms/Interfaces/Persistence/IFormFileRepository.cs`, `A/Modules/WhatsApp/Interfaces/Persistence/IWhatsAppChatFileRepository.cs` e `IPlatformWhatsAppChatFileRepository.cs`.
- **Api:** `P/Modules/Forms/Controllers/BusinessFormSettingsController.cs` y `PlatformFormSettingsController.cs`; `Contracts/UpdateOrganizationFormSettingsHttpRequest.cs` y `UpdatePlatformFormSettingsHttpRequest.cs`, con sus dos validadores; los ejemplos del chat, en `Api/Modules/WhatsApp/WhatsAppContractContributor.cs`, que ya existe.
- **Tests:** `tests/…Api.IntegrationTests/Files/FileContentInspectorTests.cs`; `TestFeatures/WhatsAppBrowser/RecorridoWhatsAppMediaClient.cs`; el escenario de las acciones, en `TestFeatures/AssistantActions/`; las tablas nuevas se inscriben en `tests/ArquitecturaBaseMultitenant.ArchitectureTests/PersonalDataPurgeTests.cs`, que crea la B2 de la base 2.
- **Front:** las llamadas de los archivos de una respuesta, en `src/modules/forms/api/records.ts` [E15], y las de la configuración, en `api/settings.ts`; las páginas, `src/modules/forms/settings/OrganizationFormSettingsPage.tsx` y `PlatformFormSettingsPage.tsx`; las del chat, en `src/modules/whatsapp/chats/api/chats.ts`. La galería va en `../ArquitecturaBaseMutitenantFront/scripts/capturas/etapa-18.json`.
