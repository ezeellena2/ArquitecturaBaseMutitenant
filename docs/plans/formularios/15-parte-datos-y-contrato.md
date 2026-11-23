# E15 · Respuestas: datos y contrato (spec, parte)

**Estado:** pendiente · parte de la [spec de la E15](15-respuestas.md), que dice qué leer y cómo se cierra.

## Cómo se lee una respuesta (263, 266 y 269)

La vista rápida y la pantalla completa leen la respuesta con la forma de su formulario, en la versión con que se cargó. El autor decide qué preguntas hay y en qué orden; la app decide la forma por el tipo de cada una (269). Esta etapa arma esa forma de solo lectura, con una tabla fija por tipo:

| Ocupa | Qué |
| --- | --- |
| Una columna | número, fecha, sí o no, texto corto de hasta 80, elegir una y elegir varias, con opciones propias o de un catálogo (262), y cada dato de una opción de catálogo |
| El renglón entero | texto largo y texto corto de más de 80 o sin largo máximo |

- **Zonas** (88): «Datos», con lo que va antes del primer título, si hay algo, y una por cada título de sección, con el título en su banda. Se pliegan y arrancan abiertas; plegada, la banda dice cuántas se respondieron, «3 de 5 respondidas».
- **La grilla** (87): tres columnas desde 46 rem de ancho de la zona, dos desde 30 y una por debajo. Salen del ancho de la zona, no del de la pantalla, medido con `ResizeObserver`, y los renglones se arman con las columnas en uso. Las preguntas van en el orden del autor, sin reordenar; la que no entra baja de renglón, con una línea entre renglones.
- **Cada pregunta es su control,** con su etiqueta arriba y lo respondido adentro, sin poder tocarlo: el recuadro gris claro con el valor en negro. Número y fecha son angostos, con los formatos del front (205); el sí o no y elegir son la pieza cerrada (262), con lo elegido por su nombre, y varias, separadas por coma, como en la planilla; el texto largo va entero (174). Una pregunta sin respuesta deja su control vacío.
- **Los datos de una opción de catálogo** (263): cada dato sellado es su campo, en el mismo renglón que su pregunta, con el nombre «Pregunta · Dato» («Proveedor · CUIT») y el valor en gris, porque sale del catálogo; con varias opciones, los valores de cada dato van separados por coma. Una pregunta va en el mismo renglón que sus datos: si no entran juntos, empieza otro. Lo que no aporta no va: un dato que ninguna opción elegida selló, como en una respuesta de antes del campo, ni el nombre del catálogo.
- **Nada debajo de otro dato** (263): debajo de un control solo puede ir lo que no es un dato de la respuesta. La línea gris de quién y cuándo va arriba, debajo del título.

## Nombres, filtros y conteos

**Los nombres:** Forms guarda ids. El de un empleado y sus empresas salen del lector de miembros; el de un cliente, de `IStructuredContactSource`, por lote y una vez por página. Un miembro que se fue conserva el nombre; sin dato, «Persona sin información»; un cliente sin WhatsApp dice «Cliente». Forms nunca lee tablas de WhatsApp.

**Filtros:**
- condiciones unidas con «y», con la gramática de la E12 (C5): texto `contains` y `equals`; número y fecha `equals`, `gte` y `lte`; elegir una y sí/no `in`; **elegir varias `anyOf`**; y en todos, `answered`, `unanswered` y `notAsked`;
- `unanswered` incluye la opcional vacía y la pregunta que no existía; `notAsked` distingue la segunda. Qué versiones tienen cada pregunta se decide en memoria, con las versiones selladas, y la consulta lo usa;
- **la planilla elige una opción por pregunta** (143) y no usa `answered`, `unanswered` ni `notAsked`, que el contrato trae igual (143);
- además: persona, versión, empresa (también «Sin empresa») y recibida, en UTC con fin exclusivo;
- la búsqueda mira solo los textos largos;
- **lo privado no sale de la memoria:** textos, personas y condiciones viajan en el cuerpo, nunca en la dirección, el storage ni los logs;
- filtrar por una opción retirada conserva la historia.

