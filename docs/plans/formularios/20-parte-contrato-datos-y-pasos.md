# E20 · Cargar una respuesta desde la web: armado, contrato, datos y pasos (spec, parte)

**Estado:** pendiente · parte de la [spec de la E20](20-cargar-desde-la-web.md), que dice qué leer y cómo se cierra.

## Cómo se arma cada pregunta (86, 87, 141, 262 y 266)

El autor decide el contenido, y la app, la forma (269). La carga usa la forma que arma la E15 para leer una respuesta y la vuelve editable (266): las zonas, la grilla de hasta tres por renglón, medida sobre la zona con `ResizeObserver`, y la tabla de los dos anchos están en su [«Cómo se lee una respuesta»](15-parte-datos-y-contrato.md#cómo-se-lee-una-respuesta-263-266-y-269), con lo que suman la E18 y la E19. Esta etapa suma la ubicación, en una columna, y lo que sigue:

- **Los controles se pueden tocar.** El Sí o No, que arranca sin nada marcado, elegir una y elegir varias van en la pieza `Picker` y ocupan una columna (262, que cambia la 141): se van las opciones en fila y las casillas en columnas. En la pieza cerrada, las varias elegidas que no entran dicen «y N más» y el campo no crece; con un tope, la lista dice «Hasta 5» y, al llegar, «Elegiste 5 de 5». El texto largo tiene de 2 a 6 renglones.
- **Zonas** (88): las fotos y los documentos van al final, en «Fotos y documentos». Plegada, la banda de los archivos dice lo que se adjuntó, «2 fotos · 1 archivo», o «0 de 1 respondidas» sin archivos (166). El bloque cuenta como una pregunta, respondida si algún ítem tiene una respuesta. Al enviar con errores, la zona plegada que los tiene se abre sola.
- **Debajo de un control** va solo el error de su pregunta (263).
- **La unidad** va en la etiqueta que escribe el autor (86).
- **Lo condicional:** una pregunta aparece en su lugar y los renglones se recalculan. Lo oculto no se manda, y una zona sin preguntas visibles no se ve. **Un cálculo** con un dato de una pregunta oculta, o uno que falta, queda «Sin respuesta», como en el servidor.

## El bloque en la carga (146, 177 y 183)

- en la página, ocupa el renglón entero de su zona, donde lo puso el autor, con su nombre como etiqueta: una tabla con una fila por ítem y una columna por pregunta, con su etiqueta arriba; un cálculo del ítem es una columna más, en gris (233 y 234). El número del ítem va en la primera columna, y ↑ ↓ y 🗑, en rojo, al final de cada fila; el tacho quita sin preguntar, como un archivo. Si no entra, la tabla se desplaza de costado en su caja, y el texto largo es una celda que crece;
- arranca con el mínimo de ítems. «Agregar ítem», al pie y sin contador, suma uno vacío y lleva el foco a su primera pregunta. Con el máximo se apaga, con «Hasta 20 ítems.» en su tooltip, y con el mínimo, el tacho, con el suyo. ↑ ↓ dejan el foco en el mismo botón del ítem movido, y quitar lo lleva al ítem que queda en su lugar o a «Agregar ítem»;
- al enviar, cada ítem se valida como preguntas sueltas: la celda con un error se marca, el mensaje va debajo de su fila con el nombre de la pregunta («Cantidad: completá esta respuesta.») y el foco va a la primera. Si faltan ítems, lo dice debajo de la tabla: «Agregá al menos 1 ítem.»;
- **en el panel y en el teléfono,** un recuadro por ítem, con «Ítem 2», ↑ ↓ y 🗑 en su banda (183 y 261, como en la tabla), y las preguntas una debajo de otra. El nombre del bloque va arriba, como etiqueta, así la banda entra en 320. En el teléfono, «Agregar ítem» va al pie de los recuadros, y Enviar sigue fijo abajo;
- «Mantener» conserva también los ítems; una versión nueva los conserva si el bloque sigue, con las preguntas que siguen con el mismo tipo, y «Enviada» muestra el bloque en una línea, como su columna en la planilla;
- **en el editor,** la (i) del máximo suma la app, con el texto de abajo.

## Los textos

Los del bloque:

| Dice | En inglés |
| --- | --- |
| «Agregar ítem» · «Hasta 20 ítems.» · «Agregá al menos 1 ítem.» | «Add item» · «Up to 20 items.» · «Add at least 1 item.» |
| «Ítem 2», en la banda de un recuadro | «Item 2» |
| «En WhatsApp se cargan hasta 10 ítems; en la app, hasta el máximo.» | «WhatsApp loads up to 10 items; the app, up to the maximum.» |

Los de «Dónde se completa» (229):

| Dice | En inglés |
| --- | --- |
| «Dónde se completa» · «En la app y por WhatsApp» · «Solo en la app» | «Where it's completed» · «In the app and on WhatsApp» · «Only in the app» |
| «Los clientes responden solo por WhatsApp.», el tooltip de «Solo en la app» apagada | «Customers answer only on WhatsApp.» |
| «Pasar a «Solo en la app»», en «Problemas» y debajo de una condición | «Switch to “Only in the app”» |
| «Se completa solo en la app.», en «Enviar formulario» | «It's completed only in the app.» |
| «App · No se carga más», en «Dar de baja» | «App · No longer filled in» |
| «Usa una lista dependiente, y WhatsApp no las muestra.» · «Por WhatsApp se pide una sola ubicación.», en «Problemas», debajo del nombre de la pregunta | «It uses a dependent list, and WhatsApp doesn't show them.» · «Only one location can be requested on WhatsApp.» |
| sin número, en «Avisos»: «Queda solo para la app» · «La organización no tiene un número de WhatsApp conectado. Para mandarlo cuando se conecte, hay que volver a publicar.» | «Only for the app» · «The organization has no WhatsApp number connected. To send it once one is connected, publish again.» |
| sin número, uno solo para clientes, en «Problemas»: «No se puede publicar» · «Este formulario es solo para clientes, y ellos responden por WhatsApp. Conectá un número o cambiá «Para quién es».» · «Ir a «Para quién es»» | «It can't be published» · «This form is for customers only, and they answer on WhatsApp. Connect a number or change “Who it's for”.» · «Go to “Who it's for”» |

Los de «Ubicación» (230). El punto se escribe igual en los dos idiomas, con punto decimal y seis decimales: «-34.603722, -58.381592»; la precisión sigue el idioma.

| Dice | En inglés |
| --- | --- |
| «Ubicación» · «Ubicación · Obligatoria» | «Location» · «Location · Required» |
| «Usar mi ubicación» · «Buscando tu ubicación…» | «Use my location» · «Finding your location…» |
| «Ver en el mapa» · «Volver a pedirla» · «Quitar la ubicación», el nombre de la ✕ | «View on the map» · «Ask for it again» · «Remove the location» |
| «± 15 m» · «± 2.400 m» | «± 15 m» · «± 2,400 m» |
| la (i) de más de 100 m: «Es una ubicación aproximada: sin GPS, el navegador la calcula por la red y puede errar por kilómetros. Desde un celular sale más precisa.» | «It's an approximate location: without GPS, the browser estimates it from the network and can be off by kilometers. A phone gives a more precise one.» |
| sin permiso: «No hay permiso para usar tu ubicación. Permitilo en la configuración del sitio, en el navegador, y volvé a tocar «Usar mi ubicación».» | «There's no permission to use your location. Allow it in the browser's site settings and press “Use my location” again.» |
| sin señal o sin tiempo: «No se pudo obtener tu ubicación. Revisá que la ubicación del dispositivo esté activada y volvé a tocar «Usar mi ubicación».» | «Your location couldn't be obtained. Check that the device's location is on and press “Use my location” again.» |
| el motivo del botón apagado: «Este navegador no da la ubicación. Abrí el formulario en otro navegador o en el celular.» | «This browser doesn't provide the location. Open the form in another browser or on a phone.» |
| el Excel: «Visita · Latitud», «Visita · Longitud» y «Visita · Precisión (m)» | «Visita · Latitude», «Visita · Longitude» and «Visita · Accuracy (m)» |
| por WhatsApp: «Para completar «Visita a cliente», mandanos tu ubicación.» · «La ubicación es opcional.» · «Seguir sin ubicación», que Meta corta en 20 caracteres | «To fill in “Visita a cliente”, send us your location.» · «The location is optional.» · «Continue without it» |
| en el detalle: «Se pidió la ubicación» · «Llegó la ubicación» · «Siguió sin la ubicación» | «Location requested» · «Location received» · «Continued without the location» |
| en el chat: «Pedido de ubicación» · «Esperando la ubicación» · «Ubicación recibida» · «Siguió sin la ubicación» · «Venció sin la ubicación» · «El pedido de ubicación venció.» | «Location request» · «Waiting for the location» · «Location received» · «Continued without the location» · «Expired without the location» · «The location request expired.» |

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `formrecords.create`, aparte de las de `formrecords.read`. La organización y quien carga salen de la sesión.

| Ruta | Pide | Devuelve |
| --- | --- | --- |
| `GET /entry` | `page`, `pageSize`, `search`, `status` (`active`, `deactivated`), `sort` (`name`, `ownCount`, `lastOwnNumber`, `lastOwnAtUtc`, 264) | `PagedResult<{id, name, deactivatedAtUtc?, canLoad, ownCount, lastOwn?: {id, displayNumber, submittedAtUtc}}>`, con lo suyo y sin los totales de la organización (153): lo que puede cargar, con el filtro de disponibilidad; los activos solo para clientes, con `canLoad: false`, para mostrarlos apagados con su motivo (83), o los dados de baja en los que cargó algo (101) |
| `GET /{id}/entry` | — | `{versionId, versionNumber, name, submitLabel, maxFileBytes, questions[], titles[]}`. Cada pregunta de catálogo trae `options?{total, single?, missingData?}`, y una lista dependiente, de qué pregunta depende. El bloque trae `rows{minRows, maxRows}` y sus preguntas, `rowsOf`, como en la E19. Un cálculo trae su `calculation`, con la forma de la E19, para la vista previa en gris (234), y una ubicación, el tipo `location` |
| `GET /{id}/entry/questions/{fieldId}/options` | `page`, `pageSize` (50), `search`, `dependsOn?`; sirve también para una pregunta de catálogo del bloque | `PagedResult<{id, text, detail?, fields?}>`; `fields` trae el valor de cada campo numérico que usa una fórmula, para la vista previa |
| `POST /{id}/records` | multipart: `versionId`, `answers` y los archivos, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]`, `[IdempotencyDurable]`, `[RequestSizeLimit]` de 101 MiB y el lugar de subida (B4). El bloque va en `answers` como `{fieldId, rows: [[{fieldId, value}, …], …]}`, un arreglo por ítem, en orden, y un ítem vacío es `[]`. Una ubicación es `{latitude, longitude, accuracyMeters?}`, y un cálculo no se manda | 201 `{recordId, displayNumber, submittedAtUtc, calculations[]}` + Location |
| `GET /{id}/own-records/columns`, `POST …/search`, `…/facets` y `…/facet-options`, `GET …/{recordId}`, `POST …/{recordId}/navigation` y `GET …/{recordId}/files/{fileId}/{thumbnail\|content}` | los de la E15 y la E18 | lo mismo, solo con lo de esa persona |

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| `GET /` y `GET /overview` | `canLoad` y `appOnly`, el de la versión activa o, sin versión, el del borrador; `overview` suma `whatsAppConnected`, que muestra las columnas de la 229; `sort` suma `appOnly` (264) |
| `GET /{id}` y `PUT /{id}/draft` | `draft.appOnly`, omitido si es false; `whatsAppConnected`; el tipo `location`; `recipientFilters[].mode` suma `answer`, con `sourceFieldId` |
| `GET /{id}/lifecycle` | `appOnly` y `loadsInApp` (para empleados o para los dos), de la versión activa |
| `POST /{id}/publication-checks` | `appOnly`, dónde se publica: lo del borrador o, sin número, la app. Con «En la app y por WhatsApp», cada problema que WhatsApp no puede mostrar suma `whatsApp: true`, que el front ofrece pasar a «Solo en la app»: los del canal (`ChannelIssues`, `CatalogTooLarge`, `RowsMinimumOverLimit`, `ConditionNotSupported`) y los nuevos `DependentListNotSupported` y `TooManyLocations`. Con «Solo en la app», sin ellos. Sin número, sin ellos y con el aviso `NoConnectedNumber` en `warnings[]`, sin `fieldId`; uno solo para clientes frena con `ChannelUnavailable` (98) |
| `POST /{id}/publications` | 200 si se publica en la app, porque termina en la misma UoW; si no, 202, como siempre |
| `GET …/conversations/{id}/forms` | los formularios de la app, con `appOnly: true` y sin versión, preguntas ni `optionCounts`, para mostrarlos apagados (184). Uno publicado sin número, con un número conectado, va con `NotPublishedForNumber` (103) |
| Los mensajes del chat [E11 y E14] | un mensaje saliente «Pedido de ubicación», `{formName, state}`, con `state` `Waiting`, `Received`, `Skipped` o `Expired`, y al vencer, la línea «El pedido de ubicación venció.»; uno entrante `location`, `{latitude, longitude}`, con «Ver en el mapa». Son contenido del chat y se guardan cifrados ([ADR 0065](../../decisions/0065-contenido-del-chat-cifrado.md)) |
| `POST …/records/facets` y `search` | la dimensión y el filtro `channel`; una ubicación filtra solo con `answered`, `unanswered` y `notAsked`, y no ordena |
| `GET …/records/{recordId}` | `enteredBy?{name, atUtc}`; la ubicación, `{latitude, longitude, accuracyMeters?}`, y los momentos `locationRequested`, `locationReceived` y `locationSkipped`, que la actividad muestra como renglones (267) |
| `POST …/records/export` | «App» en «Canal» y las tres columnas de una ubicación, vacías sin respuesta y «—» si la pregunta no existía |

**Puerto:** «sin número» sale de `HasConnectedNumberAsync`, de la E14. `ListSendableAsync` trae marcadas las versiones de la app. `StructuredFormVersion` suma `AppOnly` y la ubicación; `StructuredRecordCapture` suma el punto, que pone el envío y nunca el Flow.

**Errores:** los `Forms.Entry.*`, en `D/Forms/FormEntryErrors.cs`, nuevo; los demás, en sus archivos de la E14 y en `WhatsAppErrors.cs`.

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Entry.VersionChanged` | Conflict | se publicó otra versión |
| `Forms.Entry.Deactivated` | Conflict | el formulario se dio de baja |
| `Forms.Entry.NotForEmployees` | Conflict | el formulario es solo para clientes |
| `Forms.Entry.QuestionNotFound` | NotFound | una pregunta que no es de la versión |
| `Forms.Entry.TooLarge` | PayloadTooLarge (413) | el envío pasa de 101 MiB |
| `Forms.Entry.UploadBusy`, `UploadQueueFull` y `UploadTimedOut` | TooManyRequests (429, `Retry-After`), y Validation el tercero | otra subida suya en curso; la copia sin lugar; el cuerpo que no llegó a tiempo (0064 §4) |
| `Forms.Entry.AnswersInvalid` | Validation (`questions.<id>`) | cada error en su pregunta, con los códigos de la captura. El de un ítem va en `questions.<bloque>.<n>.<pregunta>`, con `n` desde 0 |
| `Forms.Draft.DependentListInvalid` | Validation (`questions[i].recipientFilters[j]`) | la pregunta de la que depende no va antes, no es de elegir una, elige de otro catálogo o es del bloque, o la lista está en el bloque |
| `Forms.Draft.DependentListForCustomers` | Validation (`questions[i]`) | una lista dependiente en un formulario solo para clientes |
| `Forms.Draft.AppOnlyForCustomers` | Validation (`appOnly`) | «Solo en la app» en uno solo para clientes |
| `WhatsApp.Flow.DependentListNotSupported` y `TooManyLocations` | Validation (`questions[i]`), en `issues[]` | con «En la app y por WhatsApp», una lista dependiente; una segunda ubicación |
| `Forms.Record.LocationInvalid` | Validation (`questions.<id>`) | una latitud fuera de -90 a 90, una longitud fuera de -180 a 180, una precisión fuera de 0 a 100.000 m u otras claves |

