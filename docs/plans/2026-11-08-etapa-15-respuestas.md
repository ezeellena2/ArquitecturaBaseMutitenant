# E15 · Respuestas: planilla, detalle, Excel y tablero · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/15-respuestas.md` y su parte `15-parte-datos-y-contrato.md`. Tableros: lienzo v175 y los que la spec manda redibujar. Decisiones pendientes: ninguna.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B12 | Pendiente |
| Front | F1–F8 | Pendiente |
| Integración y puerta | I1–I3 | Pendiente |

## Forma de trabajo

Modo rápido: primero el contrato (K0 y K1); después Back y Front en paralelo, y una sola puerta al final, sin prueba con Meta.

Cada tarea lleva sus pruebas, y un arreglo corre solo lo que toca. La tabla de estado la actualiza la Guía.

**Revisión adversarial:** la llevan las tareas **(R)**, completa y antes de su commit, como dice [AGENTS.md](../../AGENTS.md).

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [E14] Los valores, las versiones, la lista, las pestañas, el participante y la sesión. [E12] La barrera, `IMemberDirectoryReader` y las columnas fijas | no existen | K0 |
| [E11] `/org/whatsapp/chats/:conversationId` | existe, pero toma la bandeja de `?inbox=`: sin ella, abre Clientes | precisión 11 |
| Front: el panel al costado | no hay: `Sheet.tsx` es una hoja desde abajo | F5 crea `src/shared/ui/SidePanel.tsx` |
| EF: agrupar en otra zona | Npgsql 10.0.3 traduce `TimeZoneInfo.ConvertTimeBySystemTimeZoneId` en un `GroupBy`, como `AssistantUsageReader.cs` | B9 copia esa forma |
| Nombres por cuenta | `IUserDisplayNameReader.FindAsync(userIds)` da el nombre de cada cuenta, sea o no miembro | B5 lo usa |
| La zona de quien mira | `IProfileService.GetAsync(...).TimeZoneId` | nada |
| `SearchIndexTests` | lee solo los readers del núcleo | B2 declara la búsqueda del módulo |
| `SortIndexTests` | en una tabla privada exige `(TenantId, campo, Id)` | precisiones 2 y 3 |
| EF y las ventanas | EF no traduce `ROW_NUMBER`, `LAG` ni `LEAD` | precisión 4 |

## Contrato

