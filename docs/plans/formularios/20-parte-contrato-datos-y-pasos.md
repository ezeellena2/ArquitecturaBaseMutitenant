# E20 · Cargar una respuesta desde la web: armado, contrato, datos y pasos (spec, parte)

**Estado:** pendiente · parte de la [spec de la E20](20-cargar-desde-la-web.md), que dice qué leer y cómo se cierra.

## Cómo se arma cada pregunta (86, 87 y 141)

El autor decide el contenido. La app decide la forma, con una tabla fija por tipo:

| Ocupa | Tipos de pregunta |
| --- | --- |
| Una columna | Número, fecha, sí o no, texto corto de hasta 80, elegir una, en fila o en lista, y elegir de un catálogo, una o varias |
| El renglón entero | Texto largo, texto corto de más de 80 o sin largo máximo, elegir varias con opciones propias, foto, documento y el bloque |

- **Columnas:** tres en la compu, dos en la tableta y una en el teléfono y en el panel. Salen del ancho de la zona: tres desde 46 rem, dos desde 30 rem y una por debajo.
- **Orden:** las preguntas no se reordenan, salvo las fotos y los documentos, que van al final. Una que no entra baja de renglón, con una línea entre cada renglón visible.
- **Zonas** (88): cada título de sección arma una zona, y lo que va antes del primero queda en «Datos». Las fotos y los documentos van al final, en «Fotos y documentos». Todas se pliegan y arrancan abiertas, con el título en su banda. **Plegada, la banda dice cuántas se respondieron,** «3 de 5 respondidas», y la de los archivos, lo que se adjuntó, «2 fotos · 1 archivo», o «0 de 1 respondidas» sin archivos (166). El bloque cuenta como una pregunta, respondida si algún ítem tiene una respuesta. Al enviar con errores, la zona plegada que los tiene se abre sola.
- **Controles:** número y fecha son angostos; sí o no arranca sin nada marcado; elegir una con hasta cinco opciones las muestra en fila, aunque sean largas, y con más, una lista; elegir varias reparte sus casillas de arriba abajo en columnas, tres en la compu, dos en la tableta y en el panel y una en el teléfono, con «hasta N» en la etiqueta; el texto largo tiene de 2 a 6 renglones.
- **La unidad** va en la etiqueta que escribe el autor (86).
- **Lo condicional:** una pregunta aparece en su lugar y los renglones se recalculan. Lo oculto no se manda, y una zona sin preguntas visibles no se ve. **Un cálculo** no se ve al cargar, y aparece en «Enviada» (54 y 97).

## Contrato

Son rutas de `/api/organization/forms`, con `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `formrecords.create`. Son rutas aparte de las de `forms.read` y `formrecords.read`, porque `[HasPermission]` pide un solo permiso. La organización y quien carga salen de la sesión.

| Ruta | Pide | Devuelve |
| --- | --- | --- |
| `GET /entry` | `page`, `pageSize`, `search`, `status` (`active`, `deactivated`) | `PagedResult<{id, name, deactivatedAtUtc?, ownCount, lastOwn?: {number, receivedAtUtc}}>`, con lo suyo y sin los totales de la organización (153): lo que puede cargar, con el filtro de disponibilidad, o los dados de baja en los que cargó algo (101) |
| `GET /{id}/entry` | — | `{versionId, versionNumber, name, submitLabel, maxFileBytes, questions[], titles[]}`. Cada pregunta de catálogo trae `options?{total, single?, missingData?}`, y una lista dependiente, de qué pregunta depende. El bloque trae `rows{minRows, maxRows}` y sus preguntas, `rowsOf`, como en la E19 |
| `GET /{id}/entry/questions/{fieldId}/options` | `page`, `pageSize` (50), `search`, `dependsOn?`; sirve también para una pregunta de catálogo del bloque | `PagedResult<{id, text, detail?}>` |
| `POST /{id}/records` | multipart: `versionId`, `answers` y los archivos, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]`, `[IdempotencyDurable]` y `[RequestSizeLimit]` de 101 MiB. El bloque va en `answers` como `{fieldId, rows: [[{fieldId, value}, …], …]}`, un arreglo por ítem, en orden, y un ítem vacío es `[]` | 201 `{recordId, displayNumber, submittedAtUtc, calculations[]}` + Location |
| `GET /{id}/own-records/columns`, `POST …/search`, `POST …/facets`, `GET …/{recordId}`, `POST …/{recordId}/navigation` y `GET …/{recordId}/files/{fileId}/{thumbnail\|content}` | los de la E15 y la E18 | lo mismo, solo con lo de esa persona |