Valen `Forms.Record.OptionWithdrawn`, en su pregunta, y `Forms.Offer.RecipientDataMissing`, con el nombre del dato. `Forms.Draft.RowsInvalid`, `ConditionInvalid` y `FormulaInvalid` (E19) suman la ubicación en el bloque, en una condición y en una fórmula.

## Datos

Dos migraciones aditivas: `<fecha>_FormEntry.cs` (B5), con `AppOnly`, y `<fecha>_WhatsAppFlowLocation.cs` (B9), con las columnas de la sesión. Sin tablas nuevas. Cada una agrega y escribe en el mismo despliegue (`web`, `location` y los estados de la ubicación): es la excepción de la [0060](../../decisions/0060-dos-versiones-conviven.md), anotada en cada una, porque todavía no hay despliegue (123).

| Tabla | Columna | Tipo | Regla |
| --- | --- | --- | --- |
| `FormDefinitions` | `AppOnly` | bool, false | «Dónde se completa» del borrador, como `Audience`; `draft.appOnly` se omite si es false, así las huellas no cambian |
| `FormVersions` | `AppOnly` | bool, false | se sella al publicar: lo del borrador o, sin número, la app. La versión sella también lo elegido, así el chat distingue la publicada sin número (103) |
| `FormRecords` | `Channel` | texto | admite `web` |
| `WhatsAppFlowSessions` [E14] | `LocationState?`, `ProtectedLocation?` | texto, texto | `Requested`, `Received` o `Skipped`; el punto que llegó, cifrado con `IPayloadProtector`, que se vacía al cerrar la captura o al vencer el envío |