Todas las rutas son de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`, y cuentan en las cuotas generales. Los `POST` solo leen: sin `[Idempotent]` ni `version`. Toda ruta responde además 401, 403, 404 con la feature apagada y 400 `Validation.Failed`. El permiso se comprueba antes de buscar el recurso, y lo ajeno es `Forms.Definition.NotFound`. Cuerpos, tipos y `filtros`: los de la spec, con estas precisiones.

| Método y ruta | Permiso | Controller | Errores propios |
| --- | --- | --- | --- |
| `GET /` (suma a `sort` `recordCount`, `lastRecordNumber` y `lastRecordAtUtc`, y `recordCount?` y `lastRecord?`) | `forms.read` | `BusinessFormsController` [E14] | — |
| `GET /overview` | `formrecords.read` | `BusinessFormsController` | — |
| `GET /{id}/records/columns` | `formrecords.read` | `BusinessFormRecordsController` | — |
| `POST /{id}/records/search` | `formrecords.read` | ídem | `Forms.RecordFilter.Invalid` |
| `POST /{id}/records/facets` | `formrecords.read` | ídem | `Forms.RecordFilter.Invalid` |
| `POST /{id}/records/facet-options` | `formrecords.read` | ídem | `Forms.RecordFilter.Invalid` |
| `GET /{id}/records/{recordId}` | `formrecords.read` | ídem | `Forms.Record.NotFound` |
| `POST /{id}/records/{recordId}/navigation` | `formrecords.read` | ídem | `Forms.Record.NotFound`, `Forms.RecordFilter.Invalid` |
| `POST /{id}/records/export` → `.xlsx` | `formrecords.export` | ídem | `Forms.RecordFilter.Invalid`, `Forms.RecordExport.TooMany`, `Busy`, `TimedOut` y `TooManyAtOnce` |
| `POST /{id}/records/dashboard` | `formrecords.read` | ídem | `Forms.RecordFilter.Invalid` |
| `GET /{id}/dashboard-settings` | `formrecords.read` | ídem | — |
| `PUT /{id}/dashboard-settings` → 204 | `formrecords.read` | ídem | `Forms.Dashboard.SettingsInvalid` |

**Precisiones del contrato** (técnicas, decididas acá):
1. **Claves de columna:** una pregunta es `field:<fieldId>`; un campo sellado, `field:<fieldId>:<clave del campo>`, con su `catalogField.id` para ordenar; las fijas que se ocultan, `submittedAtUtc`, `respondent`, `channel` y `companies`. N° no se oculta. Las subcolumnas son los campos del catálogo de la pregunta, también los quitados, en su orden.
2. **El orden de la planilla** (264) usa la regla común de la base, con las claves de la parte en `FormRecordQueryFilters`, porque las columnas son dinámicas; un dato sellado compara su valor con su tipo. Usa `(TenantId, DefinitionId, SubmittedAtUtc, Id)` y el índice único `(TenantId, DefinitionId, Number)` de la E14. Vacíos al final; desempate por `-submittedAtUtc` e `Id`; lo que no ordena da 400.
3. **La lista:** `lastRecord` es la respuesta con `Number = LastRecordNumber`. Los órdenes `recordCount`, `lastRecordNumber`, `lastRecordAtUtc` y el `name` de `overview`, que es el de la versión activa, salen de otra tabla: entran en `SortIndexTests` como proyecciones sin índice, porque una organización tiene pocos formularios.
4. **La navegación** es una sola consulta de EF con subconsultas: la anterior y la siguiente por la clave del orden, y la posición contando las que van antes. Sin SQL a mano y sin traer ids.
5. **Valores:** en `choices[]`, `fields` es `[{key, name, type, value, text?}]`, con `value` canónico como en la E13 y `text`, el texto sellado de la opción en un campo «De un catálogo». En el detalle, `questions[]` es la forma de la versión de la respuesta (266), y `titles[]`, `{titleId, text, start}`, como en la E14.
6. **Quién:** `respondent.name` nulo se lee como dice la spec; `companies[]` es `[{id, name}]`; `respondents[].id` es la cuenta del empleado o el contacto del cliente.
7. **Facetas:** una pregunta con opciones propias ofrece las de todas sus versiones; una de catálogo, las que eligió alguna respuesta, con la última etiqueta sellada. `retired` es la opción propia que no está en la versión activa, o la opción a mano dada de baja o borrada. Buscar en `respondent` pide a los lectores de B5 hasta 100 ids por nombre.
8. **`received`:** `preset` se resuelve en la zona de quien mira, con fin exclusivo; `fromUtc` y `toUtc` llegan en UTC.
9. **Tablero y Excel:** las claves, los períodos, los números, los ajustes, el archivo y la hoja van en la parte, con los archivos que no nombran las tareas.
10. **Abrir conversación:** el detalle suma `conversationInbox?` (`customers` o `internal`) junto a `conversationId?`, porque la ruta del chat toma la bandeja de `?inbox=`.

**Errores** (en `Domain/Forms/`, textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo y campo | es | en |
| --- | --- | --- | --- |
| `Forms.Record.NotFound` | NotFound | No encontramos esa respuesta. | We couldn't find that response. |
| `Forms.RecordFilter.Invalid` | Validation (`conditions[i]`, `received` o `sort`) | Este filtro no es válido. | This filter isn't valid. |
| `Forms.RecordExport.TooMany` | Conflict | El máximo es 10.000 respuestas. Acotá los filtros de la planilla. | The limit is 10,000 responses. Narrow the table filters. |
| `Forms.RecordExport.Busy` | Conflict | Ya tenés una exportación en curso. Esperá a que termine. | You already have an export in progress. Wait for it to finish. |
| `Forms.RecordExport.TimedOut` | ServiceUnavailable | No pudimos armar el archivo. Probá de nuevo. | We couldn't build the file. Try again. |
| `Forms.RecordExport.TooManyAtOnce` | TooManyRequests, con `Retry-After` | Hay muchas exportaciones en curso. Probá en un momento. | There are too many exports in progress. Try again in a moment. |
| `Forms.Dashboard.SettingsInvalid` | Validation (`hiddenCards` o `countBy`) | Los ajustes del tablero no son válidos. | The dashboard settings aren't valid. |

**Permiso nuevo:** `formrecords.export`, «Exportar respuestas» / "Export responses": «Bajar en Excel las respuestas, con los filtros aplicados.» / "Download the responses to Excel, with the filters applied.". Va en `Area.formrecords` («Respuestas», 257) e incluye `formrecords.read` (`Includes`, 260), porque se exporta desde la planilla; y en el seed de `TenantAdmin` y la migración de los roles de sistema.

### K0 · Lo que dejó la etapa anterior

El chat del contrato la confirma al arrancar y anota cada diferencia en «Cambios al contrato».
- **Base de la 12:** de su F3, `FilterBar.tsx` con un solo «Filtros», chips y «Limpiar todo», y `DataTable.tsx`; `PageLoading`, `FormSkeleton`, los botones con su espera, `ConfirmDialog`, `InfoTip` y la galería a 320.
- **Base de la 12, segunda parte (214):** la hora con coma y `formatMonth` como rango (205); `PercentText` (208); `disabledReason` con un toque (211); `Picker` (262); el orden por columnas de `DataTable` y `FilterBar`, con la regla común del back (264); el chip de solo lectura y el título de grupo con su (i) (213); `PersonalDataPurgeTests` (0062).
- **E12:** `Permissions.Forms`; `FormsModelContributor`, `FormsContractContributor`, `FormsTexts{,.en}.resx` y `FormsErrorTextSource`; `FormCatalogErrors.cs`; `FormWorkLease` e `IFormWorkLease` (clave `(TenantId, UserId)`, dos minutos); `IMemberDirectoryReader` y `MemberDirectoryReader`, en las dos guardas de identidad; `IStructuredContactSource`, su apagado y `WhatsAppStructuredContactSource`; `FormCatalogFiltersPolicy`, `FormCatalogExportBuilder` e `IFormCatalogSpreadsheet`; la prueba de volumen; la exportación de ejemplos a `docs/contracts/examples/forms.json` y su guarda; en el front, `registration.ts`, sus `locales`, `pinned` de `table.tsx`, `test-e2e-real-forms.mjs` y `recorrido-usuario-forms.mjs`.
- **E13:** `FormCatalogs.FieldsJson` con `{id, key, name, type, …, isActive}`, también los quitados; `CatalogCombo.tsx` y `ColumnsMenu.tsx`, con lo elegido en la URL.
- **E14:** `FormDefinitions`; `FormDefinitionStates` (`ActiveVersionId`, `LastRecordNumber`); `FormVersions` (`Number`, `Name`, `Questions`, `Titles`); `FormRecords` (`Number` único por definición, `SourceKey`, `Channel`, `RespondentKind`, `RespondentUserId`, `RespondentContactId`, `RespondentCompanyIds`, `SubmittedAtUtc`); `FormRecordValue` (clave `(TenantId, RecordId, FieldId, Position)`, columnas tipadas, `ChoiceId`, `ChoiceLabel`, `CatalogId`, `ChoiceCode`, `ChoiceDetail`, `ChoiceValue` y `ChoiceFields`, con `{id, key, name, type, value}` y el texto de la opción en un campo «De un catálogo»); `FormQuestionType`; `IFormReader`, `FormReader` y `FormSummaryResponse`; `BusinessFormsController`; `FormRecordErrors.cs`; `FormsAccountDeletionParticipant` y su cleanup; `WhatsAppFlowSessions` (`ConversationId`, `SentByUserId`, la fecha de envío y `RecordId`) con el `SourceKey` `whatsapp:<sessionId>`; el Meta simulado de B12; en el front, `FormsPage.tsx`, `FormPage.tsx` con `formTabs.ts`, las rutas `/org/formularios/:id` y `api/forms.ts`.

### K1 · Publicar

El Back publica el contrato y regenera `docs/contracts/openapi.json`; el Front corre `npm run contracts`, sin tipos a mano. Cada tarea del Back graba con la exportación de la E12 los ejemplos reales de sus respuestas, con errores y vacíos, en `docs/contracts/examples/forms.json`, y `contracts:check` los valida.

**Texto para el chat de Contrato**

```text
Leé AGENTS.md, docs/plans/2026-11-08-etapa-15-respuestas.md, la spec 15-respuestas.md con su parte y las decisiones 264 y 266 de la hoja de ruta. Hacé K0: confirmá la lista contra el código y anotá cada diferencia en «Cambios al contrato» antes de seguir. Después K1: publicá rutas, permiso, errores con sus textos es/en y precisiones, generá openapi.json y avisá a la Guía para que el front corra npm run contracts. No programes la lógica. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo, con git commit -- <rutas>, en español y conventional commits; con la app levantada nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtrar por módulo, y los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Back

