# E20 · Cargar una respuesta desde la web · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/20-cargar-desde-la-web.md` y su parte `20-parte-contrato-datos-y-pasos.md`. Tableros aprobados en el lienzo v175, con el recorrido guiado `cargar-una-respuesta`, salvo `FRM-Editor-Listas`, que se redibuja con «Dónde se completa» (229), y el aviso de una fecha en `FRM-Editor-20`, que se aprueban antes de F9, y lo que la tabla de la spec da como «Se redibujan» por las decisiones 261 a 264, 266, 267 y 269, que se aprueba antes de F2.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B9 | Pendiente |
| Front | F1–F10 | Pendiente |

## Forma de trabajo

Modo rápido ([etapa en paralelo](../guides/etapa-en-paralelo.md) y [ADR 0071](../decisions/0071-conductor-en-carriles.md)): contrato primero; después los carriles del back y el front a la vez; la revisión en paralelo, con los tests enteros y los detectores; los arreglos; la última revisión de código y el cierre. No hay puerta por etapa: el E2E real, la galería y el recorrido van en la verificación de la etapa (abajo), sobre una copia de su corte, mientras se programa la siguiente. Un arreglo corre solo lo que toca. La tabla de estado la actualiza el cierre del conductor ([conducir una etapa](../guides/conducir-una-etapa.md)). Las migraciones de la B5 y la B9 van en ese orden y en el mismo carril, cada una armada sobre el `HEAD` del momento y con `ModelSnapshotTests` en verde.

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md)): una vez, en la revisión de la etapa, un revisor por cada tema de riesgo de las tareas **(R)**.

**Reglas de cada chat:** todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con `git commit -- <rutas>`, en español y conventional, con la línea `Tarea: 20-cargar-desde-la-web/<id>`; con la app levantada, nadie edita el `src` del front; TDD donde hay lógica. Cada tarea corre su test dirigido y el build (en el front, `tsc -b` y el lint de lo que tocó), con `node scripts/arneses/verificar.mjs` en el back y `node scripts/verificar.mjs` en el front; los tests enteros, los de integración y los de seguridad corren una vez, en la revisión de la etapa. El contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [E18] `IFileContentInspector` y el diario | `IFileStore`, `IFileStoreMaintenance` (el diario) y `LocalFileStore` están; el inspector, con la E18. | K0. |
| Allá: «Sólo en la app», «Ubicación» y el cálculo al cargar | En `be88b2c9`: `FormDefinitions.AppOnly`, `FormQuestionType.Location`, `WhatsAppFlowLocationService` y `calculation` en la carga. | B5, B9, F5 y F10. |
| Allá: la etapa 2 de renglones, probada con Meta el 9/11 | `a133cc33` y su front: tabla y recuadros como la spec; el error de un ítem en `answers.<bloque>.<n>.<pregunta>`; cuatro textos que el tablero no fija: «Al menos 1 ítem.» (el tacho con el mínimo), «Subir el ítem 2»… (los botones), «Ítem 2 · Cantidad» (cada celda) y «Ahora es el ítem 1 de 3.» (al mover). | F7 toma los cuatro textos; LD3-A no entra (191). |
| Front | `useIdempotentMutation` (con `sameOperation`), `useUnsavedChangesGuard` y `useEditForm` están. El panel al costado lo crea la E15 (`SidePanel.tsx`). `httpClient.ts` sube con `fetch`, sin avance. | K0; F1 suma el avance. |

## Contrato

**Rutas nuevas,** en `/api/organization/forms`, con `[Access(Access.Business)]`, `[FeatureGate("forms")]` y `[HasPermission]` con `formrecords.create`, aparte de las de `forms.read` y `formrecords.read`. La organización y quien carga salen de la sesión, nunca del pedido. Cuotas generales; ninguna lleva `version`. Sin acciones del asistente: una respuesta no entra en sus tipos (spec, 0068).

