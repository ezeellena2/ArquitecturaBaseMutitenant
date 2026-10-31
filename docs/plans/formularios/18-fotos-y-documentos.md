# E18 · Fotos y documentos (spec)

**Estado:** pendiente · spec de la etapa 18 del bloque (decisiones 34, 48 y 78), con el [formato](plantilla.md) de las etapas. Corresponde a la etapa 2B de ArquitecturaBase, cerrada con Meta y Azure reales, más los archivos del chat, que allá no existen. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E17.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T5, T21, la §6, puntos 6 a 8, la §14 y la §16; la [E15](15-respuestas.md): columnas, filtros, Excel y detalle; y la [E17](17-ciclo-de-vida.md): copiar.
2. **Decisiones** de la hoja de ruta: 23, 35, 42, 43, 48 y 78. Del [plan de la E11](../2026-10-13-etapa-11-whatsapp.md): D4 y D7.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [guardado](../../rules/guardado.md) | subir y bajar van fuera de la UoW; confirmar, después del commit |
   | [modulos](../../rules/modulos.md) | Forms y WhatsApp, por puertos neutrales |
   | [idempotencia](../../rules/idempotencia.md) | adjuntar es un archivo: modo durable |
   | [persistencia-ef](../../rules/persistencia-ef.md) y [multitenancy](../../rules/multitenancy.md) | las tablas nuevas, y las claves de los archivos sin organización |
   | [api-http](../../rules/api-http.md) y [cuotas](../../rules/cuotas.md) | descargas por la Api; cuentan en las cuotas generales |
   | [logs](../../rules/logs.md) y [datos-personales](../../rules/datos-personales.md) | nombres y contenidos nunca van a logs, a la lista de chats ni a los avisos |
   | [textos-libres](../../rules/textos-libres.md) y [textos-y-traducciones](../../rules/textos-y-traducciones.md) | los nombres se guardan como llegan |
   | [tests](../../rules/tests.md) | qué lleva test y dónde va |

4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer: en [`../2026-10-17-formularios-traspaso/`](../2026-10-17-formularios-traspaso/README.md), el anexo C, «Etapa 2B» y §3.6, el anexo B, «Etapa 2B», y el anexo D (F1 a F5).
6. **La maqueta,** en `../MaquetaMultitenant/src/areas/business/forms/`: `components/QuestionEditor.tsx`, `components/records/RecordValues.tsx`, `QuickView.tsx` y `pages/RecordFullPage.tsx`. Allá no están los archivos del chat: los dibuja Diseño.

## Qué se construye

**Las preguntas:**
- **Foto:** «Cámara o galería» o «Solo cámara». **Documento:** PDF, Excel, Word, JPEG y PNG, los que se elijan;
- mínimo y máximo por pregunta. Obligatoria quiere decir mínimo 1;
- el borrador se guarda igual, y no se publica si la suma de los máximos pasa de 10 o no hay almacenamiento. Sin almacenamiento, los tipos se ven grisados, con el motivo en su tooltip;
- **tope por archivo:** `Forms:MaxFileSizeMiB`, de 1 a 25, con `ValidateOnStart`, que se sella con la versión. Hasta 10 archivos y 100 MiB por respuesta, por el tope de Meta;
- copiar un formulario copia estas preguntas como cualquier otra.

**El Flow:** `PhotoPicker` y `DocumentPicker`, una pregunta de archivo por pantalla, con un perfil nuevo. Los IDs de pantalla son solo letras y guiones bajos. Las opciones de catálogo que viajan en `data` llegan a la pantalla que las usa, aunque el Flow tenga varias **[Meta]**.

**Responder con archivos:**
- WhatsApp baja cada archivo de la API de medios con el token de la organización, solo por HTTPS, desde hosts de Meta y sin redirecciones;
- verifica hash, tamaño y tipo real, con las dimensiones antes de decodificar, y arma la miniatura de una foto;
- guarda en `IFileStore`, y le pasa a Forms las claves en la captura. Forms confirma los archivos junto con la respuesta. **Nada queda a medias:** si algo falla, no hay respuesta ni número;
- tiene 90 s por captura, y reintenta a los 30, 90, 210, 450 y 930 s. Si sigue fallando, o si no coinciden hash, tamaño o tipo, queda rechazada y visible, nombrando la pregunta, y se puede mandar de nuevo;
- cada intento sube de nuevo: lo que no quedó confirmado se borra enseguida o lo limpia el worker.

