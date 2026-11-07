# E15 · Respuestas: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E15](15-respuestas.md), que dice qué leer y cómo se cierra.

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

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **El Excel no repite la lectura por página:** permisos, columnas y orden se leen una vez. Se prueba con 250 filas y con el borde de 10.000.
2. **Los esquemas no viajan por fila** ni se deserializan por fila.
3. **Un SQL por combinación:** ordenar por una opción usa el texto sellado, sin un `CASE` armado por versión.
4. **Traer todos los ids para tres vecinos:** la navegación es una ventana de SQL.
5. **El EXPLAIN corre sobre el SQL real de EF,** no sobre uno escrito a mano.
6. **Un 404 antes de revisar el permiso:** el permiso se comprueba primero, y un 403 retira de la pantalla lo que había.
7. **Lo elegido se nombra aunque no esté en la página.** Allá, el filtro de una pregunta de catálogo decía «Valor no disponible», porque la columna no trae sus opciones: el nombre de lo elegido viaja con el cambio y la planilla lo recuerda, como `knownItems` en el front.