Cada servicio público va en `OperationLog.RunAsync`, sin valores, textos buscados, personas ni filtros en los logs. Las lecturas no abren UoW; los ajustes del tablero, una. Forms nunca lee tablas de WhatsApp ni la cuenta: usa los puertos. Toda consulta de valores parte de las respuestas del formulario, nunca solo del `FieldId`.

1. **B1 · Condición, errores y permiso (R: permisos).** `D/Forms/FormRecordCondition.cs` (la condición y los operadores de cada tipo, con la gramática de la E12), `FormRecordFilterErrors.cs`, `FormRecordExportErrors.cs` y `FormDashboardErrors.cs`; suma `NotFound` a `FormRecordErrors.cs` [E14]; el permiso en `Permissions.cs`, `Permissions{,.en}.resx` y el seed. Copia `D/Forms/FormCatalogErrors.cs` [E12]. Tests: los operadores de cada tipo, `ErrorCodeTests`, `PermissionTextsTests` y `ResourceParityTests`; un rol con solo `formrecords.export` ve la planilla (260).
2. **B2 · Tablas, índices y migraciones (R: datos y aislamiento).** `D/Forms/FormDashboardSettings.cs` y `FormRecordCatalogField.cs`, con sus configuraciones en `I/Modules/Forms/Persistence/Configurations/`; `FormWorkLeaseConfiguration.cs` e `IFormWorkLease` [E12] suman el carril, `Lane` (`Catalogs` o `Records`), a la clave, y `RecordExport` a `Kind`. Migraciones `<fecha>_FormRecordsRead.cs` (las dos tablas con RLS y `GRANT`, el relleno de `FormRecordCatalogFields` con las respuestas que existen, los índices de la spec y `Catalogs` para las barreras que ya existen) y `<fecha>_SystemRoleFormRecordsExport.cs`, con el SQL en una línea y sin `\r`; el `Down` se niega si hay ajustes guardados. La excepción de la 0060 de la parte va anotada en la migración. Copia `AssistantOrganizationSettingsConfiguration.cs` y `20261010203618_SystemRoleAssistantPermissions.cs`. Tests: RLS de las dos tablas nuevas; `SearchIndexTests` declara `IX_FormRecordValues_Search_TextValue`; la migración hacia atrás; los roles existentes reciben el permiso.
3. **B3 · Columnas (R: datos, por la captura).** `A/Modules/Forms/Services/FormRecordColumnsBuilder.cs`: fija las versiones del formulario una vez por pedido, deserializa cada `Questions` una sola vez y arma columnas y subcolumnas (precisión 1). Copia `FormCatalogExportBuilder.cs` [E12]. Lee las subcolumnas de `FormRecordCatalogFields` (F15), que escribe la captura: suma a `FormRecordWriter.cs` [E14], en su UoW. Tests: el orden activo y las retiradas; subcolumnas con un campo quitado; una publicación a mitad del pedido no mezcla columnas; un campo que solo selló una respuesta vieja aparece; una captura repetida no duplica; la lectura no escribe.
4. **B4 · Filtros y orden.** `A/Modules/Forms/Interfaces/Persistence/IFormRecordReader.cs`; `I/Modules/Forms/Persistence/Readers/FormRecordReader.cs` y `FormRecordQueryFilters.cs`, la única función de filtros y orden de la planilla, los conteos, la navegación, el Excel y el tablero; `A/Modules/Forms/Services/FormRecordFiltersPolicy.cs`, que valida contra las columnas y decide en memoria qué versiones tienen cada pregunta; `IFormRecordQueryService.cs` y `FormRecordQueryService.cs`. La búsqueda mira solo los textos largos, con `ApplySearch`. Copia `CompanyReader.cs` y `FormCatalogFiltersPolicy.cs` [E12]. Tests: la tabla de verdad de `answered`, `unanswered` y `notAsked`, igual en memoria y en SQL; cero y `false`; `anyOf`; una opción retirada; ordenar por una opción con el texto sellado, sin un `CASE` por versión, y por un dato sellado numérico como número, con lo vacío al final en los dos sentidos (264); una segunda página; un rango invertido y una columna que no ordena dan 400.
5. **B5 · Nombres por lote.** Los nombres de las cuentas, con `IUserDisplayNameReader`; suma a `IMemberDirectoryReader.cs` y `MemberDirectoryReader.cs` [E12] las empresas por id y la búsqueda de cuentas por nombre entre todos los miembros, también los que se fueron; y a `IStructuredContactSource.cs`, su apagado y `WhatsAppStructuredContactSource.cs` [E12], los nombres de los contactos por id y por búsqueda, sin teléfonos. Un pedido de cada uno por página. Copia esos mismos. Tests: un pedido por página; ex miembro; cuenta dada de baja; sin WhatsApp; las guardas de identidad sin cambios; otra organización.
6. **B6 · Conteos.** Suma `facets` y `facet-options` a `FormRecordReader.cs` y `FormRecordQueryService.cs`: cada dimensión ignora solo su filtro, se piden solo las visibles y «elegir varias» cuenta la respuesta en cada opción (precisión 7). Copia `CompanyReader.cs`. Tests: cada dimensión ignora la suya; históricas en cero; una respuesta en dos opciones; 20 y `hasMore`; la segunda página de `facet-options`.
7. **B7 · Detalle, origen y navegación (R: permisos, por la bandeja).** `A/Interfaces/Integrations/StructuredData/IStructuredRecordOriginSource.cs`, `A/Models/StructuredData/StructuredRecordOrigin.cs`, `I/StructuredData/DisabledStructuredRecordOriginSource.cs` (con `TryAdd`) y `A/Modules/WhatsApp/Services/WhatsAppStructuredRecordOriginSource.cs`: con el `SourceKey`, quién la mandó y cuándo, y la conversación con su bandeja solo si quien mira tiene el permiso de esa bandeja. Suma al reader el detalle, con la forma de la versión de la respuesta (266), y la navegación (precisión 4). Copia `IStructuredContactSource.cs` [E12]. Tests: la forma es la de la versión de la respuesta, no la activa; sin el permiso de la bandeja no hay conversación; sin WhatsApp no hay origen; anterior, siguiente y posición con empates, filtros y en los bordes, en una consulta; ausente, excluida y ajena dan el mismo 404; `ModuleIsolationTests`.
8. **B8 · Excel (R: archivos y cuotas por alcance).** `A/Modules/Forms/Interfaces/Integrations/IFormRecordSpreadsheet.cs`, `I/Modules/Forms/FormRecordSpreadsheet.cs` (ClosedXML), `IFormRecordExportService.cs`, `A/Modules/Forms/Services/FormRecordExportService.cs` y `FormRecordSheetBuilder.cs`. Toma la barrera del carril `Records` sin esperar, lee los ids ordenados hasta 10.001, hidrata en lotes de 100 con un presupuesto de 30 s y suelta la barrera. Copia `IWhatsAppContactSpreadsheet.cs` y `WhatsAppContactSpreadsheet.cs`. Tests: 10.000 y 10.001; `Busy` entre dos exportaciones, y una vista previa de catálogo no la frena; `TooManyAtOnce` con `Retry-After`; el presupuesto; tipos de celda; 16 dígitos como texto; un texto con `=` queda literal; «; »; la columna «pregunta · valor»; todas y solo las visibles; encabezados en es y en; los bytes se abren con ClosedXML; el nombre en la zona de quien exporta.
9. **B9 · Tablero y ajustes (R: datos).** `IFormDashboardService.cs`, `A/Modules/Forms/Services/FormDashboardService.cs` y `FormDashboardBuilder.cs`; `IFormDashboardSettingsRepository.cs` e `I/Modules/Forms/Persistence/Repositories/FormDashboardSettingsRepository.cs`; suma los ajustes a `FormsAccountDeletionParticipant.cs` y a su cleanup [E14], e inscribe `FormDashboardSettings` en `tests/ArquitecturaBaseMultitenant.ArchitectureTests/PersonalDataPurgeTests.cs` (lo crea la B2 de la base 2): la purga la baja de la cuenta. Agrupa en SQL en la zona de quien mira y calcula `share` y `sumShares` (la parte de la spec). Copia `AssistantUsageReader.cs` para agrupar y `AssistantAccountDeletionParticipant.cs`. Tests: otra zona alrededor de la medianoche; semanas; días vacíos; 366 períodos; seis filas con «Otras»; sin tarjeta para una fecha; un `countBy` por tarjeta; sumas sin redondeo; `share` en «elegir varias», en «Otras» y sin respuestas; la baja de la cuenta borra los ajustes.
10. **B10 · La lista.** Suma a `IFormReader.cs`, `FormReader.cs` y `BusinessFormsController.cs` [E14] `recordCount`, `lastRecord` y `GET /overview`, con los filtros de las dos rutas en una sola función (precisión 3). Copia esos mismos. Tests: una consulta paginada, sin consultas por fila; `overview` con el nombre de la versión activa aunque el borrador cambió; `null` sin `formrecords.read`; cada orden, sin respuestas al final.
11. **B11 · Rutas, contrato y ficha.** `P/Modules/Forms/Controllers/BusinessFormRecordsController.cs`, `FormRecordFileResults.cs`, los contratos y validadores de la parte y los ejemplos en `FormsContractContributor.cs`; la variante «filtros en el cuerpo», con su orden propio, en `paginado-y-busqueda.md`; las relaciones en `GrowingCollectionContractTests`. Copia `BusinessAssistantController.cs` y `WhatsAppFileResults.cs`. Tests: cada ruta con 401, 403, feature apagada, 400 y paginado; `ToString` de los pedidos sin filtros; los filtros privados nunca por query; ejemplos exportados.
12. **B12 · Volumen.** EXPLAIN sobre el SQL real de EF, con unas 10.000 respuestas, de la lista, la planilla (también ordenada por un dato sellado), los conteos, la navegación, los ids del Excel y el tablero. Copia la prueba de volumen de la E12.