| Método y ruta | Pide | Devuelve | Errores |
| --- | --- | --- | --- |
| GET `/entry` | `page`, `pageSize`, `search`, `status` (`active`, `deactivated`), `sort` (`name`, `ownCount`, `lastOwnNumber`, `lastOwnAtUtc`, 264) | `PagedResult<{id, name, deactivatedAtUtc?, canLoad, ownCount, lastOwn?{id, displayNumber, submittedAtUtc}}>`, también los activos solo para clientes, con `canLoad: false` | 400 |
| GET `/{id}/entry` | — | el de la parte de la spec, con `calculation` en cada cálculo y el tipo `location` | `Forms.Definition.NotFound` y `NotPublished`; `Forms.Entry.Deactivated` y `NotForEmployees` |
| GET `/{id}/entry/questions/{fieldId}/options` | `page`, `pageSize` (50), `search`, `dependsOn?` | `PagedResult<{id, text, detail?, fields?}>`, con los campos que usa una fórmula | `Forms.Entry.QuestionNotFound`; `Forms.Offer.RecipientDataMissing` y `FilterUnavailable` |
| POST `/{id}/records` | multipart: `versionId`, `answers` y los archivos, con `[FormEntryUploadSlot]`, `[Idempotent(Mode = IdempotencyMode.DurableReference)]`, `[IdempotencyDurable(typeof(FormEntryDurableAdapter))]` y `[RequestSizeLimit]` de 101 MiB | 201 `{recordId, displayNumber, submittedAtUtc, calculations[{fieldId, value}]}`, con Location a `own-records/{recordId}` | 404 `Forms.Definition.NotFound` si se borró; `Forms.Entry.Deactivated`, `VersionChanged`, `NotForEmployees`, `AnswersInvalid`, `TooLarge`, `UploadBusy`, `UploadQueueFull` y `UploadTimedOut`; `Forms.Record.NotAllowed`, sin recibo; 422 `Request.IdempotencyKeyReused` |
| GET `/{id}/own-records/columns`; POST `…/search`, `…/facets` y `…/facet-options`; GET `…/{recordId}`; POST `…/{recordId}/navigation`; GET `…/{recordId}/files/{fileId}/{thumbnail\|content}` | los de la E15 y la E18 | lo mismo, solo con lo de esa persona | lo ajeno es 404, con `Forms.Record.NotFound` o `FileNotFound` |

`answers` lleva los valores canónicos de la E13 (número como texto con punto, fecha ISO, sí/no booleano y la opción por su id), con el bloque, la ubicación y los cálculos como dice la parte; cada archivo, una parte `file.<fieldId>`. En la respuesta, `calculations[]` tiene la forma de la E19, con `null` si quedó «Sin respuesta».

**Rutas que cambian:**

| Ruta | Permiso | Qué suma |
| --- | --- | --- |
| GET `/` y GET `/overview` | `forms.read` y `formrecords.read` | `canLoad` y `appOnly`, booleanos, en cada fila: el de la versión activa o, sin versión, el del borrador; `overview` suma `whatsAppConnected`; `sort` suma `appOnly` (264) |
| GET `/{id}` y PUT `/{id}/draft` | `forms.read` y `forms.manage` | `draft.appOnly`, omitido si es false, y `whatsAppConnected`; `activeVersion.appOnly`; el tipo `location`; `recipientFilters[].mode` `answer`, con `sourceFieldId`, omitido si no aplica; `Forms.Draft.AppOnlyForCustomers`, `DependentListInvalid` y `DependentListForCustomers` |
| GET `/{id}/lifecycle` | `forms.read` | `appOnly` y `loadsInApp` (para empleados o para los dos), de la versión activa |
| POST `/{id}/publication-checks` | `forms.publish` | `appOnly`, dónde se publica. Con «En la app y por WhatsApp», `issues[]` suma `whatsApp: true` en lo que WhatsApp no puede mostrar: los frenos de `Check`, `RowsMinimumOverLimit`, `CatalogTooLarge` (también por un filtro fijo), `DependentListNotSupported` y `TooManyLocations`. Con «Solo en la app», sin ellos ni `ChannelUnavailable`. Sin número, sin ellos y con el aviso `NoConnectedNumber` en `warnings[]`, y uno solo para clientes frena con `ChannelUnavailable` (98), con los textos de la spec. Un tope de la app, como `TooManyFiles`, frena siempre |
| POST `/{id}/publications`, `[Idempotent]` | `forms.publish` | 200 `{publicationId, state: "Published"}` si se publica en la app; si no, 202. `NothingToPublish` no aplica si la activa es de la app y el borrador va por WhatsApp con un número |
| GET `/api/organization/whatsapp/{customers\|internal}/conversations/{id}/forms` | «Atender internos» o «Atender clientes» | `appOnly: true`, sin versión, preguntas ni `optionCounts`; la publicada sin número, con un número conectado, con `NotPublishedForNumber` (103) |
| Los mensajes de esas conversaciones | los mismos | el pedido de ubicación y la ubicación que llega, como dice la parte, cifrados (0065) |
| POST `/{id}/records/search`, `facets`, `facet-options`, `dashboard` y `/{recordId}/navigation` | `formrecords.read` | `filtros` suma `channel?` (`whatsapp` o `web`), y `dimensions[]`, `channel`; una ubicación admite solo `answered`, `unanswered` y `notAsked`, y `sort` la rechaza |
| GET `/{id}/records/{recordId}` | `formrecords.read` | `enteredBy?{name, atUtc}`; la ubicación como dato; los momentos `locationRequested`, `locationReceived` y `locationSkipped` |
| POST `/{id}/records/export` | `formrecords.export` | «App» en «Canal»; las tres columnas numéricas de una ubicación |