**Una sola lectura:** cada pedido fija las versiones que existen, las deserializa una vez y consulta solo esas: una publicación a mitad no mezcla columnas. Listado, conteos, navegación, Excel y tablero usan la misma función de filtros.

**Conteos:** las facetas ofrecen las opciones de cada filtro, y el front no muestra sus cantidades (143). Cada dimensión ignora solo su propio filtro y conserva las opciones históricas, con la última etiqueta. En «elegir varias», una respuesta cuenta en cada opción. Se piden solo las secciones visibles, y su falla no impide aplicar un filtro válido.

## Contrato

Todas las rutas son de `/api/organization/forms`, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`. Los `POST` de esta tabla solo leen: no llevan `[Idempotent]`. Van en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

`filtros` es `{search?, companies?[], respondents?[{kind, id}], versions?[], received?{preset? | fromUtc, toUtc}, conditions?[{fieldId, operator, values[]}]}`. `kind` es `member` o `contact`; `preset`, `today`, `last7Days` o `thisMonth`; `companies` admite `none`. Hasta 20 condiciones y 100 valores por lista; `search` hasta 100 caracteres.

`orden` (264) es `sort`, `sortFieldId?` y `sortCatalogFieldId?`. `sort` es `number`, `submittedAtUtc`, `respondentKind`, `channel`, `field` (una pregunta, con `sortFieldId`) o `catalogField` (un dato sellado, con los dos ids), y con `-` adelante va al revés (`-submittedAtUtc`, el orden sin elegir). La planilla, la navegación y el Excel lo usan igual.

| Ruta | Permiso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /` (cambia) | `forms.read` | suma a `sort` `recordCount`, `lastRecordNumber` y `lastRecordAtUtc` (264) | cada fila suma `recordCount?` y `lastRecord?{id, displayNumber, submittedAtUtc}`: `null` sin `formrecords.read` |
| `GET /overview` | `formrecords.read` | `page`, `pageSize`, `search`, `sort` (`name`, `recordCount`, `lastRecordNumber`, `lastRecordAtUtc`) | `PagedResult<{id, name, prefix, recordCount, lastRecord?}>` |
| `GET /{id}/records/columns` | `formrecords.read` | — | `{formName, prefix, activeVersionNumber, columns[{key, fieldId, label, type, retired, sortable, decimals?, catalogField?{id, key, name}}], versions[], searchLabels[]}` |
| `POST /{id}/records/search` | `formrecords.read` | `filtros`, `page`, `pageSize`, `orden` | `PagedResult<{id, displayNumber, versionNumber, submittedAtUtc, channel, respondent{kind, id?, name?}, companies[], values}>` |
| `POST /{id}/records/facets` | `formrecords.read` | `filtros`, `dimensions[]` (`company`, `respondent`, `version`, `received`, `field:<id>`) | `{total, dimensions[{key, options[{value, label, count, retired}], hasMore, answered?, unanswered?, notAsked?}]}`, hasta 20 opciones por dimensión |
| `POST /{id}/records/facet-options` | `formrecords.read` | `filtros`, `dimension`, `search?`, `page`, `pageSize` | `PagedResult<{value, label, count, retired}>` |
| `GET /{id}/records/{recordId}` | `formrecords.read` | — | `{id, displayNumber, versionNumber, submittedAtUtc, channel, respondent, companies[], formName, questions[], titles[], values, sentBy?{name, atUtc}, conversationId?}` |
| `POST /{id}/records/{recordId}/navigation` | `formrecords.read` | `filtros`, `orden` | `{previousId?, nextId?, position, total}` |
| `POST /{id}/records/export` | `formrecords.export` | `filtros`, `orden`, `columns` (`all`, `visible`), `visibleKeys?[]` | 200 `.xlsx` |
| `POST /{id}/records/dashboard` | `formrecords.read` | `filtros`, `period` (`day`, `week`, `month`) | `{period, total, buckets[{start, count}], numbers[{fieldId, label, decimals, sum, average?, min?, max?, buckets[{start, total}]}], choices[{fieldId, label, type, catalogName?, answered, options[], others?}], respondents[], respondentsOthers?}`; cada opción o persona lleva `{label, count, share, sums, sumShares}` |
| `GET /{id}/dashboard-settings` | `formrecords.read` | — | `{hiddenCards[], countBy}` |
| `PUT /{id}/dashboard-settings` | `formrecords.read` | `{hiddenCards[], countBy}`, hasta 100 claves entre los dos | 204. Sin versión: es de una sola persona |