**La ubicación,** sin columnas nuevas: tres filas de `FormRecordValues` con `NumberValue`, `Position` 0 la latitud, 1 la longitud y 2 la precisión, si vino, redondeadas a seis decimales con `AwayFromZero`; `Type` admite `location`.

Quien cargó es `RespondentUserId`. `SourceKey` es `web:<clave>`, con el mismo índice único.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E19]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Una sola captura: separar lo común de WhatsApp | suma a `FormRecordService.cs` y `FormRecordWriter.cs` [E14] | esos mismos | WhatsApp sigue igual; numeración correlativa entre los dos canales; las opciones elegidas se revisan por lote, en una consulta por pregunta |
| B2 | Leer para cargar: versión, opciones y filtros en SQL | `A/Modules/Forms/Services/FormEntryService.cs`, `FormEntryOptionsResolver.cs`; suma a `FormOfferBuilder.cs` [E16] | `FormStructuredSource.cs` [E14] | una sola opción; sin el dato; 10.000 opciones con búsqueda; miembros y contactos; un catálogo del bloque, con sus filtros; otra organización; la fórmula y los `fields` de la vista previa |
| B3 | Listas dependientes | suma a `FormRecipientFilterPolicy.cs` [E16] y a `FormDraftEditor.cs` | esos mismos | cada motivo, también una pregunta del bloque; opción que no corresponde; oculta; copiar |
| B4 | Enviar: archivos fuera de la UoW, captura, confirmar o borrar | `FormEntrySubmitService.cs`; `P/Modules/Forms/Controllers/BusinessFormEntryController.cs`, `FormEntryDurableAdapter.cs` y `FormEntryUploadSlotAttribute.cs`, el lugar de subida | `AssistantAttachmentService.cs`, `AssistantControllerBase.Upload` y `AssistantUploadSlotAttribute.cs`, sin el cupo de la IA | sin lugar no se lee el archivo; versión nueva; dado de baja; opción dada de baja; reintento con otra versión; otra respuesta = 422; 101 MiB; archivo falso; sin permiso dentro de la UoW, sin recibo, y repetir con el permiso de vuelta; los ítems, con el mínimo, el máximo y el error de un ítem en su lugar |
| B5 | «Dónde se completa»: el ajuste, publicar y el chat | suma a `FormDefinition.cs`, `FormVersion.cs` y sus configuraciones (`AppOnly`), `FormDraftEditor.cs`, `FormPublicationService.cs` y `FormVersionCreator.cs` [E14], y `WhatsAppStructuredFormChannel.cs` | esos mismos | el ajuste guardado y sellado; `AppOnlyForCustomers`; con WhatsApp, cada cosa que no puede mostrar frena con `whatsApp: true`; con «Solo en la app», sin Meta ni `Check`; sin número, en la app, con `NoConnectedNumber`, y uno solo para clientes frena; pasar de un lado al otro, y hacia WhatsApp sigue la anterior; volver a publicar con número; `ListSendableAsync` con las de la app marcadas; `NotPublishedForNumber` |
| B6 | Lo propio | suma a `FormRecordQueryService.cs`, `FormRecordFileService.cs` y las lecturas [E15 y E18]; las rutas `own-records` | esos mismos | lo ajeno = 404 en cada ruta; también lo de WhatsApp; sin `formrecords.read`; dado de baja: se ve lo suyo y no se carga; uno solo para clientes, en `/entry` con `canLoad: false`; `/entry` ordena por sus columnas (264) |
| B7 | El canal en lo que ya existe | suma a `FormRecordQueryFilters.cs`, `FormRecordColumnsBuilder.cs` y `FormRecordSheetBuilder.cs` [E15] | esos mismos | filtro, conteos, navegación y Excel; la lista ordena por «Dónde se completa» (264) |
| B8 | Contratos, permisos y ejemplos | `Contracts/FormEntryListHttpQuery.cs`, `FormEntryOptionsHttpQuery.cs` y `FormEntrySubmitHttpRequest.cs`; suma a los de la E14 y la E15, a `FormsContractContributor.cs` y a `Permissions.resx` y `.en.resx` | esos mismos | cada ruta con 401, 403, feature apagada = 404; `RouteIsolationMatrixTests`; ejemplos exportados, también con ítems |
| B9 | La ubicación | suma a `FormQuestionType.cs`, la captura, las lecturas y el Excel [E14 y E15], al participante de la baja, a `WhatsAppFlowSessions`, a `WhatsAppOrganizationInboundWriter.cs` [E11] y a `WhatsAppFormSendService.cs` | `FormRecordFile` [E18] | rangos y redondeo; obligatoria; fuera del bloque, de las condiciones y de las fórmulas; el pedido antes del Flow, «Seguir sin ubicación» y una sola captura; el pedido y la ubicación en los mensajes del chat, cifrados; vence con la sesión (`FlowSessionHours`); se vacía al cerrar; la baja de la cuenta |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos, llamadas y subida con avance | `F/src/modules/forms/api/entry.ts`, con la carga y lo propio; suma a `api/forms.ts` [E14], `api/records.ts` [E15], `F/src/modules/whatsapp/chats/api/chats.ts` [E14] y `F/src/shared/api/httpClient.ts`, que hoy no tiene avance | `httpClient.ts` | zod estricto; avance y corte; `sameOperation`; otra clave después de un 400 sin errores por pregunta |
| F2 | Entradas y lista | suma a la lista [E17] y a `FormRecordsPage.tsx` [E15] | esos mismos | «Cargar», primero en el ⋮ (261); quien solo carga, con uno solo para clientes apagado y su motivo, y sus tres columnas, que ordenan (263 y 264); dados de baja |
| F3 | La forma editable | suma a `F/src/modules/forms/components/records/RecordForm.tsx` y `lib/recordForm.ts` [E15] el modo para cargar, sin otra grilla ni otras zonas (266) | `RecordForm.tsx` [E15] | la misma forma que la lectura en cada ancho, con los controles editables; el Sí o No, elegir una y elegir varias en la pieza, en una columna, con «y N más» y el tope (262); «2 fotos · 1 archivo» y «0 de 1 respondidas» en la zona plegada (166); la zona plegada con errores se abre sola; lecciones 1 a 3 |
| F4 | Los controles y la lista de catálogo | `components/entry/EntryField.tsx`, con la pieza `Picker` y sus opciones del servidor, sin otra lista (262) | `QuestionEditor.tsx` [E14] | búsqueda adentro, «Mostrar 50 más», «Listo (N)» y la lista dependiente que espera con «Elegí primero «Zona»»; el número, con `parseDecimalText` [E12]: «1.500» marca `numberAmbiguous` en su pregunta, y «1,5» llega con punto y todos sus dígitos (207 y T9); axe |
| F5 | Lo condicional y los cálculos | suma a `RecordForm.tsx`, con `lib/conditions.ts` y `lib/formula.ts` [E19] | los mismos | lo oculto no se manda; una zona sin preguntas visibles; el cálculo en gris, que da lo mismo que el servidor con los ejemplos del redondeo al par |
| F6 | Los archivos | `components/entry/EntryFiles.tsx` | el tablero | límites antes de enviar; cámara; barra sin palabras |
| F7 | El bloque | suma a `components/records/RecordRowsTable.tsx` y `RecordRowsCards.tsx` [E19] el modo para cargar (↑ ↓, 🗑, «Agregar ítem» y los errores), sin otra tabla (266), en una caja que se desplaza de costado si no entra | el tablero | la tabla en la página y los recuadros en el panel y el teléfono, con ↑ ↓ y 🗑 en la banda (261); el cálculo del ítem, una columna en gris; agregar, mover y quitar, con su foco; con el máximo y el mínimo; el error de cada ítem; «Mantener» y una versión nueva conservan los ítems; axe |
| F8 | La página, el panel y «Enviada» | `pages/LoadResponsePage.tsx`, `components/entry/EntryPanel.tsx`, `EntrySent.tsx`; `registration.ts` | `useEditForm` y `useUnsavedChangesGuard` | salir; versión nueva sin perder nada; mantener en la página y en el panel, con otra clave en cada envío; el panel con zonas; «Volver a Formularios»; lo cargado fuera del storage |
| F9 | «Dónde se completa», publicar, editor, lo propio y el chat | suma al editor y a `PublishDialog.tsx` [E14 y E16], `CatalogQuestionFields.tsx` [E16], `ConditionsEditor.tsx` [E19], `FormsPage.tsx`, `FormRecordsPage.tsx` y la lista de «Enviar formulario» [E14] | esos mismos | el ajuste, apagado en uno solo para clientes; «Pasar a «Solo en la app»» en «Problemas» y bajo una condición; «Estado» y «Dónde se completa» en la lista, que ordenan (264); «Según otra respuesta», frenado en la pantalla sin su pregunta (269); la (i) del máximo con la app; una dirección ajena; «Se completa solo en la app.», apagado en la pieza con su motivo; el aviso sin número y el freno de uno solo para clientes; el pedido de ubicación y la ubicación en el chat |
| F10 | La ubicación | `components/entry/EntryLocation.tsx`; suma al editor, a `RecordValues.tsx`, `RecordActivity.tsx` y a la planilla [E15] | `EntryFiles.tsx` | los estados de «Usar mi ubicación»; más de 100 m; sin permiso, sin señal y sin la función; la ✕; el enlace al mapa en la planilla; en el detalle, su control con la precisión y «Ver en el mapa» (266) y los renglones del pedido en la actividad (267); apagado en el bloque; axe |

