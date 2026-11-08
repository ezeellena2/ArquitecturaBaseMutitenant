# E20 · Cargar una respuesta desde la web · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/20-cargar-desde-la-web.md` y su parte `20-parte-contrato-datos-y-pasos.md`. Tableros aprobados en el lienzo v169, con el recorrido guiado `cargar-una-respuesta`. No hay decisión de producto pendiente: la 200 cerró la que había.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B8 | Pendiente |
| Front | F1–F9 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido, como la base: contrato primero; después Back y Front a la vez, un chat por repo; una sola puerta al final. K0 confirma lo que dejaron la E14 a la E19.

TDD donde hay lógica. Una sola revisión de código, en la puerta. Todo en `main`, sin ramas ni push; las demás reglas van en el texto de cada chat, y la tabla de estado la actualiza la Guía.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [E14], [E15], [E16] y [E19] | Todavía no existen. | Lista cerrada en K0. |
| [E18] `IFileContentInspector` y el diario | `IFileStore`, `IFileStoreMaintenance` (el diario) y `LocalFileStore` están; el inspector, con la E18. | K0. |
| Allá: la etapa 2 de renglones | `a133cc33` y su front: tabla y recuadros como la spec; el error de un ítem en `answers.<bloque>.<n>.<pregunta>`; cuatro textos que el tablero no fija: «Al menos 1 ítem.» (el tacho con el mínimo), «Subir el ítem 2»… (los botones), «Ítem 2 · Cantidad» (cada celda) y «Ahora es el ítem 1 de 3.» (al mover). | F7 toma los cuatro textos; el error va en `questions.<bloque>.<n>.<pregunta>`; LD3-A no entra (191). |
| Front | `useIdempotentMutation` (con `sameOperation`), `useUnsavedChangesGuard` y `useEditForm` están. El panel al costado lo crea la E15 (`SidePanel.tsx`). `httpClient.ts` sube con `fetch`, sin avance. | K0; F1 suma el avance. |
| Celular | Vite escucha en `127.0.0.1:5174`, admite `.localtest.me` y no cambia el `Host`. Allá está `docs/guides/probar-en-el-celular.md`. | Va en la puerta (I2), con el reenvío a `127.0.0.1:5174`. |

## Contrato