`countBy` es un mapa de la clave de cada tarjeta de opciones o personas a lo que cuenta: `count` o `sum:<fieldId>`. Sin su clave, cuenta la cantidad.

**El orden** (264): `sortable` dice si una columna ordena. No ordenan Cargó (Forms no guarda el nombre), Empresas, «elegir varias» ni un dato sellado de varios valores; pedirlo es `Forms.RecordFilter.Invalid`. Se compara lo guardado: una opción, por su texto sellado; un dato sellado, con su tipo (un número como número); `respondentKind` y `channel`, por el orden de su enum. Lo vacío (la pregunta que no existía y la que quedó vacía) va al final en los dos sentidos, y el desempate es `-submittedAtUtc` y después `Id`. En la lista, un formulario sin respuestas va al final.

**La forma de la versión** (266): el detalle trae, en la misma lectura y con el mismo permiso, la forma de la versión con que se cargó la respuesta, no la de la activa. `questions[]` son sus preguntas en orden, `{fieldId, type, label, maxLength?, decimals?, catalogFields?[{id, key, name}]}`, y `titles[]`, sus títulos de sección. `catalogFields` son los datos de una pregunta de catálogo, los mismos de sus subcolumnas en la planilla, en su orden y con su nombre, que es uno solo (35).

**Porcentajes del tablero** («Sí 14 · 64%»): los calcula y redondea el back, y el front los muestra con `PercentText` (208), sin calcularlos ([numeros-y-moneda](../../rules/numeros-y-moneda.md)). `share` es la fracción de `count` sobre las respuestas que contestaron esa pregunta (en «Quién cargó», sobre el total), y `sumShares`, con las mismas claves que `sums`, la de cada suma sobre el total de esa pregunta de número. Van redondeadas a dos decimales, `AwayFromZero`, como el porcentaje entero del tablero; sin base, `null`.

**Claves y números del tablero:** las claves de tarjeta son `received`, `number:<fieldId>`, `choice:<fieldId>` y `respondents`. `buckets[].start` es la fecha civil (`yyyy-MM-dd`) del período en la zona de quien mira; las semanas van de lunes a domingo, con todos los períodos del rango, también los vacíos, hasta 366. `sum`, `average`, `min`, `max` y `sums` viajan como texto decimal canónico; el promedio se redondea a los decimales de la pregunta, `AwayFromZero`. El `PUT` de los ajustes reemplaza todo, sin versión.

**El Excel:** `Content-Disposition` adjunto con `<formulario>-<aaaa-mm-dd>.xlsx` en UTF-8, `no-store` y `nosniff`. La hoja («Respuestas», en inglés «Responses») y los encabezados fijos (N°, Recibida, Cargó, Canal, Empresas) salen de los recursos, en el idioma del pedido. Cargó lleva el nombre, como la planilla, y si alguna fila exportada es de un cliente, los encabezados fijos suman «Es cliente», con «Sí» o «—» (206).

**Valores:** `values` es un mapa de `fieldId` a valor. Ausente es que la pregunta no existía; `null`, «Sin respuesta». El valor es `{kind: "text", text}`, `{kind: "number", number}`, con el número como texto, `{kind: "date", date}`, `{kind: "bool", bool}` o `{kind: "choices", choices[{id, label, code?, detail?, value?, catalogName?, fields}]}`, en el orden elegido.