**Rutas que cambian:**

| Ruta | Qué suma |
| --- | --- |
| `GET /` y `GET /overview` | `canLoad` y `appOnly` |
| `GET /{id}` y `PUT /{id}/draft` | `recipientFilters[].mode` suma `answer`, con `sourceFieldId` |
| `POST /{id}/publication-checks` | `warnings[]` suma `AppOnly`, con `reason`: `NoNumber`, `DependentList`, `LargeCatalog` o `RowsMinimum`. `RowsMinimumOverLimit` (E19) frena solo en un formulario solo para clientes |
| `POST /{id}/publications` | 200 si queda solo para la app, porque termina en la misma UoW; si no, 202, como siempre |
| `GET …/conversations/{id}/forms` | los formularios solo para la app, con `appOnly{reason, fieldId?}`, el motivo que se muestra, y sin versión, preguntas ni `optionCounts`, para mostrarlos apagados (166). Uno que solo tiene `NoNumber` va con `NotPublishedForNumber` (103) |
| `POST …/records/facets` y `search` | la dimensión y el filtro `channel` |
| `GET …/records/{recordId}` | `enteredBy?{name, atUtc}` |

**Puerto:** «sin número» sale de `HasConnectedNumberAsync`, de la E14. `ListSendableAsync` trae las versiones solo para la app marcadas, con sus motivos.