**Verificar un archivo** es una pieza del núcleo, que usan los dos módulos:
- el tipo sale del contenido, nunca de la extensión ni de lo que declara Meta;
- se reconocen JPEG, PNG y WebP, PDF, Word y Excel viejos y OOXML, y los audios y videos de Meta;
- **una foto se rechaza por su encabezado, antes de decodificar:** hasta 16.384 píxeles de lado y 32 Mi píxeles de área;
- un documento dañado o cifrado se rechaza. No se ejecuta ni se renderiza nada;
- **el nombre de descarga** lleva la extensión del tipo verificado y sin caracteres de formato, como U+202E;
- del proveedor no se confía en los tipos: el id de medio puede llegar como número, y se conserva como texto. Una opcional vacía llega omitida.

**Ver los archivos de una respuesta:**
- miniatura y original solo por la Api, con `formrecords.read` y pertenencia, con `no-store` y `nosniff`. El permiso se revisa antes y después de abrir el archivo. Perder el permiso cierra el visor;
- las fotos se ven en línea y el original se baja solo al abrir el visor. Los documentos siempre se descargan;
- si falta el archivo guardado, 404. Las miniaturas se piden solo si están a la vista.

**Lo que suma a las pantallas que ya existen:**
- **la planilla:** una columna por pregunta de archivo, con la primera miniatura y la cantidad, que abre el visor. Un documento dice su cantidad y sus tipos. Sus filtros son «Con respuesta», «Sin respuesta» y «La pregunta no existía»: `answered` también cuenta un archivo;
- **el detalle:** la pestaña «Fotos» en la vista rápida, y en las dos vistas, las miniaturas y los documentos con su nombre, su tamaño y «Descargar»;
- **la pantalla completa** suma «Descargar ▾», con los documentos;
- **el Excel:** la cantidad y los nombres en la celda, sin los archivos;
- **la tarjeta del chat:** «Bajando los archivos» o «Reintentando», guardada, con miniaturas y «+N», o rechazada, con «Mandar de nuevo»;
- **el visor:** anterior, siguiente, tira de miniaturas y Descargar.

**Los archivos del chat** (48), en todos los chats de atención, de una organización y de Plataforma:
- **recibir:** las fotos, los audios, los documentos y los videos que llegan se bajan, se verifican igual y se ven y se descargan desde el chat. Es lo que la D7 de la E11 dejó para esta etapa. Un sticker o una ubicación siguen diciendo solo su tipo;
- **un documento** se guarda si es de los que admite Meta: PDF, Word, Excel, PowerPoint o texto. Otro tipo queda como «Mandó un documento», sin descarga;
- **mandar:** el operador adjunta una foto, un documento, un audio o un video desde el compositor, con los topes de Meta para cada tipo y un texto opcional. Se guarda, se sube a Meta y sale por la cola. Solo dentro de las 24 horas, como el texto libre; fuera, siguen las plantillas;
- pide el mismo permiso que responder en esa bandeja. El archivo sigue la retención del chat (D4): se borra con el contacto y, en Plataforma, con la conversación;
- **no se confunde con un formulario:** los archivos de una respuesta siguen a la respuesta (23), no al chat.

## Pantallas

Parte está aprobada en el lienzo v130, y Diseño la revisa con el [texto de la etapa](18-texto-diseno.md). Muestran solo lo que existe hasta acá (78).