**Al cerrar:** `docs/features/formularios.md` suma la sección, y nace `docs/guides/probar-en-el-celular.md` con lo que dio la prueba del teléfono.

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Los renglones de la grilla se arman con las columnas en uso.** Se armaban siempre de a tres, y en el teléfono dos preguntas quedaban sin línea entre ellas.
2. **Las columnas salen del ancho de la zona, no del de la pantalla.** Se mide con `ResizeObserver`.
3. **En el panel, número y fecha siguen angostos.**
4. **Las migas también miden su contenedor.**
5. **Cada envío es otra respuesta.** Allá, con «Mantener las respuestas» y sin archivos, el segundo envío repetía la clave del primero y recibía su número. Después de un envío que llegó, el front arma otra clave; solo un reintento después de un corte repite la anterior.
6. **Un dato nuevo se omite cuando no aplica** (traspaso 7): la pregunta de la que depende una lista no viaja en null, así las versiones selladas, sus huellas y los cuerpos que ya lee el front no cambian.
7. **Lo propio lo acota el servidor, también en los conteos:** el dueño va en el pedido interno, nunca desde el cliente.
8. **Cada test arma sus datos:** allá, con datos compartidos, un test perdía su fila en la purga de borradores y a otro se le colaba una foto sembrada.
9. **Una respuesta que el front no sabe leer rompe la pantalla entera.** Allá, un valor del bloque que el esquema no conocía dejaba sin planilla al formulario: el bloque entra al contrato de la carga y de «Enviada», con su ejemplo, a la vez en el back y en el front.
10. **Un rechazo pasajero dejaba recibo** (F8 de allá): un permiso perdido en carrera guardaba el 403, y repetir lo devolvía aunque el permiso volviera. No deja recibo, y repetir se decide de nuevo.