**Errores:**

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Entry.VersionChanged` | Conflict | se publicó otra versión |
| `Forms.Entry.Unavailable` | Conflict | el formulario se dio de baja o no es para empleados |
| `Forms.Entry.QuestionNotFound` | NotFound | una pregunta que no es de la versión |
| `Forms.Entry.AnswersInvalid` | Validation (`questions.<id>`) | cada error en su pregunta, con los códigos de la captura. El de un ítem va en `questions.<bloque>.<n>.<pregunta>`, con `n` desde 0 |
| `Forms.Draft.DependentListInvalid` | Validation (`questions[i].recipientFilters[j]`) | la pregunta de la que depende no va antes, no es de elegir una, elige de otro catálogo o es del bloque, o la lista está en el bloque |
| `Forms.Draft.DependentListForCustomers` | Validation (`questions[i]`) | una lista dependiente en un formulario solo para clientes |

Valen `Forms.Record.OptionWithdrawn`, en su pregunta, y `Forms.Offer.RecipientDataMissing`, con el nombre del dato.

## Datos

Una migración aditiva, `<fecha>_FormEntry.cs`. Sin tablas nuevas.

| Tabla | Columna | Tipo | Regla |
| --- | --- | --- | --- |
| `FormVersions` | `AppOnly`, `AppOnlyReasons` | bool, texto[] | se sellan al publicar: los motivos de `AppOnly` que se dieron, sin repetir; vacío si se manda por WhatsApp |
| `FormRecords` | `Channel` | texto | admite `web` |

No hace falta una columna como `EnteredInApp`: lo dice el canal. Quien cargó es `RespondentUserId`. `SourceKey` es `web:<clave>`, con el mismo índice único.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E19]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Una sola captura: separar lo común de WhatsApp | suma a `FormRecordService.cs` y `FormRecordWriter.cs` [E14] | esos mismos | WhatsApp sigue igual; numeración correlativa entre los dos canales |
| B2 | Leer para cargar: versión, opciones y filtros en SQL | `A/Modules/Forms/Services/FormEntryService.cs`, `FormEntryOptionsResolver.cs`; suma a `FormOfferBuilder.cs` [E16] | `FormStructuredSource.cs` [E14] | una sola opción; sin el dato; 10.000 opciones con búsqueda; miembros y contactos; un catálogo del bloque, con sus filtros; otra organización |
| B3 | Listas dependientes | suma a `FormRecipientFilterPolicy.cs` [E16] y a `FormDraftEditor.cs` | esos mismos | cada motivo, también una pregunta del bloque; opción que no corresponde; oculta; copiar |
| B4 | Enviar: archivos fuera de la UoW, captura, confirmar o borrar | `FormEntrySubmitService.cs`; `P/Modules/Forms/Controllers/BusinessFormEntryController.cs`, `FormEntryDurableAdapter.cs` | `AssistantAttachmentService.cs`, `AssistantControllerBase.Upload` | versión nueva; dado de baja; opción dada de baja; reintento con otra versión; otra respuesta = 422; 101 MiB; archivo falso; sin permiso dentro de la UoW; los ítems, con el mínimo, el máximo y el error de un ítem en su lugar |
| B5 | Publicar solo para la app, y el chat | suma a `FormPublicationService.cs` [E14] y `WhatsAppStructuredFormChannel.cs` | esos mismos | los cuatro casos, también «elegir varias» de más de 20 y el mínimo de más de 10; solo clientes; sin Meta; volver a publicar con número; `ListSendableAsync` con las solo para la app marcadas; el motivo del chat con más de uno; una publicada sin número, al conectar uno, con `NotPublishedForNumber` |
| B6 | Lo propio | suma a `FormRecordQueryService.cs`, `FormRecordFileService.cs` y las lecturas [E15 y E18]; las rutas `own-records` | esos mismos | lo ajeno = 404 en cada ruta; también lo de WhatsApp; sin `formrecords.read`; dado de baja: se ve lo suyo y no se carga |
| B7 | El canal en lo que ya existe | suma a `FormRecordQueryFilters.cs`, `FormRecordColumnsBuilder.cs` y `FormRecordSheetBuilder.cs` [E15] | esos mismos | filtro, conteos, navegación y Excel |
| B8 | Contratos y ejemplos | suma a `Contracts/*` y `FormsContractContributor.cs` | esos mismos | cada ruta con 401, 403, feature apagada = 404; `RouteIsolationMatrixTests`; ejemplos exportados, también con ítems |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos, llamadas y subida con avance | suma a `F/src/modules/forms/api/` y `F/src/shared/api/httpClient.ts`, que hoy no tiene avance | `httpClient.ts` | zod estricto; avance y corte; `sameOperation` |
| F2 | Entradas y lista | suma a la lista [E17] y a `FormRecordsPage.tsx` [E15] | esos mismos | quien solo carga; grisado; dados de baja |
| F3 | El armado de una pregunta | `F/src/modules/forms/components/entry/EntryGrid.tsx`, `EntryZone.tsx` | el tablero | la tabla de los dos anchos y las casillas en columnas (141); «3 de 5 respondidas» en la zona plegada (166); `ResizeObserver`; lecciones 1 a 3 |
| F4 | Los controles y la lista de catálogo | `components/entry/EntryField.tsx`, `EntryCatalogList.tsx` | `QuestionEditor.tsx` [E14] | búsqueda, «Mostrar 50 más» y la lista dependiente que espera con «Elegí primero «Zona»»; axe |
| F5 | Lo condicional | suma a `EntryGrid.tsx`, con `lib/conditions.ts` [E19] | el mismo | lo oculto no se manda; una zona sin preguntas visibles |
| F6 | Los archivos | `components/entry/EntryFiles.tsx` | el tablero | límites antes de enviar; cámara; barra sin palabras |
| F7 | El bloque | `F/src/shared/ui/EntryRowsTable.tsx` y `EntryRowsCards.tsx`, dos piezas nuevas con su ficha, sobre `Table` de `table.tsx`, en una caja que se desplaza de costado si no entra; suma a `EntryGrid.tsx` | el tablero | la tabla en la página y los recuadros en el panel y el teléfono; agregar, mover y quitar, con su foco; con el máximo y el mínimo; el error de cada ítem; «Mantener» y una versión nueva conservan los ítems; axe |
| F8 | La página, el panel y «Enviada» | `pages/LoadResponsePage.tsx`, `components/entry/EntryPanel.tsx`, `EntrySent.tsx`; `registration.ts` | `useEditForm` y `useUnsavedChangesGuard` | salir; versión nueva sin perder nada; mantener en la página y en el panel, con otra clave en cada envío; el panel con zonas; «Volver a Formularios»; lo cargado fuera del storage |
| F9 | Publicar, editor, lo propio y el chat | suma a `PublishDialog.tsx` [E16], `CatalogQuestionFields.tsx` [E16], `FormRecordsPage.tsx` y la lista de «Enviar formulario» [E14] | esos mismos | los cuatro casos; «Según otra respuesta»; las dos (i) del bloque con la app; una dirección ajena; el formulario solo para la app, apagado con su motivo |

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
8. **Los tests con datos compartidos fallaban de a ratos:** allá, la purga de borradores de a 100 dejaba afuera la fila de un test, y una siembra con una foto sin fuente se colaba en otro. Cada test arma sus datos.
9. **Una respuesta que el front no sabe leer rompe la pantalla entera.** Allá, un valor del bloque que el esquema no conocía dejaba sin planilla al formulario: el bloque entra al contrato de la carga y de «Enviada», con su ejemplo, a la vez en el back y en el front.