| Tablero | Qué muestra | Estado |
| --- | --- | --- |
| `FRM-Editor-18` y `M-FRM-Editor-18` | las preguntas de foto y documento, «Archivos: hasta N de 10» y «Sin almacenamiento» | A revisar |
| `FRM-Publicar-18` | «Más de 10 archivos» | A revisar |
| `WA-Chats-Respuesta-18` y `M-WA-Conversacion-Respuesta-18` | la tarjeta: bajando, reintentando, con fotos y un documento, muchas fotos y los dos rechazos | A revisar |
| `WA-Chats-Visor` y `M-WA-Chats-Visor` | el visor | A revisar |
| `FRM-Respuestas-18` y `M-FRM-Respuestas-18` | la planilla con la columna de archivos, su filtro en «Más filtros» y el ⓘ de exportar | Por dibujar |
| `FRM-Respuesta-18` y `M-FRM-Respuesta-18` | el detalle, con «Fotos» y el visor | Por dibujar |
| `FRM-Respuesta-Completa-18` | la pantalla completa, con «Descargar ▾» | Por dibujar |
| `WA-Chats-Adjuntos` y `M-WA-Chats-Adjuntos` | los archivos del chat: adjuntar, subiendo, enviado, no se pudo, fuera de las 24 horas y un archivo recibido | Por dibujar |

**Mandan sobre esta spec en:**
- **«Sin almacenamiento»:** Foto y Documento aparecen grisados, con el motivo en su tooltip (43), no ocultos, porque se resuelve conectando el almacenamiento.
- **«Más de 10 archivos»** es un problema de la comprobación al publicar, con el botón grisado.
- **«Un archivo no es lo que dice»** es el rechazo por tipo real, y «No se pudo bajar un archivo», el rechazo después de los reintentos. Los dos nombran la pregunta y ofrecen «Mandar de nuevo».
- **«Reintentando»** se ve mientras corren los reintentos, sin acción del operador.
- **Fuera de las 24 horas,** adjuntar queda grisado, con el motivo en su tooltip.
- **Un archivo de un mensaje enviado** que no salió queda «No se pudo», sin reintento.

## Contrato

**Formularios,** en `/api/organization/forms`, con `[FeatureGate("forms")]`:

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /{id}/records/{recordId}/files/{fileId}/thumbnail` | `formrecords.read` | — | 200 JPEG, en línea |
| `GET /{id}/records/{recordId}/files/{fileId}/content` | `formrecords.read` | — | 200 el original: en línea si es una foto; si no, adjunto |

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| `GET /{id}` y `PUT /{id}/draft` | `questions[]`: tipos `Photo` y `Document`, con `photoSource?`, `documentKinds?`, `minFiles?` y `maxFiles?` |
| `POST /{id}/publication-checks` | `Forms.Draft.TooManyFiles` y `Forms.Publication.FilesUnavailable` |
| `GET /{id}/records/columns` | columnas de tipo `Photo` y `Document` |
| `POST …/records/search` y el detalle | el valor `{kind: "files", files[{id, fieldId, name, kind, mediaType, size, hasThumbnail}]}`. La planilla trae el primero y la cantidad |
| `POST …/records/facets` | `answered`, `unanswered` y `notAsked` de las preguntas de archivo |

`GET /api/me` suma la capacidad `form-files`, por `IModuleCapabilityContributor`: dice si hay almacenamiento.

**Chat,** en `WhatsAppConversationsControllerBase`: valen para `customers`, `internal` y Plataforma, con el permiso de cada bandeja, sin la feature de formularios:

| Ruta | Pide | Devuelve |
| --- | --- | --- |
| `POST conversations/{id}/attachments` | multipart: el archivo y `text?`, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]` | 202 `{messageId}` |
| `GET conversations/{id}/files/{fileId}/thumbnail` | — | 200 JPEG, en línea |
| `GET conversations/{id}/files/{fileId}/content` | — | 200 el original |

Los mensajes de la conversación suman `file?{id, name, kind, mediaType, size, state, hasThumbnail}`, con `state` `Pending`, `Stored` o `Failed`. La tarjeta `form` suma `files[]`, `attemptsLeft?` y `state: Receiving`.

**Puertos:**
- `StructuredRecordCapture` suma `Files[FieldId, Position, StorageKey, ThumbnailKey?, FileName, MediaType, ByteLength, Sha256]`, ya verificados, y `StructuredRecordCard`, los archivos;
- `IFileContentInspector`, en `Application/Interfaces/Integrations/Files/`: `Inspect(stream, declaredKind)` devuelve el tipo real y, si es una foto, sus dimensiones; `CreateThumbnailAsync` arma la miniatura.