**Texto para el chat del Back**

```text
Leé AGENTS.md, docs/plans/2026-11-08-etapa-15-respuestas.md, la spec 15-respuestas.md con su parte, las fichas de su «Qué leer» y las decisiones 263, 264 y 266 de la hoja de ruta. Después de K1, hacé B1 a B12 en orden, copiando los moldes de cada tarea, y grabá los ejemplos de tus respuestas; las tareas (R) llevan la revisión adversarial antes del commit. No toques el front ni la tabla de estado del plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo, con git commit -- <rutas>, en español y conventional commits; con la app levantada nadie edita el src del front; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin Docker ni filtrar por módulo, y los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Front

Todo va en `src/modules/forms`, con i18n es/en, los tipos generados, `shared/format`, TanStack Query y axe. Lo privado (búsqueda, personas y condiciones) vive solo en memoria, nunca en la URL, el storage ni una caché persistente. La URL lleva el orden, las versiones, «Columnas» (lo que difiere del valor por defecto de cada columna) y la vista rápida, `?respuesta=<recordId>`. La página y los filtros van en un estado del módulo por formulario, que comparten «Respuestas» y «Tablero» y que «Volver» recupera.

1. **F1 · Tipos y llamadas.** `api/records.ts`, y suma a `api/forms.ts` [E14] la lista y `overview`. Copia la API de la E14. Tests: zod estricto con los ejemplos exportados.
2. **F2 · La lista y las pestañas.** Suma a `FormsPage.tsx`, `FormPage.tsx` y `lib/formTabs.ts` [E14], y a `registration.ts`, `formrecords.export` en sus permisos y las rutas `/org/formularios/:id/respuestas`, `/org/formularios/:id/respuestas/:recordId/completa` y `/org/formularios/:id/tablero`; «Respuestas», con su cantidad, y «Tablero» van antes de «Diseño», y la barra aparece con dos pestañas (193). El nombre abre «Diseño» y el número de respuestas, la planilla (275); el encabezado ordena (264). Tableros `FRM-Formularios-15` y `M-FRM-Formularios-15`. Tests: las tres combinaciones de permisos; adónde abren el nombre y el número; los órdenes; sin `forms.read`, la lista sale de `overview`.
3. **F3 · La planilla.** `pages/FormRecordsPage.tsx`, `lib/recordFilters.ts` y `lib/values.ts`, con N° fijo por `pinned` [E12]; `ColumnsMenu.tsx` [E13] suma el valor por defecto de cada columna; «Ordenar por» y el encabezado que ordena (264). Lo privado no pasa por `useFilters` ni `usePagedQuery`. Tableros `FRM-Respuestas` y `M-FRM-Respuestas`. Tests: «—», «Sin respuesta», cero y `false`; «elegir varias» en su orden; las subcolumnas; «Es cliente», después de Cargó, solo si alguna fila de la página es de un cliente, sin marca al lado del nombre (206); el orden: el primer toque, la página 1 y «Limpiar todo»; lo privado fuera de la URL y del storage; un 403 retira todo; la relectura atenúa; axe.
4. **F4 · «Filtros» y los chips.** `components/records/RecordFiltersMenu.tsx` y `RecordFacetControl.tsx`, sobre el `FilterBar` y el `Picker` de la base (262). Tableros `FRM-Respuestas-Filtros` y la pantalla «Filtros» de `M-FRM-Respuestas`. Tests: cada cambio se aplica enseguida; la pieza de una opción, sin cantidades, con el buscador adentro y «Mostrar N más»; el chip nombra lo elegido aunque no esté en la página (`knownItems`); «Limpiar todo»; una faceta que falla no frena el filtro.
5. **F5 · La respuesta como el formulario y la vista rápida.** `src/shared/ui/SidePanel.tsx`, el panel al costado; `components/records/RecordForm.tsx`, `RecordValues.tsx` y `lib/recordForm.ts`, la forma de solo lectura de la parte de la spec (266); `QuickView.tsx`; `RecordActivity.tsx`, la actividad (267). La lectura de `Picker` deja varias enteras, sin «y N más» ni «…», y el campo baja de renglón (174 y 266): es la excepción escrita en las fichas de pantallas y de formatos del front. Tablero `FRM-Respuesta`. Tests: los dos anchos; tres, dos y una columna según el ancho de la zona; una pregunta con sus datos en un renglón; «N de M respondidas»; lo que no aporta no va; la actividad en recuadros; ↑ ↓ con el teclado, Esc y foco útil; una respuesta tardía se descarta; las acciones como íconos con tooltip; «Abrir conversación» lleva `?inbox=` y aparece solo con la conversación.
6. **F6 · La pantalla completa.** `pages/RecordFullPage.tsx`. Tableros `FRM-Respuesta-Completa` y `M-FRM-Respuesta`. Tests: una entrada directa arranca sin filtros privados; «Volver» a la misma fila, página, filtros y orden; la actividad en ocho columnas, con «—» y su banda (267); el teléfono.
7. **F7 · Exportar.** `components/records/ExportDialog.tsx`, con «Respuestas» y «Filtros» en `DetailGrid` y los chips de la planilla sin la ✕ (226), y «Columnas» en la pieza (262). Tableros `FRM-Respuestas-Exportar` y su estado en `M-FRM-Respuestas`. Tests: los chips y «Ninguno»; «Solo las visibles» apagada sin columnas ocultas; con más de 10.000, «Exportar a Excel» apagado con su motivo en el tooltip (211), sin esperar el 409; `Busy`; «Exportando…» con el diálogo abierto hasta la respuesta; sin `formrecords.export` no aparece, y sin respuestas queda grisado con su motivo; el teléfono exporta todas las preguntas.
8. **F8 · El tablero.** `components/records/FormDashboard.tsx`, sin biblioteca de gráficos; los porcentajes, con `PercentText` sobre `share`, sin calcularlos. Tableros `FRM-Tablero` y `M-FRM-Tablero`. Tests: quitar, agregar, contar, ver como tabla, que ordena (264), y volver al automático; ningún número de arriba debajo de otro (263); los filtros de la planilla; los vacíos; el eje por día con la fecha completa en cada marca y en el cartel, con menos marcas, y por semana y por mes, el primer día en la marca y el rango en el cartel (205); axe.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-15-respuestas.md, la spec 15-respuestas.md con su parte y las decisiones 261 a 264, 266, 267 y 269 de la hoja de ruta. Esperá K1, corré npm run contracts y hacé F1 a F8 contra los tipos generados, copiando los tableros v175 de cada tarea, o los redibujados que dice la spec. No toques el lienzo, las specs, el back ni la tabla de estado del plan. Lo privado nunca va a la URL ni al storage. Por tarea, npx tsc -b y npm test; al cerrar la parte, build, lint y contracts:check. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo, con git commit -- <rutas>, en español y conventional commits; con la app levantada nadie edita el src; TDD donde hay lógica; al terminar cada tarea, build y tests rápidos enteros, sin filtrar por módulo, y los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Integración y puerta

1. **I1 · Integrar.** El front contra la API real; cada diferencia vuelve a su dueño y se anota en «Cambios al contrato».
2. **I2 · Puerta.** `/puerta` una vez, sobre copias del corte: build y `dotnet test` del back; build, lint, test y `contracts:check` del front; el E2E real (`test-e2e-real-forms.mjs`) con respuestas del Meta simulado de la E14 (un catálogo con campos, «elegir varias» y una opcional vacía, de la lista al tablero); `realinear-docs` y `estado-docs` en 0; la galería (`scripts/capturas/etapa-15.json`) a 1440, 390 y 320, solo de los tableros de la spec, con sus teléfonos; y la lista de control, con un test por punto que aplica (1, 3, 5, 6, 7, 8 y 10). El retiro del módulo no corresponde.
3. **I3 · Recorrido y cierre.** `/recorrido` (`recorrido-usuario-forms.mjs`) con `revisar-las-respuestas`, `del-catalogo-a-la-planilla` y `del-campo-a-la-planilla`, más un cliente con su nombre y «Abrir conversación», un enlace abierto en otra pestaña sin los filtros privados y el `.xlsx` abierto en Excel sin reparación. Al cerrar, `docs/features/formularios.md` suma «Respuestas»; cada carpeta nueva trae su `AGENTS.md` y `CLAUDE.md` desde que nace, con su fila en `docs/architecture/arbol.md`.

| Puerta | Resultado |
| --- | --- |
| Back / Front / Contrato / E2E / Documentación / Galería / Recorrido | Pendiente |

**Texto para el chat de Integración**

```text
Leé AGENTS.md de los dos repos, docs/plans/2026-11-08-etapa-15-respuestas.md y la skill /puerta. Hacé I1 a I3: integrá contra el contrato, devolvé cada diferencia a su dueño y corré la puerta una sola vez sobre copias del corte; después de un arreglo, solo lo que toca. No toques el lienzo ni manejes la PC del usuario. Dejá solo la tabla final corta en este plan. Reglas: todo en main, sin ramas, worktrees, stash, reset ni push; commiteá solo lo tuyo, con git commit -- <rutas>, en español y conventional commits; con la app levantada nadie edita el src del front; TDD donde hay lógica; al terminar cada arreglo, build y tests rápidos enteros, sin Docker ni filtrar por módulo, y los de integración y seguridad del área si tocó rutas, persistencia, autenticación o aislamiento; el contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.
```

## Relación con otras etapas

**Usa de las anteriores:** la lista de K0; de la E11, las bandejas, con `whatsapp.customers` y `whatsapp.internal`, y `/org/whatsapp/chats/:conversationId?inbox=`; del núcleo, `IUserDisplayNameReader` e `IProfileService`.

**Deja para las siguientes:**
- **Back:** lo de B1 a B11, en especial `FormRecordQueryFilters`, `IStructuredRecordOriginSource`, el carril `Records` y las claves de columna, de orden y de tarjeta.
- **Front:** lo de F1 a F8, en especial la forma de solo lectura (`lib/recordForm.ts`, `RecordForm.tsx` y `RecordValues.tsx`), `RecordActivity.tsx` y `SidePanel.tsx`.

## Cambios al contrato


## Para decidir

Nada.