**Errores** (en `Domain/Forms/`, con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Record.NotFound` | NotFound | ausente, ajena, excluida o de otro formulario |
| `Forms.RecordFilter.Invalid` | Validation (`conditions[i]`) | pregunta, operador o valor fuera de la gramática, un orden que no se admite o un rango invertido |
| `Forms.RecordExport.TooMany` | Conflict | más de 10.000 respuestas |
| `Forms.RecordExport.Busy` | Conflict | esa persona ya tiene una exportación de respuestas en curso |
| `Forms.RecordExport.TimedOut` | ServiceUnavailable | se pasó el presupuesto de 30 s |
| `Forms.Dashboard.SettingsInvalid` | Validation (`hiddenCards` o `countBy`) | más de 100 claves entre los dos, o algo que no es `count` ni `sum:<fieldId>` |

Lo ajeno sigue siendo `Forms.Definition.NotFound`. Un 403 o un permiso revocado no devuelven datos ni conteos.

**Permiso nuevo:** `formrecords.export`, con sus textos y la migración de los roles de sistema.

## Datos

Una migración, `<fecha>_FormRecordsRead.cs`, y otra de roles, `<fecha>_SystemRoleFormRecordsExport.cs`. Las respuestas de antes aparecen tal cual, sin migrar. La clave nueva de `FormWorkLeases`, `Records` y `RecordExport` se agregan y se escriben en el mismo despliegue: es la excepción de la [0060](../../decisions/0060-dos-versiones-conviven.md), anotada en la migración, porque todavía no hay despliegue (la E10 va después del bloque, 123) y volver a la imagen anterior no aplica.

**FormDashboardSettings** (`ITenantOwned`, esquema `tenant`, con RLS, sin auditoría ni versión):

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `UserId`, `DefinitionId` | uuid | clave; FK compuesta a `FormDefinitions`; `UserId` sin FK, porque la cuenta es global |
| `Settings` | jsonb | `{hiddenCards[], countBy{}}`; una clave que ya no existe se ignora al leer |
| `UpdatedAtUtc` | timestamptz | — |

**Índices nuevos:**
- `FormRecords`: `(TenantId, DefinitionId, SubmittedAtUtc, Id)`; `(TenantId, DefinitionId, RespondentUserId)`; `(TenantId, DefinitionId, RespondentContactId)`, y GIN sobre `RespondentCompanyIds`;
- `FormRecordValues`: `(TenantId, FieldId, ChoiceId)`, `(TenantId, FieldId, NumberValue)`, `(TenantId, FieldId, DateValue)`, y un GIN trigram `IX_FormRecordValues_Search_TextValue`, parcial en `LongText`, sobre `f_unaccent(lower("TextValue"))`.

`FormWorkLeases` suma `Lane` a su clave, que pasa a ser `(TenantId, UserId, Lane)`: `Catalogs` para la vista previa, aplicar y exportar de la E12, que siguen de a uno por persona, y `Records` para exportar respuestas, con `Kind` `RecordExport`. Lo que ya existe queda en `Catalogs`. Exportar respuestas cuenta también en los topes por organización y de la instalación de la E12 (C11): una planilla pesada no frena a las demás organizaciones.

**FormRecordCatalogFields** (`ITenantOwned`, esquema `tenant`, con RLS; F15 de ArquitecturaBase): `(DefinitionId, FieldId, CatalogFieldId)`, única, con los campos de catálogo que selló alguna respuesta de cada pregunta. Las subcolumnas «pregunta · campo» salen de acá y no de recorrer las respuestas, porque los campos sellados dependen de la captura (E14) y no solo de la versión. La escribe la captura, en su UoW y sin repetir, nunca una lectura; la planilla la lee por su índice. La misma migración la llena con las respuestas que ya existen.

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **El Excel no repite la lectura por página:** permisos, columnas y orden se leen una vez. Se prueba con 250 filas y con el borde de 10.000.
2. **Los esquemas no viajan por fila** ni se deserializan por fila.
3. **Un SQL por combinación:** ordenar por una opción usa el texto sellado, sin un `CASE` armado por versión.
4. **Traer todos los ids para tres vecinos:** la navegación es una ventana de SQL.
5. **El EXPLAIN corre sobre el SQL real de EF,** no sobre uno escrito a mano.
6. **Un 404 antes de revisar el permiso:** el permiso se comprueba primero, y un 403 retira de la pantalla lo que había.
7. **Lo elegido se nombra aunque no esté en la página.** Allá, el filtro de una pregunta de catálogo decía «Valor no disponible», porque la columna no trae sus opciones: el nombre de lo elegido viaja con el cambio y la planilla lo recuerda, como `knownItems` en el front.