**Errores** (con sus textos en `FormsTexts` y en los del módulo WhatsApp):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Record.FileNotFound` | NotFound | ajeno, inexistente, o falta el archivo guardado |
| `Forms.Draft.TooManyFiles` | Validation (`questions`) | la suma de los máximos pasa de 10 |
| `Forms.Draft.QuestionInvalid` | Validation | suma mínimo y máximo de archivos, u origen o tipos fuera de regla |
| `Forms.Publication.FilesUnavailable` | ServiceUnavailable | una pregunta de archivo, sin almacenamiento |
| `Forms.Record.FileRejected` | Validation (`fieldId`) | no coinciden hash, tamaño o tipo |
| `Forms.Record.FileDownloadFailed` | Failure (`fieldId`) | no bajó tras los reintentos |
| `WhatsApp.Flow.TooManyFiles` | Validation | más archivos o más de 100 MiB en una respuesta |
| `WhatsApp.Chat.FileNotFound` | NotFound | ajeno, inexistente, o falta el archivo guardado |
| `WhatsApp.Chat.FileTooLarge` | Validation (`file`) | pasa el tope de Meta para su tipo |
| `WhatsApp.Chat.FileUnsupported` | Validation (`file`) | un tipo que Meta no admite o que no es lo que dice |

Al adjuntar valen los del envío: `WhatsApp.Conversation.WindowClosed` y los demás de la E11.

## Datos

Tres migraciones: `<fecha>_FormRecordFiles.cs`, `<fecha>_WhatsAppChatFiles.cs` y la de la sesión. Los paquetes nuevos, `SkiaSharp`, sus nativos de Linux y `OpenMcdf`, van en `Directory.Packages.props`, y solo los usa Infrastructure.

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
| `StorageKey`, `ThumbnailKey`, `FileName`, `MediaType`, `ByteLength`, `Sha256` | — | como `FormRecordFiles` |

**Otras columnas:** `FormVersions` suma `MaxFileBytes`. `WhatsAppFlowSessions` suma el estado `Receiving`, `Attempts` y `NextAttemptAtUtc`. Las categorías de `IFileStore` son `forms` y `whatsapp`. **Las claves no llevan la organización** (decisión de la E10): el aislamiento lo dan estas tablas y la Api.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front; `M/`, la maqueta, y `[E14]` y `[E15]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Verificar: tipo, dimensiones, nombre y miniatura | `A/Interfaces/Integrations/Files/IFileContentInspector.cs`, `I/Files/FileContentInspector.cs`; suma a `D/Common/FileSignaturePolicy.cs` | `AssistantAttachmentPolicy.cs` | un archivo de cada tipo; dañado, cifrado y con la extensión cambiada; bomba de píxeles rechazada sin decodificar; U+202E; arquitectura: Application no usa SkiaSharp |
| B2 | Preguntas, opción y sellado | suma a `FormQuestionType.cs`, `FormDraft.cs`, `FormDraftEditor.cs` y `FormVersionCreator.cs` [E14]; `A/Configuration/Forms/FormsFilesOptions.cs` | los mismos; `WhatsAppOptions.cs` | mínimo y máximo; suma de 10; sin almacenamiento |
| B3 | El Flow con pantallas | suma a `WhatsAppFlowBuilder.cs` [E14] | el mismo | una pantalla por pregunta; IDs; catálogo en otra pantalla; presupuesto de respuesta |
| B4 | Bajar de Meta | `A/Modules/WhatsApp/Interfaces/Integrations/IWhatsAppMediaClient.cs`, `I/Modules/WhatsApp/WhatsAppMediaClient.cs` | `WhatsAppGraphClient.cs` | hosts, HTTPS, sin redirecciones; 90 s; id como número; token de la organización |
| B5 | La captura con archivos, fuera de la UoW | suma a `WhatsAppFlowReplyVerifier.cs`, a la fase de captura y a `IWhatsAppFlowReplyService` [E14] | `AssistantAttachmentService.cs`: guardar, UoW, confirmar o borrar | opcional omitida; hash distinto; reintentos y rechazo; sin número ni valores en un rechazo; la misma sesión dos veces no duplica |
| B6 | Guardar en Forms | `D/Forms/FormRecordFile.cs`; suma a `FormRecordWriter.cs` y `FormRecordService.cs` [E14]; la configuración y la migración | `FormRecordWriter.cs` | todo o nada; aislamiento |
| B7 | Ver y descargar | `A/Modules/Forms/Services/FormRecordFileService.cs`; suma a `BusinessFormRecordsController.cs` y `FormRecordFileResults.cs` [E15] | `AssistantAttachmentService.DownloadAsync` y `AssistantFileResults.cs` | permiso antes y después de abrir; falta el archivo = 404; `no-store`, `nosniff` y adjunto; de otra organización |
| B8 | Planilla, conteos, detalle y Excel | suma a `FormRecordColumnsBuilder.cs`, `FormRecordReader.cs` y `FormRecordSheetBuilder.cs` [E15] | esos mismos | `answered` con un archivo; cantidad y nombres en el Excel; sin consultas por fila |
| B9 | Limpiar lo no confirmado, cada hora | `I/Modules/Forms/FormFileCleanupWorker.cs` y su parte en WhatsApp; `IsStorageReferencedAsync` | `AssistantRetentionWorker.cs` | a las 24 horas; un archivo referenciado se confirma |
| B10 | Los archivos que llegan al chat | `D/WhatsApp/WhatsAppChatFile*.cs`; suma a `WhatsAppOrganizationInboundWriter.cs` y `PlatformWhatsAppInboundWriter.cs`; `A/Modules/WhatsApp/Services/WhatsAppChatFileService.cs`, `I/Modules/WhatsApp/WhatsAppChatFileWorker.cs` | `WhatsAppBroadcastWorker.cs`, `WhatsAppChatMessageBase.cs` | cada tipo; lo que no se guarda; reintentos; un mensaje trabado no frena a los demás; organización y Plataforma |
| B11 | Mandar un archivo | suma a `WhatsAppReplyIssuer.cs`, `WhatsAppOutboundMessage.cs` y `WhatsAppCloudClient.cs`; la ruta en `WhatsAppConversationsControllerBase.cs` | `AssistantControllerBase.Upload` y `WhatsAppReplyIssuer.IssueAsync` | ventana cerrada; topes por tipo; idempotencia; falla al subir a Meta; un archivo por mensaje |
| B12 | Ver, retención y baja | suma a la lectura de mensajes, `WhatsAppContactRemover.cs` y la retención de Plataforma; rutas de archivos | `AssistantRetentionWorker.cs` | borrar el contacto borra sus archivos; sin permiso de la bandeja; la lista de chats no trae nombres |
| B13 | Capacidad, contratos y ejemplos | `FormsCapabilityContributor.cs`; suma a `FormsContractContributor.cs` y al de WhatsApp | `AssistantCapabilityContributor.cs` | cada ruta con 401, 403, feature apagada = 404; `RouteIsolationMatrixTests`; ejemplos exportados |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` y `F/src/modules/whatsapp/chats/` | `M/…/api/records.ts` | zod estricto con los ejemplos |
| F2 | El editor | suma a `QuestionEditor.tsx` [E14] | `M/…/QuestionEditor.tsx` | «Archivos: hasta N de 10»; sin almacenamiento |
| F3 | El visor común | `F/src/shared/ui/PhotoViewer.tsx` | `M/…/records/RecordValues.tsx` | teclado y foco; solo las miniaturas visibles; perder el permiso lo cierra; axe |
| F4 | La tarjeta del chat | suma a la tarjeta [E14] | el tablero | los estados; «Mandar de nuevo» |
| F5 | Planilla, detalle y Excel | suma a `FormRecordsPage.tsx`, `QuickView.tsx`, `RecordFullPage.tsx`, `RecordValues.tsx` y `ExportDialog.tsx` [E15] | `M/…` | columna, pestaña «Fotos» y «Descargar ▾» |
| F6 | Los archivos del chat | suma a `ChatComposer.tsx` y `MessageThread.tsx` | los tableros | adjuntar, grisado fuera de las 24 horas, subiendo y error; un archivo recibido se ve y se baja |

**Al cerrar:** `docs/features/formularios.md` y `docs/features/whatsapp.md` suman su sección, y el núcleo, la de `IFileContentInspector`.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E10] | El adaptador de nube de `IFileStore` y de `IFileStoreMaintenance`. | En desarrollo alcanza `LocalFileStore`; las claves son del almacenamiento y opacas. |
| [E10] | Cómo se sabe que no hay almacenamiento. | La capacidad `form-files` se apaga sin adaptador configurado. |
| [E14] | La fase de captura, la sesión y el presupuesto de 16 KiB. | Se pueden sumar `Receiving` y los reintentos, y lo de cada archivo entra en el presupuesto. |
| [E15] | Los nombres de las piezas de lectura. | Los de la columna «Archivos». |
| [E11] | El mensaje `Media` del chat y el payload de `WhatsAppOutboundMessage`. | La entrada guarda el id de medio de Meta, que hoy descarta; el outbox lleva la clave del archivo. |
| [Meta] | `PhotoPicker`, `DocumentPicker` y varias pantallas con `data`, sin endpoint. | Lo de la §14 de la E14, y `${data.x}` llega a cada pantalla. |
| [Meta] | Los topes de cada tipo y `POST {número}/media`, contra la documentación vigente. | Foto 5 MB, audio y video 16 MB, documento 100 MB. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Meta exige IDs de pantalla de solo letras y guiones bajos.**
2. **El id de medio puede llegar como número JSON,** y se conserva su texto, sin convertirlo.
3. **Meta omite la propiedad de una opcional vacía:** no manda `null`, `""` ni una lista vacía.
4. **Limpieza cuadrática:** consultar por página, nunca una consulta por archivo.
5. **Las dimensiones se miran antes de decodificar.**

## Decisiones tomadas (no se vuelven a preguntar)

- **Formatos de una pregunta:** sin CSV (Meta no lo admite), video ni comprimidos.
- **Nada a medias:** si un archivo no baja, o no coincide, la respuesta queda rechazada y visible.
- **Reintentos simples:** bajar de Meta y subir son operaciones que se pueden repetir, sin protocolo durable. Meta guarda los archivos hasta 20 días.
- **Los archivos siguen a la respuesta,** sin plazo mientras exista la organización (23).
- **Sin clave determinística:** el almacenamiento elige la clave, y el diario de `IFileStoreMaintenance` cierra lo que quedó sin confirmar.
- **Un solo archivo por mensaje** en el chat, como en Meta.

## Fuera de esta etapa

- Exportar los archivos y descargar todas las fotos juntas.
- El antivirus, en la lista de lanzamiento: es requisito antes de producción.
- La copia de «Mis mensajes» sigue diciendo el tipo, sin el archivo.
- Video, comprimidos y CSV en una pregunta.

## Cómo se cierra

La puerta de la etapa, más una **prueba real con Meta y el almacenamiento de la E10**:
1. una respuesta con cámara, un PDF y una pregunta de archivo opcional vacía;
2. las descargas íntegras, con el tamaño y el SHA-256 iguales a los guardados;
3. un rechazo y el reenvío manual, sin saltos de numeración;
4. en un chat de Clientes, ver y bajar una foto que mandó el contacto, y mandarle un PDF y una foto desde el compositor, abriéndolos en el celular;
5. un formulario con una pregunta de catálogo y dos de archivo, cada una en su pantalla: las opciones llegan a su pantalla y la respuesta vuelve completa.

## Requisitos previos

- La E17 cerrada.
- **De la E10:** el adaptador de nube de `IFileStore`. En desarrollo alcanza con `LocalFileStore`.

## En el multitenant

- **Datos de la organización:** `FormRecordFiles` y `WhatsAppChatFiles` son `ITenantOwned`, con RLS. Plataforma, en su esquema global.
- **Almacenamiento:** un solo contenedor privado, sin prefijo por organización. Borrar lo de una organización recorre esa metadata.
- **Workers:** la captura con archivos es la fase de la E14, sin `ExecutionActor`; la limpieza y los archivos del chat recorren las organizaciones con `TenantJobRunner`.
- **Feature `forms`:** las rutas de los archivos de las respuestas la llevan. Las del chat son de WhatsApp: van sin ella y valen también en Plataforma.
- **Permisos:** ninguno nuevo.
- **WhatsApp (E11):** la API de medios se llama con el token de la WABA de la organización.
- **Cuotas:** las lecturas cuentan en las cuotas generales.