**Textos de error,** en `FormsTexts.resx` y `.en.resx`, con su tipo, en la [parte de errores](formularios/20-parte-errores-y-celular.md#errores-y-sus-textos); los códigos van en `D/Forms/FormEntryErrors.cs`, nuevo, y `FormDraftErrors.cs` y `FormRecordErrors.cs` [E14], y los de `WhatsApp.Flow`, en `D/WhatsApp/WhatsAppErrors.cs` y `WhatsAppTexts.resx`.

**Datos:** los de la parte, en `<fecha>_FormEntry.cs` (B5) y `<fecha>_WhatsAppFlowLocation.cs` (B9); el enum de `FormRecords.Channel`, guardado como texto, suma `web`.

### K0 · Lo que dejó la etapa anterior

La lista cerrada, que el chat del contrato confirma antes de programar:
- **E14:** `FormRecordService.CaptureAsync` y `FormRecordWriter`; `FormQuestionType`, `FormDraftEditor` y `FormsAccountDeletionParticipant`; `FormPublicationService`, con `FormPublicationWorks`, y `FormVersionCreator`; `IStructuredFormChannel.HasConnectedNumberAsync` y `Check`; `IStructuredFormSource.ListSendableAsync`, `StructuredFormChoice` y `WhatsAppFormSendService`; `FormStructuredSource` y `WhatsAppStructuredFormChannel`; `FormRecords` (`Channel`, `SourceKey` y los `Respondent*`); `FormDefinitions.Audience`, molde de `AppOnly`; `WhatsAppFlowSessions`; `Forms.Publication.ChannelUnavailable`, `ChannelIssues` y `NothingToPublish`, `Forms.Draft.CatalogTooLarge`, `WhatsApp.Flow.TextTooLong`, `TooManyComponents`, `ResponseTooLarge` y `UnsupportedConfiguration`, y `Forms.Record.NotAllowed` y `OptionWithdrawn`; en el front, `FormsPage.tsx`, `PublishDialog.tsx`, `PublicationBand.tsx`, `QuestionEditor.tsx` y `SendFormDialog.tsx`.
- **E15:** `FormRecordQueryService`, `FormRecordReader`, `FormRecordQueryFilters`, `FormRecordColumnsBuilder`, `FormRecordSheetBuilder`, `BusinessFormRecordsController`, `FormRecordFileResults`, `FormReader` con `GET /overview`, y `filtros`; en el front, las rutas `/org/formularios/:id/respuestas` y `…/completa`, `FormRecordsPage.tsx`, `RecordFiltersMenu.tsx`, `lib/recordFilters.ts`, `SidePanel.tsx`, `QuickView.tsx`, `RecordFullPage.tsx`, `RecordValues.tsx`, la forma de solo lectura (`RecordForm.tsx` y `lib/recordForm.ts`, 266) y `RecordActivity.tsx` (267).
- **E16:** `FormOfferBuilder`, con los filtros en SQL; `FormRecipientFilterPolicy` y `recipientFilters[].mode`, con el freno de un filtro fijo que deja más del tope; `warnings[]` (`{fieldId, code, …}`); `Forms.Offer.RecipientDataMissing` y `FilterUnavailable`; la zona «Avisos» y `CatalogQuestionFields.tsx`, con «Agregar filtro ▾».
- **E17:** `FormAvailabilityPolicy`, en memoria, y `FormAvailabilityQueryFilters`, privado del reader; `status=deactivated`; `lifecycle`; la reactivación en `CompleteAsync` y `ResolveAsync`, con `IAuditLog`; `Forms.Definition.NotPublished`; `FormDefinitionCopyCreator` y `LifecycleDialogs.tsx`.
- **E18:** `IFileContentInspector`, con `CreateThumbnailAsync`; `IFileStore` con la categoría `forms`; `FormRecordFile` y `FormRecordFileService`; `FormVersions.MaxFileBytes`; `Forms.Record.FileRejected` y `FileNotFound`; en el front, `PhotoViewer.tsx`.
- **Base 2:** `numberAmbiguous` (207), el conversor del chat cifrado (0065), la pieza `Picker`, con sus opciones del servidor (262), el orden por columnas (264) con `SortIndexTests`, y el lugar de subida (B12: `AssistantUploadSlotAttribute`, `IExclusiveSlots` y `AssistantUploadLimiter`).
- **E12:** `parseDecimalText` y `FormsPersonalDataPurgeTests`.
- **E19:** `FormConditionEvaluator`, `FormLogicPolicy` y `FormRecordLogicPolicy`; `FormFormulaEvaluator`, `lib/formula.ts` y `lib/decimal.ts`, con el redondeo al par (232); `StructuredRecordCapture.Rows` y `FormRecordRowValues`; `rowsOf` y el valor `rows`; `ConditionNotSupported`, `RowsMinimumOverLimit` y `RowsOverLimit`; el perfil `flow-static-7.3-r11`, el último; en el front, `lib/conditions.ts`, `ConditionsEditor.tsx`, `RowsBlockEditor.tsx` y `components/records/RecordRowsTable.tsx` y `RecordRowsCards.tsx`, de solo lectura.

### K1 · Publicar el contrato

El chat del contrato publica las rutas nuevas (con `501`) y las que cambian, genera `docs/contracts/openapi.json` y exporta ejemplos reales de cada cuerpo nuevo, también el 400 de un ítem y el chat con un formulario apagado. El front corre `npm run contracts`.

**Texto para el chat del Contrato**

```text
Leé AGENTS.md, este plan y la spec de la E20 con su parte. K0: confirmá cada nombre de la lista en el código y anotá lo que difiera en «Cambios al contrato». K1: publicá el contrato de esta sección, generá openapi.json, exportá los ejemplos reales y avisá a la Guía. Reglas: las de «Forma de trabajo» de este plan.
```

## Back

Cada tarea copia su molde, con `Result` y ProblemDetails, `OperationLog.RunAsync`, `[LoggerMessage]` (nunca valores, nombres de archivos ni la clave), `TimeProvider`, textos es/en y una UoW por escritura. Forms y WhatsApp se hablan solo por los puertos de `StructuredData`.

1. **B1 · Una sola captura (R: datos).** Suma a `FormRecordService.cs` y `FormRecordWriter.cs` [E14], con `FormRecordLogicPolicy.cs` [E19]: lo común recibe la versión, quien responde, el canal, la clave, los valores, los ítems y los archivos, de WhatsApp o de la web. Copia esos mismos. Las opciones elegidas se revisan por lote. Tests: los de WhatsApp de la E14 a la E19, en verde; la numeración correlativa entre canales; `SourceKey` `web:<clave>` único; las empresas de quien carga (9); `SubmittedAtUtc` del servidor.
2. **B2 · Leer para cargar (R: aislamiento, por la oferta filtrada).** `A/Modules/Forms/Interfaces/Services/IFormEntryService.cs`, `A/Modules/Forms/Services/FormEntryService.cs` y `FormEntryOptionsResolver.cs`; suma a `FormOfferBuilder.cs` [E16]: de a 50, con los filtros en SQL antes de paginar y sin el tope de 200. Copia `FormStructuredSource.cs` [E14]. Tests: los de B2 de la spec, con EXPLAIN en las 10.000 opciones; sin versión publicada, `NotPublished`.
3. **B3 · Listas dependientes.** Suma a `FormRecipientFilterPolicy.cs` [E16], `FormDraftEditor.cs` [E14] y `FormLogicPolicy.cs` [E19]: una lista cuya pregunta de origen quedó oculta también se oculta, en memoria y en `HiddenFieldIds`. Copia esos mismos. Tests: los de B3 de la spec.
4. **B4 · Enviar (R: archivos, permisos y cuotas por alcance).** `A/Modules/Forms/Interfaces/Services/IFormEntrySubmitService.cs`, `A/Modules/Forms/Services/FormEntrySubmitService.cs`, `P/Modules/Forms/Controllers/BusinessFormEntryController.cs`, `P/Modules/Forms/FormEntryDurableAdapter.cs` y `P/Modules/Forms/FormEntryUploadSlotAttribute.cs`. Copia `AssistantAttachmentService.cs`, `AssistantControllerBase.Upload` y `AssistantDurableAdapter.cs`.
   - **El lugar de subida** (0064 §4, como `FormCatalogFileSlotAttribute` de la E12): `FormEntryUploadSlotAttribute` copia `AssistantUploadSlotAttribute` (B12 de la base 2) sin el cupo de la IA, y corre después de la sesión y del permiso y antes de la idempotencia y de `[FromForm]`. Toma un lugar por persona entre copias con `IExclusiveSlots` (`forms-entry`; la segunda a la vez, `UploadBusy` enseguida) y uno de la copia con `FormEntryUploadLimiter`, que copia `AssistantUploadLimiter` y reparte su espera por organización (`UploadQueueFull`); el cuerpo tiene su plazo (`UploadTimedOut`). Los valores van en `Forms:Uploads:*`, validados al arrancar: son técnicos de cada copia (270). Sin lugar no se lee el archivo.
   - Afuera de la UoW: validar el pedido; revisar cada archivo con `IFileContentInspector`, armar la miniatura de una foto, como la E18, y guardar con `IFileStore`, anotado en el diario. La captura común los guarda en su UoW y confirma el diario después del commit; si falla, se borran.
   - Orden: borrado, 404 `Forms.Definition.NotFound`; dado de baja, `Deactivated`; otra versión activa, `VersionChanged`; no es para empleados, `NotForEmployees`.
   - El reintento: `FormEntryDurableAdapter` busca la respuesta por `SourceKey` y revalida la membresía, el permiso y que sea de quien pide. Un 403 por un permiso perdido en carrera no deja recibo (F8 de allá).
   - Más de 101 MiB es 413 `Forms.Entry.TooLarge`, atrapado como en `WhatsAppWebhookFilter`.

   Tests: los de B4 de la spec, más el borrado, la miniatura y el reintento con el permiso perdido; la segunda subida de la misma persona; una organización con sus lugares ocupados no frena a otra; el plazo del cuerpo.
5. **B5 · «Dónde se completa», publicar y el chat (R: Meta y migración).** Suma a `FormDefinition.cs` y `FormVersion.cs`, con sus configuraciones (`AppOnly`), `FormDraftEditor.cs`, `FormPublicationService.cs`, `FormVersionCreator.cs`, `FormStructuredSource.cs` y `WhatsAppFormSendService.cs` [E14], y a `WhatsAppStructuredFormChannel.cs`. Copia esos mismos y `Audience`. Con «Solo en la app» o sin número, crea la `FormPublication` ya `Published`, sin `FormPublicationWorks` ni Flow, y sobre uno dado de baja limpia la baja y registra la reactivación con `IAuditLog`, como la E17. Tests: los de B5 de la spec; un tope de la app que frena; el 200 y su reintento; reactivar sin número; los de la E14, la E16 y la E19 que esperaban esos frenos, con `whatsApp`; la migración de ida y vuelta, con el `Down` que se niega si hay datos.
6. **B6 · Lo propio (R: aislamiento y permisos).** Suma a `FormRecordQueryFilters.cs`, `FormRecordQueryService.cs`, `FormRecordReader.cs` y `FormRecordFileService.cs` [E15 y E18], y `GET /entry`, con el filtro de disponibilidad de la E17 (`FormAvailabilityQueryFilters` en el reader y `FormAvailabilityPolicy` en memoria), y las rutas `own-records` a `BusinessFormEntryController.cs`. Copia `BusinessFormRecordsController.cs` [E15]. «Solo lo mío» va en la función única de filtros, con quien pide; nunca viene del cliente. Los órdenes de `/entry` entran en `SortIndexTests` como proyecciones sin índice, como los de la lista de la E15. Tests: los de B6 de la spec, más los conteos y la navegación acotados, una copia con los mismos `FieldId` y `/entry` sin totales, con «Dados de baja» solo donde cargó algo.
7. **B7 · Lo que ya existe.** Suma a `FormRecordQueryFilters.cs`, `FormRecordColumnsBuilder.cs`, `FormRecordSheetBuilder.cs` y al detalle [E15]; `canLoad`, `appOnly`, `whatsAppConnected` y `sort=appOnly` (264) a `FormReader.cs`; `activeVersion.appOnly` a `GET /{id}` [E14], y `appOnly` y `loadsInApp` a `lifecycle` [E17]. Copia esos mismos. `appOnly` entra en `SortIndexTests` como proyección sin índice. Tests: el canal en el filtro, los conteos, la navegación, el tablero y el Excel, con «App»; `enteredBy`; `canLoad` según el permiso, el estado y para quién es.
8. **B8 · Contratos, permisos y ejemplos (R: permisos).** `P/Modules/Forms/Contracts/FormEntryListHttpQuery.cs`, `FormEntryOptionsHttpQuery.cs` y `FormEntrySubmitHttpRequest.cs`; suma a los contratos de la E14 y la E15, `FormsContractContributor.cs` y `Permissions.resx` y `.en.resx`: `formrecords.create` pasa a «Completar formularios por WhatsApp o desde la web.» («Fill in forms on WhatsApp or on the web.»), y `forms.publish` deja «en WhatsApp» (98): «Publicar un formulario, darlo de baja y reactivarlo.» («Publish a form, deactivate it and reactivate it.»). Tests: cada ruta con 401, 403 y feature apagada = 404; `RouteIsolationMatrixTests`, `ExplicitRouteInventoryTests`, `IdempotentActionsTests` y `PermissionTextsTests`; los ejemplos de K1, también con ítems y una ubicación.

9. **B9 · La ubicación (R: Meta, cifrado y datos).** Suma a `FormQuestionType.cs`, `FormDraftEditor.cs`, la captura y `FormRecordWriter.cs` [E14], las lecturas, `FormRecordColumnsBuilder.cs` y `FormRecordSheetBuilder.cs` [E15], `FormLogicPolicy.cs` [E19], `FormsAccountDeletionParticipant.cs` y su cleanup, y en WhatsApp, a `WhatsAppFormSendService.cs`, `WhatsAppFlowSessions` y `A/Modules/WhatsApp/Services/WhatsAppOrganizationInboundWriter.cs` [E11], con un `WhatsAppFlowLocationWriter.cs` si hace falta un helper, y su migración, `<fecha>_WhatsAppFlowLocation.cs`, con el `Down` que se niega si hay ubicaciones. La línea de `FormRecordValues` en `FormsPersonalDataPurgeTests` [E12] suma que la baja borra la ubicación de quien la mandó (23 y 230). Copia `FormRecordFile` [E18] y, de allá, `WhatsAppFlowLocationService`: bajo el lock del contacto, la ubicación o «Seguir sin ubicación» completan el envío más viejo que la espera, recién ahí sale el Flow, y la captura toma el punto de la sesión. Tests: los de B9 de la spec, más el Excel con «—» y la respuesta que queda tras la baja.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan, la spec de la E20 con su parte, las decisiones 261 a 264, 266, 267 y 269 de la hoja de ruta y las fichas de su «Qué leer». Esperá K0 y K1. Hacé B1 a B9 en orden, copiando el molde de cada tarea; no toques el front ni la tabla de estado del plan. La revisión adversarial de las tareas (R) va después, en la revisión de la etapa. Reglas: las de «Forma de trabajo» de este plan.
```

## Front

Las pantallas copian los tableros v175, con i18n en es y en, tipos generados, `shared/format` y TanStack Query. El canal se llama «App» (90). Tests con MSW, los ejemplos exportados, zod estricto y axe; cada lección de la spec lleva su test.

1. **F1 · Tipos, llamadas y subida con avance.** `F/src/modules/forms/api/entry.ts`, con la carga y lo propio; suma a `api/forms.ts` [E14] (`appOnly`, `whatsAppConnected`, `canLoad`, la ubicación y `lifecycle`), `api/records.ts` [E15] (el canal), `F/src/modules/whatsapp/chats/api/chats.ts` [E14] (el pedido de ubicación y los formularios apagados) y `F/src/shared/api/httpClient.ts`: una subida con `XMLHttpRequest` que informa el avance y conserva la sesión, los errores y el reloj del servidor. Copia `httpClient.ts`. Tests: zod estricto; el avance y el corte; con `sameOperation`, otra clave después de un envío que llegó o de un 400 sin errores por pregunta, y la misma tras un corte.
2. **F2 · Entradas, lista y lo propio.** Suma a `FormsPage.tsx` [E14 y E17], `FormRecordsPage.tsx`, `QuickView.tsx` y `RecordFullPage.tsx` [E15], `PhotoViewer.tsx` [E18] y `registration.ts`: con solo `formrecords.create` se entra a `/org/formularios` (con `/entry`) y a `/org/formularios/:id/respuestas` y `…/completa`, que leen `own-records/*`. Tableros `FRM-Formularios-20` y `FRM-Respuestas-Propias`, con sus teléfonos. Tests: los de F2 de la spec (261, 263 y 264), con «Todavía no cargaste» y uno solo para clientes apagado, con su motivo; «Estado» y «Dónde se completa», solo con `whatsAppConnected`; lo propio, con «Empresa», sin «Cargó» ni «Exportar a Excel», y sin `forms.read`.
3. **F3 · La forma editable.** Suma a `F/src/modules/forms/components/records/RecordForm.tsx` y `lib/recordForm.ts` [E15] el modo para cargar, sin otra grilla ni otras zonas (266). Tableros `FRM-Cargar-Respuesta` y `FRM-Cargar-Panel`. Tests: los de F3 de la spec, más «3 de 5 respondidas».
4. **F4 · Controles y lista de catálogo.** `components/entry/EntryField.tsx`, con la pieza `Picker` y sus opciones del servidor, sin otra lista (262). Copia `QuestionEditor.tsx` [E14]. Tableros `FRM-Cargar-Lista` y su teléfono. Tests: la búsqueda en el servidor y «Mostrar 50 más»; una sola opción, ya elegida; «Te falta el dato «Sucursal».», con Enviar grisado; «Elegí primero «Zona»» y «Cambió «Zona»: volvé a elegir.»; un número se lee con `parseDecimalText` [E12]: «1.500» es `numberAmbiguous` en su pregunta, y «1,5» llega como el texto `1.5`, con todos sus dígitos (207 y T9).
5. **F5 · Lo condicional y los cálculos.** Suma a `RecordForm.tsx`, con `lib/conditions.ts` y `lib/formula.ts` [E19]. Tests: los de F5 de la spec, más una pregunta que aparece en su lugar, la lista de una pregunta oculta, el cálculo con un campo del catálogo y 2,205 → 2,20; O19 de la lista de control.
6. **F6 · Los archivos.** `components/entry/EntryFiles.tsx`. Tableros `FRM-Cargar-Archivos` y su teléfono. Tests: tipo, tamaño y cantidad antes de enviar; «Tomar foto» primero y «Solo cámara» sin galería; la barra sin palabras.
7. **F7 · El bloque.** Suma a `components/records/RecordRowsTable.tsx` y `RecordRowsCards.tsx` [E19] el modo para cargar (↑ ↓, 🗑, «Agregar ítem» y los errores), sin otra tabla (266), y a `RecordForm.tsx`. Tableros `FRM-Cargar-Respuesta`, su teléfono y `FRM-Cargar-Panel`. Tests: los de F7 de la spec, más «Hasta 20 ítems.» y los cuatro textos de allá.
8. **F8 · La página, el panel y «Enviada».** `pages/LoadResponsePage.tsx`, `components/entry/EntryPanel.tsx` y `EntrySent.tsx`; `registration.ts`, con `/org/formularios/:id/cargar`. Copia `useEditForm` y `useUnsavedChangesGuard`; el panel usa `SidePanel.tsx` [E15]. Tableros `FRM-Cargar-Respuesta`, `FRM-Cargar-Panel` y `FRM-Cargar-Enviada`, con sus teléfonos. Tests: los de F8 de la spec, más «Pantalla completa», «Enviando…» con su aro y la barra, y los estados de `FRM-Cargar-Enviada`.
9. **F9 · «Dónde se completa», publicar, el editor, la planilla y el chat.** Suma al editor [E14], a `PublishDialog.tsx` y `CatalogQuestionFields.tsx` [E16], `PublicationBand.tsx` y `SendFormDialog.tsx` [E14], `LifecycleDialogs.tsx` [E17], `RowsBlockEditor.tsx` y `ConditionsEditor.tsx` [E19], y `FormRecordsPage.tsx`, `RecordFiltersMenu.tsx`, `lib/recordFilters.ts`, `QuickView.tsx`, `RecordFullPage.tsx` y `RecordValues.tsx` [E15]. Tableros `FRM-Editor-20`, `FRM-Publicar-20`, `FRM-Editor-Listas`, `FRM-Respuestas-20`, `FRM-Respuesta-20` y `WA-Chats-Formulario-20`, con sus teléfonos. «Dar de baja» suma a «Deja de pasar» la fila «App · No se carga más» («App · No longer filled in») si la versión se carga en la app, y en una de la app es la única, sin lo de WhatsApp (78 y 229). Tests: los de F9 de la spec; sin número, sin el ajuste; con él, en la pieza (262); O16 y O18 de la lista de control; en «Publicar», «Pasar a «Solo en la app»» guarda el borrador (`version` y su 409) y vuelve a comprobar; bajo una condición, solo cambia el ajuste; la banda; la ubicación en el chat; el filtro «Canal»; la línea gris y el renglón «La cargó» (267), sin «Abrir conversación»; las líneas de «Dar de baja».

10. **F10 · La ubicación.** `components/entry/EntryLocation.tsx`; suma al editor, a `RecordValues.tsx`, `RecordActivity.tsx` y a la planilla [E15]. Copia `EntryFiles.tsx`. Tableros `FRM-Editor-Ubicacion`, `FRM-Cargar-Respuesta` y `FRM-Cargar-Panel`. Tests: los de F10 de la spec; `getCurrentPosition` con alta precisión, sin caché y hasta 20 s.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-20-cargar-desde-la-web.md y la spec de la E20 con su parte, con las decisiones 261 a 264, 266, 267 y 269 de la hoja de ruta y «Cómo se lee una respuesta» de la parte de la E15. Esperá K1 y corré npm run contracts. Hacé F1 a F10 contra los tipos generados, copiando los tableros v175; no toques el lienzo, las specs, el back ni la tabla de estado del plan. Al cerrar la parte, también npm run build. Reglas: las de «Forma de trabajo» de este plan.
```

## Revisión y cierre

Sin tareas: los hace el conductor ([conducir una etapa](../guides/conducir-una-etapa.md)). La lista de control de la revisión suma los riesgos de la [prueba integral de ArquitecturaBase](../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase): a 1024 px, «Solo en la app» entra en su tarjeta (O18); el cálculo en vivo lee con `parseDecimalText` y la cultura de la cuenta, sin resultado mientras un número sea ambiguo, igual que «Enviada» (O19), y «Según otra respuesta» sin su pregunta se frena en la pantalla, marcando solo ese campo (O16). Al cerrar, `docs/features/formularios.md` suma la sección, y nace el `AGENTS.md` de `components/entry/` en el front.

**Pendiente para la I1 de la E10** (123). El resto del paso 5: conectar un número, volver a publicar y mandar el formulario por WhatsApp a un celular; y el pedido de ubicación con el número de una organización, con la ubicación, «Seguir sin ubicación» y el Flow después.

## Verificación de la etapa

Lo que recorre la verificación de esta etapa, el segundo hito, después del código listo ([conducir una etapa](../guides/conducir-una-etapa.md)). Cuando el cierre retira el plan, esta sección pasa a `docs/backlog.md`, a «Verificación de la etapa 20-cargar-desde-la-web».

- **E2E real** (`F/scripts/test-e2e-real-forms.mjs` [E12]): los pasos 1 a 4, 6 y 8 de «Cómo se cierra» de la spec, comparando con una respuesta del Meta simulado, y del paso 5, publicar y cargar sin número, también sobre uno dado de baja.
- **Recorrido:** `cargar-una-respuesta`, sumado a `F/scripts/recorrido-usuario-forms.mjs` [E12].
- **Galería** (`scripts/capturas/etapa-20.json`), a 1440, 390 y 320: los tableros de la etapa.
- **Celular:** el paso 7 en un Android por USB (`chrome://inspect`, del 5174 a `127.0.0.1:5174`, y `https://localtest.me:5174`): una foto con la cámara, dos ítems en sus recuadros y la ubicación. El usuario recibe los pasos clic por clic; nadie maneja su PC. Con lo que dé el teléfono nace `docs/guides/probar-en-el-celular.md`.

## Relación con otras etapas

**Usa de las anteriores:**
- **Base:** los botones que trabajan (43 y 115), `PageLoading`, `FormSkeleton` y la escala (180). No suma fuentes a la campana.
- **Base 2, E14 a E19:** lo de K0.

**Deja para las siguientes:**
- **Back:** `FormEntryService`, `FormEntryOptionsResolver`, `FormEntrySubmitService`, `BusinessFormEntryController`, con la carga y lo propio, `FormEntryDurableAdapter`, `FormEntryUploadSlotAttribute` y `FormEntryUploadLimiter`, con `Forms:Uploads:*`; «solo lo mío» en `FormRecordQueryFilters`.
- **Rutas, datos y contrato:** todo lo de «Contrato».
- **Front:** el modo para cargar de `RecordForm.tsx`, `pages/LoadResponsePage.tsx`, `components/entry/*`, el modo para cargar de `RecordRowsTable.tsx` y `RecordRowsCards.tsx`, la subida con avance y «App · No se carga más» en `LifecycleDialogs.tsx`.
- **Para quién:** la E21 corrige en el lugar con el modo para cargar de `RecordForm.tsx`, el multipart con su lugar de subida, `FormEntrySubmitService`, `FormEntryDurableAdapter` y `sameOperation`, suma a `own-records` y suma la ubicación al PDF y a las reglas; la E21, la E23 y la E25 conservan la fila de «Dar de baja»; la E22 suma a lo que frena publicar una plantilla lo que trae `whatsApp`; la E23 deja afuera las versiones `AppOnly` y pide la ubicación antes del Flow; la E25 pasa al formulario desde `LoadResponsePage.tsx`.

## Cambios al contrato


## Para decidir

Nada: lo del traspaso 15 son la 229 y la 230.
