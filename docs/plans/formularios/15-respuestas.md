# E15 · Respuestas: planilla, detalle, Excel y tablero (spec)

**Estado:** pendiente · spec de la etapa 15 del bloque (decisiones 34, 57, 78 y 79), con el [formato](plantilla.md) de las etapas. Es lo que ArquitecturaBase llama etapa 3, con el menú `MF1`, el detalle completo (`DC0` y `DC1` ajustado) y el tablero `DOC7-A`. **Solo lee,** salvo los ajustes del tablero de cada persona. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E14.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T7, T11 y T22, §3 (`FormVersions`, `FormRecords` y `FormRecordValues`) y §7 (puertos); y la [E12](12-catalogos.md): C5, C8, C11 y la §4.
2. **Decisiones** de la hoja de ruta: 9, 23, 24, 35, 43, 49, 57, 78 y 79.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) | la planilla, sus conteos y la variante con filtros en el cuerpo, que nace acá |
   | [multitenancy](../../rules/multitenancy.md) y [permisos](../../rules/permisos.md) | todo se lee con RLS; lo ajeno y lo excluido dan el mismo 404 |
   | [modulos](../../rules/modulos.md) | el nombre del cliente y su conversación, solo por puertos neutrales |
   | [numeros-y-moneda](../../rules/numeros-y-moneda.md) y [fechas-y-zonas](../../rules/fechas-y-zonas.md) | el Excel, los totales y los períodos en la zona de quien mira |
   | [concurrencia](../../rules/concurrencia.md) | la barrera de una exportación por persona |
   | [persistencia-ef](../../rules/persistencia-ef.md) y [guardado](../../rules/guardado.md) | los índices y los ajustes del tablero |
   | [datos-personales](../../rules/datos-personales.md) y [logs](../../rules/logs.md) | los ajustes se van con la baja; ningún valor ni filtro a los logs |
   | [api-http](../../rules/api-http.md), [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | rutas, filtros inválidos y errores |
   | [textos-y-traducciones](../../rules/textos-y-traducciones.md) | los encabezados fijos en el idioma de quien mira; las preguntas, como se escribieron (35) |
   | [tests](../../rules/tests.md) | qué lleva test y dónde va |

4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto,** solo para leer:
   - en [`../2026-10-17-formularios-traspaso/`](../2026-10-17-formularios-traspaso/README.md): anexo C, «Etapa 3» y §3.5, y anexo B, «Etapa 3»;
   - en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`: anexo C §5 (G-01 a G-08), §6 (L-01 a L-04), C1-05 y C2-07, y anexo D (`MF1`, `DC0`, `DC1` ajustado, `CA5`, `CA6`, `CE5`, `CE6` y `DOC7-A`).
6. **La maqueta,** en `../MaquetaMultitenant/src/areas/business/forms/`: `pages/FormsOverviewPage.tsx`, `FormRecordsPage.tsx` y `RecordFullPage.tsx`, `components/records/` y `lib/recordFilters.ts` y `values.ts`. Se copia lo que se ve, sin lo de etapas posteriores: fotos, aprobación, PDF, corregir y CSV. Sus rutas no son el contrato: manda esta spec.

## Qué se construye

**La lista «Formularios» (79, `MF1`):**
- con `formrecords.read`, Preguntas y Último cambio dan lugar a tres columnas, un dato en cada una: Respuestas (de `LastRecordNumber`), Última respuesta (su número, que la abre) y Recibida. Sin valores, en una sola consulta paginada. Sin ese permiso, la lista sigue como en la E14;
- quien ve respuestas y no tiene `forms.read` usa otra ruta, porque `[HasPermission]` pide un solo permiso. Ve solo los formularios publicados, con el nombre de su versión activa y nunca el del borrador, sin «En WhatsApp»;
- el nombre abre «Respuestas» si hay una versión publicada y se pueden ver; si no, «Diseño».

**Las pestañas:** «Respuestas», con su cantidad, y «Tablero» van antes de «Diseño» (79). Se ven con `formrecords.read` y una versión publicada.

**La planilla:**
- columnas fijas: N°, Recibida, Cargó, Canal y Empresas. Cargó es un empleado o un cliente, con la marca «Cliente» (49). Canal dice «WhatsApp». Empresas son las de quien respondió (24): un cliente no tiene;
- una columna por pregunta, unidas por `FieldId` entre versiones: primero el orden de la versión activa y después las retiradas;
- «—» si la pregunta no existía en esa versión, y «Sin respuesta» si quedó vacía. Cero y `false` se distinguen;
- **«elegir varias»** muestra las opciones en el orden en que se eligieron, separadas por coma;
- **los campos sellados** van en subcolumnas después de su pregunta, «pregunta · campo». Solo se muestran: no se ordenan, ni filtran, ni cuentan. «Columnas» las prende o las apaga;
- se ordena por N°, Recibida y cada pregunta, menos «elegir varias». Una opción ordena por su texto sellado. Cargó no se ordena, porque el nombre no vive en Forms.

**Los nombres:** Forms guarda ids. El de un empleado y sus empresas salen del lector de miembros; el de un cliente, de `IStructuredContactSource`, por lote y una vez por página. Un miembro que se fue conserva el nombre; sin dato, «Persona sin información»; un cliente sin WhatsApp o con el contacto borrado dice «Cliente». Forms nunca lee tablas de WhatsApp.

**Filtros:**
- condiciones unidas con «y», con la gramática de la E12 (C5): texto `contains` y `equals`; número y fecha `equals`, `gte` y `lte`; elegir una y sí/no `in`; **elegir varias `anyOf`**; y en todos, `answered`, `unanswered` y `notAsked`;
- `unanswered` incluye la opcional vacía y la pregunta que no existía; `notAsked` distingue la segunda. Qué versiones tienen cada pregunta se decide en memoria, con las versiones selladas, y la consulta lo usa;
- además: persona, versión, empresa (también «Sin empresa») y recibida, en UTC con fin exclusivo;
- la búsqueda mira solo los textos largos;
- **lo privado no sale de la memoria:** textos, personas y condiciones viajan en el cuerpo, nunca en la dirección, el storage ni los logs;
- filtrar por una opción retirada conserva la historia.

**Una sola lectura:** cada pedido fija las versiones que existen, las deserializa una vez y consulta solo esas: una publicación a mitad no mezcla columnas. Listado, conteos, navegación, Excel y tablero usan la misma función de filtros.

**Conteos:** cada dimensión ignora solo su propio filtro y conserva las opciones históricas, con la última etiqueta. En «elegir varias», una respuesta cuenta en cada opción. Se piden solo las secciones visibles, y su falla no impide aplicar un filtro válido.

**Excel (`.xlsx`):**
- con los filtros aplicados y `formrecords.export`, hasta 10.000 respuestas (con más, el diálogo pide acotar) y sin trabajos de fondo;
- una consulta de ids ordenados, hasta 10.001, e hidratación en lotes de 100, con un presupuesto de 30 s;
- una exportación a la vez por persona, con la barrera que no espera (C11). Comparte la barrera con la carga de catálogos: un trabajo pesado por persona;
- números, fechas y sí/no van como tipos de Excel, y los textos, literales. **Un número de más de 15 dígitos significativos va como texto,** porque Excel lo redondearía;
- una opcional vacía es una celda vacía; una pregunta que no existía, «—»;
- **«elegir varias»** va en una celda, unida con «; ». En un catálogo tipado se suma la columna «pregunta · valor», con los valores canónicos;
- **todas las preguntas** trae todos los campos sellados, una columna por campo; **solo las visibles** sigue a «Columnas»;
- la hoja se llama «Respuestas»: el nombre del formulario no siempre entra en una hoja. El archivo es `<formulario>-<aaaa-mm-dd>.xlsx`, en la zona de quien exporta.

**El tablero (57, `DOC7-A`):** la pestaña «Tablero», armada sola desde los tipos de las preguntas de la versión activa, sin nada escrito en el código:
- arriba, «Respuestas» y, de las tres primeras preguntas de número, el total, el promedio, el mínimo y el máximo;
- abajo, una tarjeta por gráfico: cuántas llegaron por día, semana o mes, con los días vacíos en tono suave; el total de cada número por período; el reparto de cada pregunta de elegir o de sí/no; las fechas por período, y quién cargó. Las opciones y las personas muestran seis y «Otras»;
- usa los filtros de la planilla, y todo se calcula en SQL en la zona de quien mira, hasta los últimos 366 períodos;
- cada tarjeta se ve como tabla o se quita, y las de opciones y personas cuentan cantidad o el total de un número. «Agregar gráfico» devuelve lo quitado, y «Volver al automático» lo deja como al principio;
- lo quitado y qué cuenta cada tarjeta se guardan por persona y formulario. Los filtros, no.

**El detalle:**
- **la vista rápida,** al costado de la planilla: el número, ↑ ↓ (también con el teclado), «Abrir en pantalla completa», ✕ y Esc. Muestra la versión, «Abrir conversación», quién la respondió y quién se la mandó, y la pestaña Respuesta, con las preguntas de su versión y los campos sellados de cada opción;
- **la pantalla completa,** en `/org/formularios/:id/respuestas/:recordId/completa`: «Respuesta» y «Actividad» a todo el ancho y plegables. Arriba, el número, ↑ ↓ y «Abrir conversación». Actividad cuenta quién la mandó y quién la respondió. El formulario queda en las migas;
- **ubicar una respuesta:** la posición, la anterior y la siguiente salen de una sola consulta con ventanas de SQL, con el filtro y el orden de la planilla. Lo ausente, lo excluido y lo ajeno dan el mismo 404;
- **la dirección compartible** lleva solo el orden, las versiones y las columnas. Una entrada directa arranca sin filtros privados. «Volver» regresa a la misma fila, página, filtros y orden;
- **«Abrir conversación»** aparece solo si quien mira puede atender esa bandeja, y no manda nada al abrir.

## Pantallas

Se dibujaron antes de las reglas del 25 y el 26/10, y Diseño los revisa con esta etapa, con el [texto de la etapa](15-texto-diseno.md). El recorrido guiado suma los circuitos `CA6` y `CE6`. Muestran solo lo que existe hasta acá (78).

| Tablero | Qué muestra | Estado |
| --- | --- | --- |
| `FRM-Formularios-15` | la lista, con las columnas de respuestas (`MF1`) y «Quien solo ve respuestas» | A revisar |
| `FRM-Respuestas` | la planilla, con «Columnas», «Empresa» y sus estados; suma `CA5` y `CE5` | A revisar |
| `FRM-Respuestas-Filtros` | «Más filtros», en un panel al costado | A revisar |
| `FRM-Respuesta` | la vista rápida | A revisar |
| `FRM-Respuestas-Exportar` | el diálogo de exportar | A revisar |
| `FRM-Tablero` | el tablero (`DOC7-A`) | A revisar |
| `M-FRM-Respuestas`, `M-FRM-Respuesta`, `M-FRM-Tablero` | los tres, en el teléfono. Tocar una fila abre `M-FRM-Respuesta`: no hay vista rápida | A revisar |
| `FRM-Respuesta-Completa` | la pantalla completa (`DC0` y `DC1` ajustado), con sus dos secciones plegadas y abiertas | Por dibujar |
| `M-FRM-Formularios-15` | la lista en el teléfono, con «N respuestas · estado» | Por dibujar |

**Lo que se corrige en lo dibujado:**
- **Sale lo de etapas posteriores:** las columnas Fotos y Remito, la pestaña Fotos y su visor, su sección en «Más filtros», «Por aprobar · 3» en el panel y la tarjeta «Por estado».
- **Cambia:** Canal pasa a su columna (un dato por celda) y «Abrir en pantalla completa» se suma a la vista rápida. «Contando…» y «Exportando…» quedan grisados con su mismo texto (43).

**Estados que faltan dibujar:**
- un cliente con la marca «Cliente», y «Persona sin información»;
- `CA5`: «Fallas» con dos opciones, el filtro «alguna de» y su lugar en «Más filtros»;
- `CE5`: las subcolumnas con «—» en una respuesta de antes de los campos, y los campos en el detalle;
- con `forms.read` y sin `formrecords.read`: la lista sin las columnas de respuestas.

**Mandan sobre esta spec en:**
- **Las fichas del front:** un dato por celda, en un renglón; fechas completas, con hora, en la zona de quien mira; sin rótulos que expliquen. Un texto que no entra se corta, con el completo en su tooltip.
- **La navegación (34 y 79):** el menú dice «Gestión de formularios», y su panel tiene Formularios y Catálogos, sin «Respuestas».
- **La planilla** se desplaza a lo ancho dentro de su caja, con N° fijo a la izquierda. Es la tabla con columnas fijas de la E12.
- **«Ya tenés una exportación en curso»** es la barrera. **«No pudimos actualizar»** conserva lo que se ve.
- **Exportar** no aparece sin `formrecords.export`, y sin respuestas queda grisado, con el motivo en su tooltip (43).

## Contrato

Todas las rutas son de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Los `POST` de esta tabla solo leen: no llevan `[Idempotent]`. Van en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

`filtros` es `{search?, companies?[], respondents?[{kind, id}], versions?[], received?{preset? | fromUtc, toUtc}, conditions?[{fieldId, operator, values[]}]}`. `kind` es `member` o `contact`; `preset`, `today`, `last7Days` o `thisMonth`; `companies` admite `none`. Hasta 20 condiciones y 100 valores por lista; `search` hasta 100 caracteres.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /` (cambia) | `forms.read` | suma `sort=lastRecordAtUtc` | cada fila suma `recordCount?` y `lastRecord?{id, displayNumber, submittedAtUtc}`: `null` sin `formrecords.read` |
| `GET /overview` | `formrecords.read` | `page`, `pageSize`, `search`, `sort` (`name`, `lastRecordAtUtc`) | `PagedResult<{id, name, prefix, recordCount, lastRecord?}>` |
| `GET /{id}/records/columns` | `formrecords.read` | — | `{formName, prefix, activeVersionNumber, columns[{key, fieldId, label, type, retired, decimals?, catalogField?{key, name}}], versions[], searchLabels[]}` |
| `POST /{id}/records/search` | `formrecords.read` | `filtros`, `page`, `pageSize`, `sort` (`submittedAtUtc`, `number`, `field:<id>`) | `PagedResult<{id, displayNumber, versionNumber, submittedAtUtc, channel, respondent{kind, id?, name?}, companies[], values}>` |
| `POST /{id}/records/facets` | `formrecords.read` | `filtros`, `dimensions[]` (`company`, `respondent`, `version`, `received`, `field:<id>`) | `{total, dimensions[{key, options[{value, label, count, retired}], hasMore, answered?, unanswered?, notAsked?}]}`, hasta 20 opciones por dimensión |
| `POST /{id}/records/facet-options` | `formrecords.read` | `filtros`, `dimension`, `search?`, `page`, `pageSize` | `PagedResult<{value, label, count, retired}>` |
| `GET /{id}/records/{recordId}` | `formrecords.read` | — | `{id, displayNumber, versionNumber, submittedAtUtc, channel, respondent, companies[], formName, questions[], titles[], values, sentBy?{name, atUtc}, conversationId?}` |
| `POST /{id}/records/{recordId}/navigation` | `formrecords.read` | `filtros`, `sort` | `{previousId?, nextId?, position, total}` |
| `POST /{id}/records/export` | `formrecords.export` | `filtros`, `sort`, `columns` (`all`, `visible`), `visibleKeys?[]` | 200 `.xlsx` |
| `POST /{id}/records/dashboard` | `formrecords.read` | `filtros`, `period` (`day`, `week`, `month`) | `{period, total, buckets[{start, count}], numbers[{fieldId, label, decimals, sum, average?, min?, max?, buckets[{start, total}]}], choices[{fieldId, label, type, catalogName?, answered, options[], others?}], dates[{fieldId, label, buckets[]}], respondents[], respondentsOthers?}`; cada opción o persona lleva `{label, count, sums}` |
| `GET /{id}/dashboard-settings` | `formrecords.read` | — | `{hiddenCards[], countBy}` |
| `PUT /{id}/dashboard-settings` | `formrecords.read` | `{hiddenCards[], countBy}`, hasta 100 claves | 204. Sin versión: es de una sola persona |

**Valores:** `values` es un mapa de `fieldId` a valor. Ausente es que la pregunta no existía; `null`, «Sin respuesta». El valor es `{kind: "text", text}`, `{kind: "number", number}`, con el número como texto, `{kind: "date", date}`, `{kind: "bool", bool}` o `{kind: "choices", choices[{id, label, code?, detail?, value?, catalogName?, fields}]}`, en el orden elegido.

**Errores** (en `Domain/Forms/`, con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Record.NotFound` | NotFound | ausente, ajena, excluida o de otro formulario |
| `Forms.RecordFilter.Invalid` | Validation (`conditions[i]`) | pregunta, operador o valor fuera de la gramática, un orden que no se admite o un rango invertido |
| `Forms.RecordExport.TooMany` | Conflict | más de 10.000 respuestas |
| `Forms.RecordExport.Busy` | Conflict | esa persona ya tiene un trabajo en curso |
| `Forms.RecordExport.TimedOut` | ServiceUnavailable | se pasó el presupuesto de 30 s |
| `Forms.Dashboard.SettingsInvalid` | Validation (`hiddenCards`) | más de 100 claves |

Lo ajeno sigue siendo `Forms.Definition.NotFound`. Un 403 o un permiso revocado no devuelven datos ni conteos.

**Permiso nuevo:** `formrecords.export`, con sus textos y la migración de los roles de sistema.

## Datos

Una migración, `<fecha>_FormRecordsRead.cs`, y otra de roles, `<fecha>_SystemRoleFormRecordsExport.cs`. Las respuestas de antes aparecen tal cual, sin migrar.

**FormDashboardSettings** (`ITenantOwned`, esquema `tenant`, con RLS, sin auditoría ni versión):

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `UserId`, `DefinitionId` | uuid | clave; FK compuesta a `FormDefinitions`; `UserId` sin FK, porque la cuenta es global |
| `Settings` | jsonb | `{hiddenCards[], countBy}`; una clave que ya no existe se ignora al leer |
| `UpdatedAtUtc` | timestamptz | — |

**Índices nuevos:**
- `FormRecords`: `(TenantId, DefinitionId, SubmittedAtUtc, Id)`; `(TenantId, DefinitionId, RespondentUserId)`; `(TenantId, DefinitionId, RespondentContactId)`, y GIN sobre `RespondentCompanyIds`;
- `FormRecordValues`: `(TenantId, FieldId, ChoiceId)`, `(TenantId, FieldId, NumberValue)`, `(TenantId, FieldId, DateValue)`, y un GIN trigram `IX_FormRecordValues_Search_TextValue`, parcial en `LongText`, sobre `f_unaccent(lower("TextValue"))`.

`FormWorkLeases.Kind` suma `RecordExport`.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front; `M/`, la maqueta, y `[E12]` y `[E14]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | La condición, sus operadores por tipo, los errores y el permiso | `D/Forms/FormRecordCondition.cs`, `FormRecordFilterErrors.cs`, `FormRecordExportErrors.cs`; suma a `Permissions.cs`, `Permissions{,.en}.resx` y `FormRecordErrors.cs` | `D/Forms/FormCatalogErrors.cs` [E12] | operadores por tipo; `PermissionTextsTests` |
| B2 | Tabla, índices y migraciones | `I/Modules/Forms/Persistence/Configurations/FormDashboardSettingsConfiguration.cs`; las dos migraciones | `AssistantOrganizationSettingsConfiguration.cs`, `20261010203618_SystemRoleAssistantPermissions.cs` | aislamiento; `SearchIndexTests`; hacia atrás |
| B3 | Las columnas de todas las versiones, deserializadas una vez | `A/Modules/Forms/Services/FormRecordColumnsBuilder.cs` | `FormCatalogExportBuilder.cs` [E12] | orden activo y retiradas; subcolumnas; publicación concurrente |
| B4 | Filtros y orden en una sola función, con la planilla | `A/Modules/Forms/Interfaces/Persistence/IFormRecordReader.cs`, `I/Modules/Forms/Persistence/Readers/FormRecordReader.cs`, `FormRecordQueryFilters.cs`; `A/Modules/Forms/Services/FormRecordFiltersPolicy.cs`, `FormRecordQueryService.cs` | `I/Persistence/Readers/CompanyReader.cs`, `FormCatalogFiltersPolicy.cs` [E12] | la tabla de verdad de `unanswered` y `notAsked`; cero y `false`; `anyOf`; opción retirada; orden por opción; una segunda página |
| B5 | Nombres por lote | suma a `IMemberDirectoryReader.cs`, `MemberDirectoryReader.cs`, `IStructuredContactSource.cs`, su apagado y el de WhatsApp | esos mismos [E12] | un solo pedido por página; ex miembro, sin dato y sin WhatsApp; `IdentityAccessTests` |
| B6 | Conteos | suma a `FormRecordReader.cs` y `FormRecordQueryService.cs` | `CompanyReader.cs` | cada dimensión ignora la suya; históricas en cero; una respuesta en dos opciones |
| B7 | Detalle, origen y navegación | `A/Interfaces/Integrations/StructuredData/IStructuredRecordOriginSource.cs`, su modelo, `I/StructuredData/DisabledStructuredRecordOriginSource.cs`, `A/Modules/WhatsApp/Services/WhatsAppStructuredRecordOriginSource.cs`; suma al reader | `IStructuredContactSource.cs` [E12] | sin permiso de la bandeja no hay `conversationId`; sin WhatsApp; `ROW_NUMBER`, `LAG` y `LEAD`; los tres 404 iguales |
| B8 | Excel | `A/Modules/Forms/Interfaces/Integrations/IFormRecordSpreadsheet.cs`, `I/Modules/Forms/FormRecordSpreadsheet.cs`, `A/Modules/Forms/Services/FormRecordExportService.cs`, `FormRecordSheetBuilder.cs` | `IWhatsAppContactSpreadsheet.cs`, `WhatsAppContactSpreadsheet.cs` | 10.000 y 10.001; `Busy` entre dos pedidos; presupuesto; tipos de celda; 16 dígitos como texto; un texto con `=`; «; »; bytes abiertos con ClosedXML |
| B9 | Tablero y ajustes | `A/Modules/Forms/Services/FormDashboardService.cs`, `FormDashboardBuilder.cs`; repositorio de los ajustes; suma a `FormsAccountDeletionParticipant.cs` y a su cleanup | `AssistantAccountDeletionParticipant.cs` | agrupar en otra zona; días vacíos; seis y «Otras»; sumas sin redondeo; la baja de la cuenta borra los ajustes |
| B10 | La lista | suma a `IFormReader.cs` y `FormReader.cs` [E14] | esos mismos | sin consultas por fila; `overview` con el nombre de la versión activa; borrador cambiado |
| B11 | Rutas y ficha | `P/Modules/Forms/Controllers/BusinessFormRecordsController.cs`, `FormRecordFileResults.cs`, `Contracts/*`, `FormsContractContributor.cs`; suma la variante a `paginado-y-busqueda.md`; suma las relaciones a `GrowingCollectionContractTests` | `BusinessAssistantController.cs`, `WhatsAppFileResults.cs` | cada ruta con 401, 403, feature apagada = 404, 400 y paginado; `ToString` sin filtros; los filtros privados nunca por query; ejemplos exportados |
| B12 | Medir el volumen | prueba de volumen del módulo | la de la E12 | EXPLAIN sobre el SQL real de EF con 10.000 respuestas |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` | `M/…/api/records.ts` | zod estricto con los ejemplos |
| F2 | La lista y las pestañas | suma a `FormsPage.tsx` y a las pestañas [E14]; `registration.ts` | `M/…/pages/FormsOverviewPage.tsx` | las tres combinaciones de permisos; adónde abre el nombre |
| F3 | La planilla | `pages/FormRecordsPage.tsx`, `lib/recordFilters.ts`, `lib/values.ts`, `components/records/CustomerTag.tsx` | `M/…` | «—», «Sin respuesta», cero y `false`; lo privado fuera de la URL; 403 retira todo; axe |
| F4 | «Más filtros» | `components/records/MoreFiltersPanel.tsx` | `M/…` | borrador hasta aplicar; un conteo que falla no frena |
| F5 | La vista rápida | `components/records/QuickView.tsx`, `RecordValues.tsx`; la excepción en la ficha de tablas | `M/…`, sin Workflow | teclado, Esc y foco útil; una respuesta tardía se descarta |
| F6 | La pantalla completa | `pages/RecordFullPage.tsx` | `M/…`, sin Workflow | entrada directa sin filtros privados; «Volver» a la misma fila; el teléfono |
| F7 | Exportar | `components/records/ExportDialog.tsx` | `M/…`, sin CSV | más de 10.000; `Busy`; botón según la 43 |
| F8 | El tablero | `components/records/FormDashboard.tsx`, sin biblioteca de gráficos | `M/…`, sin estados ni montos | quitar, agregar, contar y volver al automático; ver como tabla; axe |

**Al cerrar:** `docs/features/formularios.md` suma la sección de las respuestas.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | `FormRecordValues`, `FormVersions.Questions` y `ChoiceFields`. | Tal como dicen la §3 y T22: filas tipadas y el texto sellado también en las opciones propias. |
| [E14] | Los nombres de la lista, las pestañas y el participante de la baja. | Los de la columna «Archivos». La cuenta dada de baja deja `RespondentUserId` en `null`. |
| [E14] | La sesión de WhatsApp a partir del `SourceKey`. | `whatsapp:<sessionId>` llega a la conversación, a quien mandó y a cuándo. |
| [E12] | La barrera `IFormWorkLease` y `IMemberDirectoryReader`. | La barrera admite otro `Kind`; el lector suma nombres por id, también de quien ya no es miembro, y empresas. |
| [E11] | `/org/whatsapp/chats/:conversationId`. | Abre las dos bandejas, y no hace falta decir cuál. |
| [E9] | Las cuotas. | Las rutas cuentan en las generales, sin perfil propio. |
| Base de la 12 | La tabla con columnas fijas, el panel al costado y las pestañas del front. | Están en el front, o entran copiados de la maqueta. |
| EF | Agrupar en SQL en otra zona horaria. | Se puede con la versión de Npgsql del repo, sin SQL a mano. |

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **El Excel no repite la lectura por página:** permisos, columnas y orden se leen una vez. Se prueba con 250 filas y con el borde de 10.000.
2. **Los esquemas no viajan por fila** ni se deserializan por fila.
3. **Un SQL por combinación:** ordenar por una opción usa el texto sellado, sin un `CASE` armado por versión.
4. **Traer todos los ids para tres vecinos:** la navegación es una ventana de SQL.
5. **El EXPLAIN corre sobre el SQL real de EF,** no sobre uno escrito a mano.
6. **Un 404 antes de revisar el permiso:** el permiso se comprueba primero, y un 403 retira de la pantalla lo que había.

## Decisiones tomadas (no se vuelven a preguntar)

- **La planilla pagina por páginas y con total,** aunque las respuestas solo crezcan: es un listado de negocio, con filtros y conteos, no un historial como la auditoría o los mensajes, que paginan por cursor. Lo sostienen sus índices, y se prueba con volumen.
- **La navegación** (34), con una sola lista «Formularios» y pestañas por formulario (79).
- **Sin migrar:** las respuestas de las etapas anteriores aparecen tal cual.
- **Exportar:** `.xlsx`, con permiso propio, hasta 10.000 respuestas y sin trabajos de fondo.
- **«Columnas»** vive en la URL y no se guarda en el servidor; los ajustes del tablero, sí.
- **El tablero no compara** con el período anterior, porque el tablero aprobado no lo tiene.
- **Cargó no se ordena:** el nombre vive en la cuenta o en WhatsApp, y Forms no los lee.

## Fuera de esta etapa

- Importar respuestas desde Excel, exportar los archivos, exportar en segundo plano y los permisos por formulario.
- Lo que suman las etapas siguientes: el papel «Filtro» de un campo (E16); las fotos, los documentos y «Descargar ▾» (E18); las líneas, los cálculos, «no se mostró» y los montos por moneda del tablero (E19); «Cargar», «Agregar respuesta», el canal «App», su filtro y lo propio de quien solo carga (E20); y el estado, «Por aprobar», la aprobación, la tarjeta «Por estado», el PDF, los avisos, corregir y anular (E21).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`. No hace falta una prueba nueva con Meta: lee lo que ya llegó. El recorrido prueba:
1. respuestas reales de la E14, con un catálogo con campos, «elegir varias» y una opcional vacía, en la lista, la planilla, los filtros, la vista rápida, la pantalla completa y el tablero;
2. un cliente con su nombre y «Abrir conversación»;
3. un enlace a una respuesta abierto en otra pestaña, sin la búsqueda ni los filtros privados;
4. el `.xlsx` abierto en Excel, sin reparación;
5. EXPLAIN sobre unas 10.000 respuestas, en la lista, la planilla, los conteos, la navegación, el Excel y el tablero.

## Requisitos previos

La E14 cerrada.

## En el multitenant

- **Datos de la organización:** todo se lee dentro de RLS. `FormDashboardSettings` es la única tabla nueva.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** suma `formrecords.export`; `formrecords.read` es de la E14.
- **Sin WhatsApp:** Cargó dice «Cliente» y no hay «Abrir conversación».
- **Cuotas:** las rutas cuentan en las cuotas generales.