**Rutas nuevas,** en `/api/organization/forms`, con `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `[HasPermission]` con `formrecords.create`, aparte de las de `forms.read` y `formrecords.read`. La organización y quien carga salen de la sesión. Cuotas generales; ninguna lleva `version`.

| Método y ruta | Pide | Devuelve | Errores |
| --- | --- | --- | --- |
| GET `/entry` | `page`, `pageSize`, `search`, `status` (`active`, `deactivated`) | `PagedResult<{id, name, deactivatedAtUtc?, ownCount, lastOwn?{id, displayNumber, submittedAtUtc}}>` | 400 |
| GET `/{id}/entry` | — | el de la spec, con las preguntas de cálculo para «Enviada» | `Forms.Definition.NotFound` y `NotPublished`; `Forms.Entry.Deactivated` y `NotForEmployees` |
| GET `/{id}/entry/questions/{fieldId}/options` | `page`, `pageSize` (50), `search`, `dependsOn?` | `PagedResult<{id, text, detail?}>` | `Forms.Entry.QuestionNotFound`; `Forms.Offer.RecipientDataMissing` y `FilterUnavailable` |
| POST `/{id}/records` | multipart: `versionId`, `answers` y los archivos, con `[Idempotent(Mode = IdempotencyMode.DurableReference)]`, `[IdempotencyDurable(typeof(FormEntryDurableAdapter))]` y `[RequestSizeLimit]` de 101 MiB | 201 `{recordId, displayNumber, submittedAtUtc, calculations[{fieldId, value}]}`, con Location a `own-records/{recordId}` | 404 `Forms.Definition.NotFound` si se borró; `Forms.Entry.Deactivated`, `VersionChanged`, `NotForEmployees`, `AnswersInvalid` y `TooLarge`; `Forms.Record.NotAllowed`; 422 `Request.IdempotencyKeyReused` |
| GET `/{id}/own-records/columns`; POST `…/search`, `…/facets` y `…/facet-options`; GET `…/{recordId}`; POST `…/{recordId}/navigation`; GET `…/{recordId}/files/{fileId}/{thumbnail\|content}` | los de la E15 y la E18 | lo mismo, solo con lo de esa persona | lo ajeno es 404, con `Forms.Record.NotFound` o `FileNotFound` |

`answers` lleva los valores canónicos de la E13 (número como texto con punto, fecha ISO, sí/no booleano y la opción por su id); el bloque, `{fieldId, rows: [[{fieldId, value}, …], …]}`, con `[]` en un ítem vacío; cada archivo, una parte `file.<fieldId>`. Un cálculo tiene la forma de la E19, con `null` si quedó «Sin respuesta».

**Rutas que cambian:**

| Ruta | Permiso | Qué suma |
| --- | --- | --- |
| GET `/` y GET `/overview` | `forms.read` y `formrecords.read` | `canLoad` y `appOnly`, booleanos, en cada fila |
| GET `/{id}` y PUT `/{id}/draft` | `forms.read` y `forms.manage` | `activeVersion.appOnly`; `recipientFilters[].mode` `answer`, con `sourceFieldId`, omitido si no aplica; `Forms.Draft.DependentListInvalid` y `DependentListForCustomers` |
| GET `/{id}/lifecycle` | `forms.read` | `appOnly` y `loadsInApp` (para empleados o para los dos), de la versión activa |
| POST `/{id}/publication-checks` | `forms.publish` | `warnings[]` de la E16 vuelve opcional `fieldId` y suma `reason`, `path` y `count`. `AppOnly` lleva `reason`, `fieldId?`, `path?`, `count?` y `limit?`, con los motivos `NoNumber` (sin `fieldId`), `DependentList`, `LargeCatalog` (también por un filtro fijo), `RowsMinimum` y los topes del canal que frena `Check` (200): `ConditionNotSupported`, `TooManyComponents`, `ResponseTooLarge`, `TextTooLong` y `UnsupportedConfiguration`. Para empleados o para los dos pasan a `AppOnly` esos frenos, `ChannelUnavailable` y `CatalogTooLarge`; un tope de la app, como `Forms.Draft.TooManyFiles` o `TooManyQuestions`, sigue frenando |
| POST `/{id}/publications`, `[Idempotent]` | `forms.publish` | 200 `{publicationId, state: "Published"}` si queda solo para la app; si no, 202. `NothingToPublish` no aplica si la activa es solo para la app y ahora se puede mandar |
| GET `/api/organization/whatsapp/{customers\|internal}/conversations/{id}/forms` | «Atender internos» o «Atender clientes» | `appOnly{reason, fieldId?, label?, path?, count?, limit?}`, sin versión, preguntas ni `optionCounts`; el que solo tiene `NoNumber`, con `NotPublishedForNumber` (103) |
| POST `/{id}/records/search`, `facets`, `facet-options`, `dashboard` y `/{recordId}/navigation` | `formrecords.read` | `filtros` suma `channel?` (`whatsapp` o `web`), y `dimensions[]`, `channel` |
| GET `/{id}/records/{recordId}` | `formrecords.read` | `enteredBy?{name, atUtc}` |
| POST `/{id}/records/export` | `formrecords.export` | «App» en «Canal» |

El front arma los motivos (`FRM-Publicar-20` y `WA-Chats-Formulario-20`) con esos datos; uno de la 200 dice el problema como la E14 (T26) y suma «Se carga desde la app.», y en el chat, una condición dice ««Descuento» tiene una condición que WhatsApp no puede mostrar. Se carga desde la app.».

**Textos de error,** en `FormsTexts.resx` y `.en.resx`:

| Código | Tipo | es | en |
| --- | --- | --- | --- |
| `Forms.Entry.VersionChanged` | Conflict | {0} tiene una versión nueva. Pasá a ella para enviar: lo que cargaste se conserva. | {0} has a new version. Switch to it to send: what you entered is kept. |
| `Forms.Entry.Deactivated` | Conflict | {0} se dio de baja y ya no se puede cargar. | {0} was deactivated and can no longer be filled in. |
| `Forms.Entry.NotForEmployees` | Conflict | {0} es solo para clientes: se responde por WhatsApp. | {0} is for customers only: it's answered on WhatsApp. |
| `Forms.Entry.QuestionNotFound` | NotFound | Esa pregunta no es de este formulario. | That question isn't part of this form. |
| `Forms.Entry.AnswersInvalid` | Validation (`questions.<id>`; en un ítem, `questions.<bloque>.<n>.<pregunta>`) | Revisá las respuestas marcadas. | Check the highlighted answers. |
| `Forms.Entry.TooLarge` | PayloadTooLarge | Lo que mandás pasa de 100 MiB. Sacá algún archivo y volvé a enviar. | What you're sending is over 100 MiB. Remove a file and send it again. |
| `Forms.Draft.DependentListInvalid` | Validation (`questions[i].recipientFilters[j]`) | Tiene que ser una pregunta anterior que elija de «{0}». | It has to be an earlier question that picks from «{0}». |
| `Forms.Draft.DependentListForCustomers` | Validation (`questions[i]`) | Una lista dependiente no se puede usar en un formulario solo para clientes. | A dependent list can't be used in a form for customers only. |

Cada error de `AnswersInvalid` lleva el código de la captura en su pregunta, también `Forms.Record.OptionWithdrawn`, `FileRejected` y `RowsInvalid`, y `Forms.Offer.RecipientDataMissing`, con el nombre del dato.

**Datos:** la migración `<fecha>_FormEntry.cs` (B5). `FormVersions` suma `AppOnly` (bool) y `AppOnlyReasons` (jsonb, `[]` por defecto), sellados al publicar: `[{reason, fieldId?, path?, count?, limit?}]` en el orden de las preguntas, así el chat dice el motivo de la primera. El enum de `FormRecords.Channel`, guardado como texto, suma `web`, y `SourceKey` es `web:<clave>`, con el mismo índice único.

### K0 · Lo que dejó la etapa anterior

El chat del contrato confirma cada nombre en el código; lo que difiera va a «Cambios al contrato» antes de programar.
- **E14:** `FormRecordService.CaptureAsync` y `FormRecordWriter`; `FormPublicationService`, con `FormPublicationWorks`, y `FormVersionCreator`; `IStructuredFormChannel.HasConnectedNumberAsync` y `Check`; `IStructuredFormSource.ListSendableAsync`, `StructuredFormChoice` y `WhatsAppFormSendService`; `FormStructuredSource` y `WhatsAppStructuredFormChannel`; `FormRecords` (`Channel`, `SourceKey` y los `Respondent*`); `Forms.Publication.ChannelUnavailable`, `ChannelIssues` y `NothingToPublish`, `Forms.Draft.CatalogTooLarge`, `WhatsApp.Flow.TextTooLong`, `TooManyComponents`, `ResponseTooLarge` y `UnsupportedConfiguration`, y `Forms.Record.NotAllowed` y `OptionWithdrawn`; en el front, `FormsPage.tsx`, `PublishDialog.tsx`, `PublicationBand.tsx`, `QuestionEditor.tsx` y `SendFormDialog.tsx`.
- **E15:** `FormRecordQueryService`, `FormRecordReader`, `FormRecordQueryFilters`, `FormRecordColumnsBuilder`, `FormRecordSheetBuilder`, `BusinessFormRecordsController`, `FormRecordFileResults`, `FormReader` con `GET /overview`, y `filtros`; en el front, las rutas `/org/formularios/:id/respuestas` y `…/completa`, `FormRecordsPage.tsx`, `RecordFiltersMenu.tsx`, `lib/recordFilters.ts`, `SidePanel.tsx`, `QuickView.tsx`, `RecordFullPage.tsx` y `RecordValues.tsx`.
- **E16:** `FormOfferBuilder`, con los filtros en SQL; `FormRecipientFilterPolicy` y `recipientFilters[].mode`, con el freno de un filtro fijo que deja más del tope; `warnings[]` (`{fieldId, code, …}`); `Forms.Offer.RecipientDataMissing` y `FilterUnavailable`; la zona «Avisos» y `CatalogQuestionFields.tsx`, con «Agregar filtro ▾».
- **E17:** `FormAvailabilityPolicy`; `status=deactivated`; `lifecycle`; la reactivación en `CompleteAsync` y `ResolveAsync`, con `IAuditLog`; `Forms.Definition.NotPublished`; `FormDefinitionCopyCreator` y `LifecycleDialogs.tsx`.
- **E18:** `IFileContentInspector`, con `CreateThumbnailAsync`; `IFileStore` con la categoría `forms`; `FormRecordFile` y `FormRecordFileService`; `FormVersions.MaxFileBytes`; `Forms.Record.FileRejected` y `FileNotFound`; en el front, `PhotoViewer.tsx`.
- **E19:** `FormConditionEvaluator`, `FormLogicPolicy` y `FormRecordLogicPolicy`; `StructuredRecordCapture.Rows` y `FormRecordRowValues`; `rowsOf` y el valor `rows`; `ConditionNotSupported`, `RowsMinimumOverLimit` y `RowsOverLimit`; el perfil `flow-static-7.3-r11`, el último; en el front, `lib/conditions.ts` y `RowsBlockEditor.tsx`.

### K1 · Publicar el contrato

El chat del contrato publica las rutas nuevas (con `501`) y las que cambian, genera `docs/contracts/openapi.json` y exporta ejemplos reales de cada cuerpo nuevo, también el 400 de un ítem y el chat con un formulario apagado. El front corre `npm run contracts`.

**Texto para el chat del Contrato**

```text
Leé AGENTS.md, este plan y la spec de la E20 con su parte. K0: confirmá cada nombre de la lista en el código y anotá lo que difiera en «Cambios al contrato». K1: publicá el contrato de esta sección, generá openapi.json, exportá los ejemplos reales y avisá a la Guía. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo (git commit -- <rutas>, en español, conventional commits); con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtro por módulo, más los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto.
```

## Back

Cada tarea copia su molde, con `Result` y ProblemDetails, `OperationLog.RunAsync`, `[LoggerMessage]` (nunca valores, nombres de archivos ni la clave), `TimeProvider`, textos es/en y una UoW por escritura. Quien carga sale de la sesión, nunca del pedido. Forms y WhatsApp se hablan solo por los puertos de `StructuredData`.

1. **B1 · Una sola captura.** Suma a `FormRecordService.cs` y `FormRecordWriter.cs` [E14], con `FormRecordLogicPolicy.cs` [E19]: lo común recibe la versión, quien responde, el canal, la clave, los valores, los ítems y los archivos, venga de WhatsApp o de la web. Copia esos mismos. Tests: los de WhatsApp de la E14 a la E19, en verde; la numeración correlativa entre canales; `SourceKey` `web:<clave>` único; las empresas de quien carga (9); `SubmittedAtUtc` del servidor.
2. **B2 · Leer para cargar.** `A/Modules/Forms/Interfaces/Services/IFormEntryService.cs`, `A/Modules/Forms/Services/FormEntryService.cs` y `FormEntryOptionsResolver.cs`; suma a `FormOfferBuilder.cs` [E16]: de a 50, con los filtros en SQL antes de paginar y sin el tope de 200. Copia `FormStructuredSource.cs` [E14]. Tests: los de B2 de la spec, con EXPLAIN en las 10.000 opciones; sin versión publicada, `NotPublished`.
3. **B3 · Listas dependientes.** Suma a `FormRecipientFilterPolicy.cs` [E16], `FormDraftEditor.cs` [E14] y `FormLogicPolicy.cs` [E19]: una lista cuya pregunta de origen quedó oculta también se oculta, en memoria y en `HiddenFieldIds`. Copia esos mismos. Tests: cada motivo, también una pregunta del bloque; una opción que no corresponde a la respuesta; la de origen oculta; copiar.
4. **B4 · Enviar.** `A/Modules/Forms/Interfaces/Services/IFormEntrySubmitService.cs`, `A/Modules/Forms/Services/FormEntrySubmitService.cs`, `P/Modules/Forms/Controllers/BusinessFormEntryController.cs` y `P/Modules/Forms/FormEntryDurableAdapter.cs`. Copia `AssistantAttachmentService.cs`, `AssistantControllerBase.Upload` y `AssistantDurableAdapter.cs`.
   - Afuera de la UoW: validar el pedido; revisar cada archivo con `IFileContentInspector`, armar la miniatura de una foto, como la E18, y guardar con `IFileStore`, anotado en el diario. La captura común los guarda en su UoW y confirma el diario después del commit; si falla, se borran.
   - Orden: borrado, 404 `Forms.Definition.NotFound`; dado de baja, `Deactivated`; otra versión activa, `VersionChanged`; no es para empleados, `NotForEmployees`.
   - El reintento: `FormEntryDurableAdapter` busca la respuesta por `SourceKey` y revalida la membresía, el permiso y que sea de quien pide.
   - Más de 101 MiB es 413 `Forms.Entry.TooLarge`, atrapado como en `WhatsAppWebhookFilter`.

   Tests: los de B4 de la spec, más el borrado, la miniatura y el reintento con el permiso perdido, que no devuelve nada.
5. **B5 · Publicar solo para la app, y el chat.** Suma a `FormPublicationService.cs`, `FormVersionCreator.cs`, `FormStructuredSource.cs` y `WhatsAppFormSendService.cs` [E14], y a `WhatsAppStructuredFormChannel.cs`; la migración `<fecha>_FormEntry.cs`. Copia esos mismos. `Check`, que es puro, corre siempre, con número o sin él (200): en uno para empleados o para los dos, cada tope del canal es un motivo de `AppOnly`, con su código como `reason`; en uno solo para clientes sigue frenando con `ChannelIssues`. Publicar solo para la app crea la `FormPublication` ya `Published`, sin `FormPublicationWorks` ni Flow. En esa misma UoW, sobre uno dado de baja, limpia la baja y registra la reactivación con `IAuditLog`, como la E17. Tests: los de B5 de la spec; cada tope del canal, con número y sin él; un tope de la app que frena; el 200 y su reintento; reactivar sin número; los de la E14, la E16 y la E19 que esperaban esos frenos pasan a `AppOnly` para empleados; la migración de ida y vuelta, con el `Down` que se niega si hay datos.
6. **B6 · Lo propio.** Suma a `FormRecordQueryFilters.cs`, `FormRecordQueryService.cs`, `FormRecordReader.cs` y `FormRecordFileService.cs` [E15 y E18], y `GET /entry`, con `FormAvailabilityPolicy` [E17], y las rutas `own-records` a `BusinessFormEntryController.cs`. Copia `BusinessFormRecordsController.cs` [E15]. «Solo lo mío» va en la función única de filtros, con quien pide; nunca viene del cliente. Tests: los de B6 de la spec, más los conteos y la navegación acotados, una copia con los mismos `FieldId` y `/entry` sin totales, con «Dados de baja» solo donde cargó algo (101).
7. **B7 · Lo que ya existe.** Suma a `FormRecordQueryFilters.cs`, `FormRecordColumnsBuilder.cs`, `FormRecordSheetBuilder.cs` y al detalle [E15]; `canLoad` y `appOnly` a `FormReader.cs`; `activeVersion.appOnly` a `GET /{id}` [E14], y `appOnly` y `loadsInApp` a `lifecycle` [E17]. Copia esos mismos. Tests: el canal en el filtro, los conteos, la navegación, el tablero y el Excel, con «App»; `enteredBy`; `canLoad` según el permiso, el estado y para quién es.
8. **B8 · Contratos, permisos y ejemplos.** Suma a `Contracts/*`, `FormsContractContributor.cs` y `Permissions.resx` y `.en.resx`: `formrecords.create` pasa a «Completar formularios por WhatsApp o desde la web.» («Fill in forms on WhatsApp or on the web.»), y `forms.publish` deja «en WhatsApp» (98): «Publicar un formulario, darlo de baja y reactivarlo.» («Publish a form, deactivate it and reactivate it.»). Tests: cada ruta con 401, 403 y feature apagada = 404; `RouteIsolationMatrixTests`, `ExplicitRouteInventoryTests`, `IdempotentActionsTests` y `PermissionTextsTests`; los ejemplos de K1, también con ítems.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan, la spec de la E20 con su parte y las fichas de su «Qué leer». Esperá K0 y K1. Hacé B1 a B8 en orden, copiando el molde de cada tarea; no toques el front ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo (git commit -- <rutas>, en español, conventional commits); con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtro por módulo, más los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato» y avisar a la Guía; ninguna clave real pasa por vos; al usuario, solo lo de producto.
```

## Front

Las pantallas copian los tableros v169, con i18n en es y en, tipos generados, `shared/format` y TanStack Query. El canal se llama «App» (90). Lo cargado vive en la página: nunca va a la URL, al storage ni a la caché. Tests con MSW, los ejemplos exportados, zod estricto y axe; cada lección de la spec lleva su test.

1. **F1 · Tipos, llamadas y subida con avance.** Suma a `F/src/modules/forms/api/` y a `F/src/shared/api/httpClient.ts`: una subida con `XMLHttpRequest` que informa el avance y conserva la sesión, los errores y el reloj del servidor. Copia `httpClient.ts`. Tests: zod estricto; el avance y el corte; con `sameOperation`, otra clave después de un envío que llegó y la misma después de un corte.
2. **F2 · Entradas, lista y lo propio.** Suma a `FormsPage.tsx` [E14 y E17], `FormRecordsPage.tsx`, `QuickView.tsx` y `RecordFullPage.tsx` [E15], `PhotoViewer.tsx` [E18] y `registration.ts`: con solo `formrecords.create` se entra a `/org/formularios` (con `/entry`) y a `/org/formularios/:id/respuestas` y `…/completa`, que leen `own-records/*`. Tableros `FRM-Formularios-20` y `FRM-Respuestas-Propias`, con sus teléfonos. Tests: los de F2 de la spec, con «Mis cargas», «Mi última carga» y «Todavía no cargaste»; «Solo en la app · versión 4»; lo propio, con «Empresa», sin «Cargó» ni «Exportar a Excel», y sin `forms.read`.
3. **F3 · El armado.** `F/src/modules/forms/components/entry/EntryGrid.tsx` y `EntryZone.tsx`. Tableros `FRM-Cargar-Respuesta` y `FRM-Cargar-Panel`. Tests: la tabla de los dos anchos; las columnas, medidas con `ResizeObserver` sobre la zona; las casillas en columnas; «3 de 5 respondidas», «2 fotos · 1 archivo» y «0 de 1 respondidas»; la zona plegada con errores se abre sola.
4. **F4 · Controles y lista de catálogo.** `components/entry/EntryField.tsx` y `EntryCatalogList.tsx`. Copia `QuestionEditor.tsx` [E14]. Tableros `FRM-Cargar-Lista` y su teléfono. Tests: la búsqueda en el servidor y «Mostrar 50 más»; una sola opción, ya elegida; «Te falta el dato «Sucursal».», con Enviar grisado; «Elegí primero «Zona»» y «Cambió «Zona»: volvé a elegir.».
5. **F5 · Lo condicional.** Suma a `EntryGrid.tsx`, con `lib/conditions.ts` [E19]. Tests: una pregunta aparece en su lugar; lo oculto no se manda; una zona sin preguntas visibles no se ve; la lista de una pregunta oculta, oculta; el cálculo no se ve.
6. **F6 · Los archivos.** `components/entry/EntryFiles.tsx`. Tableros `FRM-Cargar-Archivos` y su teléfono. Tests: tipo, tamaño y cantidad antes de enviar; «Tomar foto» primero y «Solo cámara» sin galería; la barra sin palabras.
7. **F7 · El bloque.** `F/src/shared/ui/EntryRowsTable.tsx` y `EntryRowsCards.tsx`, sobre `Table` de `table.tsx`, con su entrada en `docs/rules/pantallas-y-ui.md`; suma a `EntryGrid.tsx`. Tableros `FRM-Cargar-Respuesta`, su teléfono y `FRM-Cargar-Panel`. Tests: los de F7 de la spec, con «Ítem 2» en los recuadros, «Hasta 20 ítems.», «Al menos 1 ítem.» y los cuatro textos de allá.
8. **F8 · La página, el panel y «Enviada».** `pages/LoadResponsePage.tsx`, `components/entry/EntryPanel.tsx` y `EntrySent.tsx`; `registration.ts`, con `/org/formularios/:id/cargar`. Copia `useEditForm` y `useUnsavedChangesGuard`; el panel usa `SidePanel.tsx` [E15]. Tableros `FRM-Cargar-Respuesta`, `FRM-Cargar-Panel` y `FRM-Cargar-Enviada`, con sus teléfonos. Tests: los de F8 de la spec, más «Pantalla completa», «Enviando…» con su aro y la barra, y los estados de `FRM-Cargar-Enviada`.
9. **F9 · Publicar, el editor, la planilla y el chat.** Suma a `PublishDialog.tsx` y `CatalogQuestionFields.tsx` [E16], `PublicationBand.tsx` y `SendFormDialog.tsx` [E14], `LifecycleDialogs.tsx` [E17], `RowsBlockEditor.tsx` [E19], y `FormRecordsPage.tsx`, `RecordFiltersMenu.tsx`, `lib/recordFilters.ts`, `QuickView.tsx`, `RecordFullPage.tsx` y `RecordValues.tsx` [E15]. Tableros `FRM-Publicar-20`, `FRM-Editor-Listas`, `FRM-Respuestas-20`, `FRM-Respuesta-20` y `WA-Chats-Formulario-20`, con sus teléfonos. «Dar de baja» suma a «Qué deja de pasar» «No se carga más desde la app.» («It's no longer filled in from the app.») si la versión se carga desde la app, y en una solo para la app es la única línea, sin lo de WhatsApp (78). Tests: los de F9 de la spec; cada motivo de la 200, en «Avisos» y en el chat; «Problemas» en uno solo para clientes; la banda; el filtro «Canal»; «en la app, la cargó …», sin «Abrir conversación»; las líneas de «Dar de baja».

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-20-cargar-desde-la-web.md y la spec de la E20 con su parte. Esperá K1 y corré npm run contracts. Hacé F1 a F9 contra los tipos generados, copiando los tableros v169; no toques el lienzo, las specs, el back ni la tabla de estado del plan. Al cerrar la parte, npm run build, lint y contracts:check. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo (git commit -- <rutas>, en español, conventional commits); con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, npx tsc -b y npm test enteros, sin filtro por módulo, más los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato» y avisar a la Guía; ninguna clave real pasa por vos; al usuario, solo lo de producto.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real, con los tipos regenerados y los ejemplos estrictos.
2. **I2 · Puerta.** `/puerta`, una sola vez, sobre copias del corte:
   - las suites de los dos repos y `contracts:check`;
   - el E2E real, con los pasos 1 a 4 y 6 de «Cómo se cierra» de la spec, comparando con una respuesta del Meta simulado, y del paso 5, publicar y cargar sin número, también sobre uno dado de baja;
   - `realinear-docs --detectar` y `estado-docs --detectar` en 0;
   - la galería de `scripts/capturas/etapa-20.json`, a 1440, 390 y 320, con los tableros de la etapa;
   - `/recorrido`, con `cargar-una-respuesta`, y la revisión de código con la lista de control;
   - el paso 7 en un Android por USB (`chrome://inspect`, del 5174 a `127.0.0.1:5174`, y `https://localtest.me:5174`): una foto con la cámara y dos ítems en sus recuadros, con pasos clic por clic para el usuario.

   Al cerrar, `docs/features/formularios.md` suma la sección; nace `docs/guides/probar-en-el-celular.md`, con lo que dio el teléfono, y nace el `AGENTS.md` de `components/entry/` en el front.
3. **I3 · Pendiente para la I1 de la E10.** El resto del paso 5: conectar un número, volver a publicar y mandar el formulario por WhatsApp a un celular.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido / Celular / Revisión | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, este plan y la skill /puerta. Integrá contra la API real y devolvé a su dueño solo las diferencias reales. Corré la puerta una vez sobre copias del corte; con un arreglo, solo lo que toca. Para el celular, dale al usuario los pasos clic por clic; no manejes su PC. Escribí la documentación de «Al cerrar»; no toques el lienzo; Meta con número queda pendiente (I3); en el plan, solo la tabla de la puerta. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo (git commit -- <rutas>, en español, conventional commits); con la app levantada, nadie edita el src del front; TDD donde hay lógica; al terminar cada arreglo, build y tests rápidos enteros de su repo, sin Docker ni filtro por módulo, más los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto.
```

## Relación con otras etapas

**Usa de las anteriores:**
- **Base:** los botones que trabajan (43 y 115), `PageLoading`, `FormSkeleton` y la escala (180). No suma fuentes a la campana.
- **E14 a E19:** lo de K0.

**Deja para las siguientes:**
- **Rutas:** `GET /entry`, `GET /{id}/entry`, las opciones, `POST /{id}/records` y `own-records/*`.
- **Back:** `FormEntryService`, `FormEntryOptionsResolver`, `FormEntrySubmitService`, `BusinessFormEntryController`, con la carga y lo propio, y `FormEntryDurableAdapter`; «solo lo mío» en `FormRecordQueryFilters`.
- **Datos:** `FormVersions.AppOnly` y `AppOnlyReasons`; el canal `web` y `SourceKey` `web:<clave>`; `recipientFilters[].mode` `answer`.
- **Contrato:** `AppOnly`, con los motivos de la 200, y `appOnly`, `canLoad`, `loadsInApp`, `enteredBy`, el filtro `channel` y los errores `Forms.Entry.*`, `DependentListInvalid` y `DependentListForCustomers`.
- **Front:** `pages/LoadResponsePage.tsx`, `components/entry/*`, `shared/ui/EntryRowsTable.tsx` y `EntryRowsCards.tsx`, la subida con avance de `httpClient.ts`, y «No se carga más desde la app.» en `LifecycleDialogs.tsx`.
- **Documentos:** `docs/guides/probar-en-el-celular.md`.
- **Para quién:** la E21 corrige con el multipart, `FormEntrySubmitService`, `FormEntryDurableAdapter` y `sameOperation`, y suma a `own-records`; la E21, la E23 y la E25 conservan la línea de «Dar de baja»; la E22 suma los motivos de `AppOnly` a los que frenan publicar una plantilla; la E23 deja afuera las versiones `AppOnly`; la E25 pasa al formulario desde `LoadResponsePage.tsx`.

## Cambios al contrato


## Para decidir

Nada: lo resolvió la decisión 200.
