# E13 · Campos de los catálogos · plan ejecutable

**Estado:** pendiente · Va después de los arreglos de la revisión externa con el código listo ([hoja de ruta](2026-09-27-plan-de-desarrollo.md)) (decisión del usuario del 2026-11-27). Spec: `docs/plans/formularios/13-campos.md`. Tableros: los de la v175, salvo los que la spec marca «Se redibujan con el traspaso 18, tanda 2»: la tarea que usa uno de esos espera la versión del lienzo que lo trae, y donde un tablero contradiga la 261 a la 264, manda la decisión. No queda nada de producto para decidir.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K1 | Pendiente |
| Back | B1–B10 | Pendiente |
| Front | F1–F6 | Pendiente |

## Forma de trabajo

Modo rápido ([etapa en paralelo](../guides/etapa-en-paralelo.md) y [ADR 0071](../decisions/0071-conductor-en-carriles.md)): contrato primero; después los carriles del back y el front a la vez; la revisión en paralelo, con los tests enteros y los detectores; los arreglos; la última revisión de código y el cierre. No hay puerta por etapa: el E2E real, la galería y el recorrido van en la verificación de la etapa (abajo), sobre una copia de su corte, mientras se programa la siguiente. Un arreglo corre solo lo que toca. La tabla de estado la actualiza el cierre del conductor ([conducir una etapa](../guides/conducir-una-etapa.md)). No hay prueba con Meta. Requisito: la E12 cerrada.

**Reglas de cada chat:** todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con `git commit -- <rutas>`, en español y conventional, con la línea `Tarea: 13-campos/<id>`; con la app levantada, nadie edita el `src` del front; TDD donde hay lógica. Cada tarea corre su test dirigido y el build (en el front, `tsc -b` y el lint de lo que tocó), con `node scripts/arneses/verificar.mjs` en el back y `node scripts/verificar.mjs` en el front; los tests enteros, los de integración y los de seguridad corren una vez, en la revisión de la etapa. El contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md)): una vez, en la revisión de la etapa, un revisor por cada tema de riesgo de las tareas **(R)**, que recibe el código y los escenarios sin la conclusión de quien programó.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [E12] El `PUT` y su delta | Lo deja la E12 (C3, su B3): `items` lleva solo lo cambiado, y sin cambios escribe solo la fila del catálogo | K0 |
| [E12] Nombres de helpers y páginas | El plan de la E12 los fija; `CatalogPreviewPhone.tsx` nace allá, aunque la spec lo da como nuevo en la F4 | K0; F4 lo suma |
| [E12] `IFormCatalogSpreadsheet` | Lee columnas por encabezado y con la cultura del pedido (C9) | K0 |
| [E12] La barrera | Una por persona, dos minutos, sin esperar | K0; cubre las columnas de campos |
| [E12] El bloqueo | `LockAsync(ids)` usa `AcquireAdvisoryLocksAsync`, que ordena y deduplica las claves: dos catálogos se bloquean siempre en el mismo orden | nada |
| Base de la 12: botones | El aro, el gerundio y el motivo de lo apagado: los confirmó la K0 de la E12 | nada |
| [E12] Columnas fijas | `EditableTable` y `pinned` de `table.tsx` | K0 |
| jsonb y GIN | `AssistantMessageConfiguration` y `AssistantDurableOperationConfiguration` usan `jsonb` y `HasMethod("gin")`; nadie usa `jsonb_path_ops` | B2 suma `HasOperators("jsonb_path_ops")` |
| «Columnas» | El front no la tiene | `F/src/shared/ui/ColumnsMenu.tsx`, nueva (F3) |
| Formato | `DisplayFormatter` existe | arma el «Sí», el número y la fecha de la presentación |
| Tableros | «Sí, quitar el campo y sus 1.097 valores»; «1.097 opciones tienen un valor que no es un número entero.»; «1 opción supera los 30 caracteres del título.»; el combo dice «Zonas · 6 opciones» también cerrado | los cuerpos de abajo |

## Contrato

### Rutas

Son las de la E12, con su acceso, permisos, cuotas y errores. Sin rutas nuevas. Lo que suma cada una:

| Ruta | Permiso | Qué suma | Errores nuevos |
| --- | --- | --- | --- |
| GET `{id}` | `formcatalogs.read` | `fields[]`, `presentation` y `presentationIssues` | — |
| GET `{id}/items` | `formcatalogs.read` | `fields` y `presentationIssue?` en cada opción; filtro `ids`, repetido, hasta 100 y sin vacíos | 400 en `ids` |
| POST `/` | `formcatalogs.manage` | `fields?[]` y `presentation?`, solo a mano | 400: `TooManyFields`, `FieldKeyDuplicate`, `FieldInvalid`, `PresentationInvalid` |
| PUT `{id}` | `formcatalogs.manage` | `fields?[]`, `presentation?`, `items[i].fields`, `confirmedFieldRemovals?[]` y `sort.column` con el `id` de un campo activo guardado (224); responde `fieldIdentityMap` | 400: todos los `Field*` y `PresentationInvalid`; 409 `SourceInUse` |
| POST `{id}/imports/previews` y `{id}/imports` | `formcatalogs.manage` | las columnas de los campos | por fila y columna: `FieldValueInvalid`, `FieldValueRequired`, `FieldValueDuplicate` |
| GET `{id}/export` | `formcatalogs.read` | una columna por campo activo (119) | — |
| GET `template` | `formcatalogs.read` | `catalogId?`, con las columnas de sus campos | 404 `NotFound` |

### Cuerpos

Valen los de la sección «Contrato» de la spec, con los valores en JSON como dice ahí. Cambios que piden los tableros:
- **Cada campo** del detalle suma `valuesCount` (las opciones con valor, para «sus 1.097 valores»). El combo cerrado dice solo el catálogo, con `sourceCatalogName`, y la cantidad de opciones va abierto, debajo de cada nombre, con `optionsCount` del listado de la E12 (262): el detalle no suma la cantidad.
- **`presentationIssues`** del detalle: `{count, examples}`, con hasta tres `{itemId, text, issue}` de las opciones activas, en orden de posición.
- **`FieldTypeNotConvertible` y `FieldValuesIncompatible`** suman `invalidCount` a `invalidRows`. Cada fila es `{position, itemId, text, reason}`.
- **`PUT`** responde `{version, itemIdentityMap, fieldIdentityMap}`.

### Errores

En `Domain/Forms/FormCatalogErrors.cs`, con sus textos en `FormsTexts.resx` y `.en.resx`. Solo `General.ConcurrencyConflict` pone el editor en conflicto: `SourceInUse` es un error que se corrige (lección 2).

| Código | HTTP y campo | es | en |
| --- | --- | --- | --- |
| `Forms.Catalog.TooManyFields` | 400, `fields` | Llegó a 20 campos, el máximo de un catálogo. | It reached 20 fields, the maximum for a catalog. |
| `Forms.Catalog.FieldKeyDuplicate` | 400, `fields[i].key` | Ya la usa «{name}». | "{name}" already uses it. |
| `Forms.Catalog.FieldInvalid` | 400, `fields[i]` | Revisá el tipo, las reglas o el catálogo de este campo. | Check this field's type, rules or catalog. |
| `Forms.Catalog.FieldRemovalNotConfirmed` | 400, `fields[i]` | Confirmá que se quitan sus {count} valores. | Confirm that its {count} values are removed. |
| `Forms.Catalog.FieldTypeNotConvertible` | 400, `fields[i].type` | {count} opciones tienen un valor que no es {type}. | {count} options have a value that isn't {type}. |
| `Forms.Catalog.FieldValuesIncompatible` | 400, `fields[i]` | {count} opciones no cumplen la regla nueva. | {count} options don't meet the new rule. |
| `Forms.Catalog.FieldValuesMissing` | 400, `fields[i].required` | {count} opciones activas no tienen valor. | {count} active options have no value. |
| `Forms.Catalog.FieldValueInvalid` | 400, `items[i].fields.<id>` | el de su tipo, como en la E12, o «Puede tener hasta {n} caracteres.» o «Tiene que estar entre {min} y {max}.» | the one for its type, as in E12, or "It can have up to {n} characters." or "It must be between {min} and {max}." |
| `Forms.Catalog.FieldValueRequired` | 400, `items[i].fields.<id>` | Completá este dato. | Fill in this value. |
| `Forms.Catalog.FieldValueDuplicate` | 400, `items[i].fields.<id>` | Ya lo tiene la opción {n}. | Option {n} already has it. |
| `Forms.Catalog.SourceInUse` | 409 | Lo usan campos de otros catálogos. Quitalos antes de darlo de baja. | Fields in other catalogs use it. Remove them before deactivating it. |
| `Forms.Catalog.PresentationInvalid` | 400, `presentation` | Revisá qué va en el título y en la descripción. | Check what goes in the title and the description. |

`ItemInUse` suma `uses: options`: «No se puede borrar: la usa otra opción. Podés darla de baja.» («It can't be deleted: another option uses it. You can deactivate it.»). Los motivos de `invalidRows` (`required`, `range`, `length` e `invalid`) y de `presentationIssue` («El título queda vacío.», «No entra en el título de WhatsApp.», «No entra en la descripción de WhatsApp.») van en `enums.json` del front.

### Decisiones técnicas del contrato

- **Bloqueos:** guardar bloquea el catálogo y las fuentes de sus campos, y dar de baja bloquea el catálogo, en una sola llamada a `LockAsync(ids)`, que ordena las claves.
- **Claves:** el JSON del contrato usa la identidad del campo; `FieldValuesJson` guarda por clave, y el servicio traduce. Una clave guardada no cambia: otra clave en un campo existente es `FieldInvalid`, igual que un campo nuevo con «Obligatorio».
- **Único:** el texto se compara sin distinguir mayúsculas y sin espacios en los extremos, como el código.
- **En uso:** una opción la usa otra cuando un campo activo de otro catálogo la tiene como valor (`FieldValuesJson @>`, con el índice GIN). `SourceInUse` mira los campos activos con `FieldsJson @>`, sin índice: son pocos catálogos por organización.
- **Presentación:** si quitar un campo deja el título vacío, vuelve a `text`. `presentationIssues` se calcula por lotes de 500 opciones activas; `valuesCount`, con un solo `GROUP BY` por catálogo.
- **El Excel y el teléfono** formatean el «Sí», el número y la fecha con `DisplayFormatter`, en la cultura del pedido.

### K0 · Lo que dejó la E12

El chat del contrato confirma esta lista al arrancar. Lo que difiera se anota en «Cambios al contrato» antes de programar:
1. **Back:** `FormCatalog`, `FormCatalogItem`, `FormCatalogErrors`, `FormsTextLimits` y `UniqueKeys`; `FormCatalogService`, `FormCatalogImportService`, `FormCatalogItemEditor`, `FormCatalogFiltersPolicy`, `FormCatalogPositionGuard`, `FormCatalogFileResolver`, `FormCatalogImportPolicy` y `FormCatalogExportBuilder`; `IFormCatalogRepository.LockAsync(ids)`, `IFormCatalogReader`, `IFormWorkLease` con `FormCatalogFileSlotAttribute` e `IFormCatalogSpreadsheet`; el `PUT` con `itemIdentityMap` y `sort` (224), el guardado sin cambios que escribe solo la fila del catálogo y `ItemInUse` con `uses`; `FormsContractContributor`, los ejemplos de `docs/contracts/examples/forms.json` con su guarda, la prueba de volumen del módulo y `FormCatalogActionSource` con `catalogoptions.edit@1` y su escenario.
2. **Front:** `api/catalogs.ts`, `CatalogEditorPage.tsx`, `useCatalogEditor.ts`, `OptionsSection.tsx`, `OptionsDialog.tsx`, `SortDialog.tsx`, `ImportDialog.tsx`, `CatalogPreviewPhone.tsx` y `CatalogConflictDialog.tsx`; `EditableTable`, `PasteGrid` y `pinned` de `table.tsx`; `parseDecimalText` en `shared/format/parsers.ts`; la validación de los ejemplos en `check-contracts.mjs`, y el adaptador de la propuesta en `CatalogEditorPage.tsx`.
3. **Segunda parte de la base** (214): el número ambiguo (207), el tooltip que se abre con un clic o un toque (211), el chip de solo lectura sin ✕ (213), la pieza `Picker`, también la múltiple (262), y el orden por columnas (264).

### K1 · Publicar el contrato

En el back, los `*HttpRequest`, las respuestas y los errores de arriba, con sus textos, en `FormsContractContributor.cs`, y `openapi.json` regenerado; las guardas de rutas en verde. En el front, `npm run contracts`.

**Texto para el chat de Contrato**

```text
Leé AGENTS.md, docs/plans/2026-11-08-etapa-13-campos.md y la spec 13-campos.md. Primero K0: confirmá la lista contra el código que dejó la E12. Después K1: los cuerpos y los errores de la sección «Contrato», con textos es y en, en FormsContractContributor. Generá openapi.json y avisale a la Guía para el npm run contracts del front, que no tocás. Reglas: las de «Forma de trabajo» de este plan.
```

## Back

Abreviaturas de la spec: `D/`, `A/`, `I/` y `P/`, y `[E12]` es un archivo que deja esa etapa. Aplican `Result`, `OperationLog.RunAsync`, `[LoggerMessage]` y una UoW por método que escribe, con locks, lecturas, reglas y escrituras en ese orden. Los valores nunca van a los logs. Cada lección de «Lo que ya se pagó allá» tiene su test.

1. **B1 · Dominio.** `D/Forms/FormCatalogField.cs`, `FormCatalogFieldType.cs` y `FormCatalogPresentation.cs`; suma a `FormCatalog.cs` (`ChangeFields`, `ChangePresentation`), `FormCatalogItem.cs` (`SetFieldValues`) y `FormCatalogErrors.cs` [E12]. Copia: `FormCatalog.cs` [E12]. Tests en `Domain.UnitTests/Forms/`: la clave fija, reservada y repetida; 20 contando solo los activos (105); las reglas por tipo; un campo nuevo no nace obligatorio.
2. **B2 · Columnas y migración (R: datos).** `FormCatalogConfiguration.cs` y `FormCatalogItemConfiguration.cs` [E12]; `<fecha>_FormCatalogFields.cs`, aditiva, con el GIN `jsonb_path_ops` sobre `FieldValuesJson` y un `Down` que se niega si hay campos o valores. Copia: `AssistantMessageConfiguration.cs` (jsonb) y `AssistantDurableOperationConfiguration.cs` (GIN). Tests: aislamiento, la migración de ida y vuelta y un alta de la versión anterior, que toma los valores por defecto de las columnas nuevas (0060).
3. **B3 · El valor de un campo.** `A/Modules/Forms/Services/FormCatalogFieldValuePolicy.cs`, con la cultura del pedido. Copia: `FormCatalogFiltersPolicy.cs` [E12]. Tests: «1.500», «12.345», `""`, `null` y una fuente dada de baja, en es y en (lección 3).
4. **B4 · Guardar campos, presentación y valores (R: datos).** Suma a `FormCatalogService.cs` [E12]; `FormCatalogFieldEditor.cs`. Copia: `FormCatalogItemEditor.cs` [E12]. Tests: el delta, con ausente que conserva y `null` que limpia; sin cambios no toca ni audita opciones; una opción entre 10.000 escribe y audita solo esa; el 409; dos personas a la vez sobre un catálogo y su fuente; ordenar por un campo de cada tipo, con lo vacío al final (224).
5. **B5 · Cambiar el tipo o las reglas.** `A/Modules/Forms/Services/FormCatalogFieldChangePolicy.cs`, que valida todas las opciones por lotes; un error revierte todo. Copia: `FormCatalogImportPolicy.cs` [E12]. Tests: las conversiones, a texto con el texto de la opción fuente o «Sí» y «No»; `invalidRows` con las primeras 50 e `invalidCount`; obligatorio y único sobre las activas.
6. **B6 · Quitar un campo y proteger una fuente.** Suma a `FormCatalogFieldEditor.cs` y `FormCatalogService.cs`; `ItemInUse` suma `uses: options`. Copia: `FormCatalogPositionGuard.cs` [E12]. Tests: quitar con valores pide confirmar y los conserva; `SourceInUse`; una opción fuente dada de baja no bloquea guardar, importar ni exportar (lección 1); borrar una que usa otra opción no borra ninguna.
7. **B7 · Pegar, importar, exportar y la plantilla (R: archivos).** Suma a `FormCatalogFileResolver.cs`, `FormCatalogImportPolicy.cs`, `FormCatalogExportBuilder.cs` y `FormCatalogImportService.cs` [E12]. Tests: exportar y reimportar sin diferencias, con número, fecha, Sí/No y fuente; dos opciones con el mismo texto; encabezado por nombre o por clave; columna ausente conserva y vacía limpia; una fuente por identidad, código o texto único; una celda que nombra la opción guardada dada de baja la conserva.
8. **B8 · La presentación.** `A/Modules/Forms/Services/FormCatalogPresentationBuilder.cs`: título y descripción de cada opción, `presentationIssue` y `presentationIssues`. Copia: `FormCatalogExportBuilder.cs` [E12]. Tests: 30 y 300 caracteres; contenido vacío; un campo quitado sale de la presentación; los tres ejemplos.
9. **B9 · Rutas, contratos y ejemplos.** Suma a `BusinessFormCatalogsController.cs`, `Contracts/*HttpRequest.cs` y `FormsContractContributor.cs` [E12]. Tests: cada ruta con 401, 403, feature apagada, validación, 409 y paginado; un código por cada 409 (lección 2); `ids` con más de 100 o vacíos da 400; una fuente de otra organización se rechaza como inexistente; los ejemplos de cada respuesta y error nuevos en `forms.json`, que pide la guarda de la E12. Suma a `ReadTargetFieldsAsync` de `FormCatalogActionSource.cs` [E12] los campos activos del catálogo destino, con el tipo y los límites de la spec («Acciones del asistente»), y a su escenario, un caso con un campo. Como la acción es `FieldsFromTarget`, su declaración en `assistant-actions.json` no cambia y sigue en `@1`.
10. **B10 · Volumen.** Suma a la prueba de volumen de la E12: 10.000 opciones por 20 campos al guardar, importar y exportar, y el detalle con `presentationIssues`, con `EXPLAIN`.

**Texto para el chat del Back**

```text
Leé AGENTS.md, docs/plans/2026-11-08-etapa-13-campos.md, la spec 13-campos.md y las fichas de su «Qué leer». Con K1 publicado, hacé B1 a B10 en orden, contra ese contrato, sumando a las piezas de la E12 en lugar de crear otras; nada de ArquitecturaBase. La revisión adversarial de las tareas (R) va después, en la revisión de la etapa. Cada lección de «Lo que ya se pagó allá» lleva su test. No toques el front ni la tabla de estado del plan. Reglas: las de «Forma de trabajo» de este plan.
```

## Front

Todo suma al módulo `src/modules/forms` de la E12, contra los tipos generados. Cada pantalla copia su tablero (la v175 o el que dice «Estado»). Tests con MSW y axe. Los estados dibujados de la spec tienen cada uno su test.

1. **F1 · Tipos y llamadas.** Suma a `api/catalogs.ts` [E12]: los campos, la presentación, `ids` y `catalogId` de la plantilla. Test: los ejemplos nuevos de `forms.json` pasan `contracts:check`, estricto y con zod (E12).
2. **F2 · El modal «Campos de …» y el combo.** `components/catalog/CatalogFieldsDialog.tsx` y `CatalogCombo.tsx`, el combo de la 111 sobre el listado de la E12 (`source=manual`, `status=active` y `search`), sin nada propio de los campos (lección 5). Tableros: `FRM-Catalogo-Campos` y `M-FRM-Catalogo-Campos`. Copia: `OptionsDialog.tsx` [E12] y `Picker` (262). Tests: agregar, subir, bajar y quitar; la lista con el número, el nombre y el tipo en sus columnas (263); una copia hasta «Listo», que avisa si hay un campo nuevo; el tope de 20 con su motivo; la clave y su candado; quitar con valores pide marcar la casilla; el combo abre con los primeros, busca adentro de la lista y cerrado dice solo el catálogo (262); el celular. Suma a `SortDialog.tsx` [E12] los campos activos guardados, con el sentido de cada tipo (224; tableros `FRM-Catalogo-Ordenar-13` y su teléfono); quitar el campo elegido cancela el orden pendiente.
3. **F3 · Una columna por campo.** Suma a `OptionsSection.tsx` y `OptionsDialog.tsx` [E12], con el control de cada tipo; «Columnas» en `F/src/shared/ui/ColumnsMenu.tsx`, nueva, sobre la pieza múltiple `Picker` (262), con lo elegido en la URL. Tableros: `FRM-Catalogo-13`, `M-FRM-Catalogo-13`, `FRM-Catalogo-Opciones-13` y su teléfono. El número de un campo se lee con `parseDecimalText` [E12]. Tests: la columna y la fila grisadas hasta guardar, con su motivo; «Reactivar» en el ⋮ de la fila (261); el valor que apunta a una opción dada de baja; «1.500» en un número da `numberAmbiguous` (207), y uno de 18 dígitos llega entero; «No se puede borrar: la usa otra opción.»; cambiar el tipo muestra los errores del servidor por fila, sin frenar antes de mandar (lección 4).
4. **F4 · «En WhatsApp».** `components/catalog/PresentationEditor.tsx`, con sus chips y «Por ejemplo», y suma a `CatalogPreviewPhone.tsx` [E12]. Copia: `FilterChip.tsx` (base de la 12), el chip con ✕, y la pieza `Picker` (262) para «Agregar campo». Tablero: `FRM-Catalogo-13`. Tests: cada pieza lista lo que falta; con un solo lugar en el título, su ✕ apagado; se arma como el servidor; un título que no entra, con la cantidad y hasta tres ejemplos.
5. **F5 · El editor.** Suma a `CatalogEditorPage.tsx` y `useCatalogEditor.ts` [E12], también al adaptador de la propuesta, que dibuja la columna de cada campo. Tests: una propuesta con un campo se ve entera; `SourceInUse` deja el editor editable (lección 2); solo `formcatalogs.read` ve «Campos» y la tabla de lectura; los textos en es y en.
6. **F6 · Importar y la plantilla.** Suma a `ImportDialog.tsx` y `PasteGrid.tsx` [E12]: la grilla trae una columna por campo activo, en el orden de la plantilla, y reconoce su encabezado por el nombre o por la clave, así no la marca como ajena; «Descargar la plantilla» pide `template` con `catalogId`. Tablero: `FRM-Catalogo-Importar`, con una columna más por campo. Tests: pegar con y sin encabezados de campos; un encabezado por la clave; una columna que no es de ningún campo sigue en rojo; la plantilla pedida con `catalogId`.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-13-campos.md y la spec 13-campos.md, con las decisiones 261 a 264 y 269 de la hoja de ruta. Con K1 publicado, corré npm run contracts y hacé F1 a F6 sumando a las piezas de la E12, contra los tipos generados y copiando los tableros de cada tarea, de la v175 o los que dice «Estado»; si a uno le falta un estado, seguí y avisale a la Guía. No toques el lienzo, el back ni la tabla de estado del plan, ni escribas tipos generados. Reglas: las de «Forma de trabajo» de este plan.
```

## Revisión y cierre

Sin tareas: los hace el conductor ([conducir una etapa](../guides/conducir-una-etapa.md)). La lista de control de la revisión suma los riesgos de la [prueba integral de ArquitecturaBase](../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase): el tope del nombre, la clave y el largo de un campo es el mismo en la pantalla y en el servidor, y al llegar se ve (O13); la plantilla y la exportación nombran igual las columnas de los campos (O14); la revisión de otra persona lista los cambios de los campos y de las opciones antes de «Usar la revisión actual» (O15). Al cerrar, `docs/features/formularios.md` suma la sección de los campos, y cada carpeta nueva lleva su `AGENTS.md`.

## Verificación de la etapa

Lo que recorre la verificación de esta etapa, el segundo hito, después del código listo ([conducir una etapa](../guides/conducir-una-etapa.md)). Cuando el cierre retira el plan, esta sección pasa a `docs/backlog.md`, a «Verificación de la etapa 13-campos».

- **E2E real:** los casos de «Cómo se cierra» de la spec, sumados a `F/scripts/test-e2e-real-forms.mjs` [E12].
- **Recorrido:** `campos-de-un-catalogo`, sumado a `F/scripts/recorrido-usuario-forms.mjs` [E12].
- **Galería** (`F/scripts/capturas/etapa-13.json`), a 1440, 390 y 320: los ocho tableros de la etapa y el diálogo de importar con las columnas de los campos al lado de `FRM-Catalogo-Importar`.

## Relación con otras etapas

### Usa de las anteriores

- **E12:** la lista de K0, las rutas de catálogos, `FormsTexts`, `tenant.FormCatalogs` y `FormCatalogItems`, y los scripts `test-e2e-real-forms.mjs` y `recorrido-usuario-forms.mjs`.
- **Base de la 12:** los botones, la (i), lo apagado con su motivo y `ConfirmDialog` con `pending`.
- **Núcleo:** `DisplayFormatter`, `AcquireAdvisoryLocksAsync` y, en el front, `Picker`.

### Deja para las siguientes

- **Datos:** `FormCatalogField`, `FormCatalogFieldType` y `FormCatalogPresentation`; las columnas `FieldsJson` y `Presentation` de `FormCatalogs`, y `FieldValuesJson` de `FormCatalogItems`, con su GIN `jsonb_path_ops`.
- **Helpers:** `FormCatalogFieldEditor`, `FormCatalogFieldValuePolicy`, `FormCatalogFieldChangePolicy` y `FormCatalogPresentationBuilder`.
- **Contrato:** `fields[]` con su `id` y su `key`, `presentation` con los lugares `text`, `code`, `value`, `detail` y `field:<id>`, `fieldIdentityMap`, el filtro `ids` de las opciones, `catalogId` de la plantilla, `uses: options` de `ItemInUse` y los errores de arriba.
- **Front:** `CatalogFieldsDialog`, `CatalogCombo`, `PresentationEditor` y `ColumnsMenu`; las columnas de los campos en `ImportDialog` y `PasteGrid`.

## Cambios al contrato

**K0 (2026-11-28).** La lista de la E12 está entera en los dos repos; la segunda parte de la base vive en `parseDecimalText` con `NumberAmbiguousError` (207), `InformativeTooltip` (211), `FilterChip` sin ✕ (213), `Picker` con `multiple` (262) y el orden del back en cada listado (264). Lo que difiere:
- **`sort.column` del `PUT`** es hoy el enum `FormCatalogItemSortColumn`, con `Text` y `Code` así en el JSON: el `id` de un campo no entra. K1 lo pasa a un texto que acepta `Text`, `Code` o el `id` de un campo activo guardado, así la versión anterior del front sigue mandando lo mismo (0060).
- **`ItemInUse`** existe con `uses` (textos, sin enum en el contrato), pero en la E12 nada lo produce (`ReadRemovedAsync` de `FormCatalogItemEditor`): B6 es la primera. El front suma al texto genérico («No se puede borrar porque se usa. Podés darla de baja.») la etiqueta de `FormCatalogItemUse.<uso>` de `enums.json`, que todavía no existe; la frase de `uses: options` es entera, así que va en lugar del genérico, no sumada a él.
- **Los encabezados y la cultura de una importación** no los resuelve `IFormCatalogSpreadsheet`, que solo traslada celdas: los reconoce `FormCatalogFileResolver`, por encabezado en cualquier cultura habilitada y el texto con la del pedido. B7 suma ahí las columnas de los campos.
- **La barrera** (`IFormWorkLease`) tiene, además del lugar por persona, topes por organización y por instalación (`Forms:CatalogFiles:MaxPerOrganization` y `MaxPerInstallation`); `FormCatalogFileSlotAttribute` vive en `Api/Modules/Forms/`, y el `UniqueKeys` del módulo, en `A/Modules/Forms/Interfaces/Persistence/`.
- **El adaptador de la propuesta** (`catalogoptions.edit@1`) vive en `src/modules/forms/lib/proposal.ts` y `hooks/useCatalogProposal.ts`; `CatalogEditorPage.tsx` solo lo usa. F5 suma ahí la columna de cada campo.

**K1 (2026-11-28).** Publicado en `openapi.json` y en los ejemplos de `forms.json`, con las listas vacías y la presentación por defecto hasta que B1 a B9 las llenen. Lo que fijó:
- **Nombres:** `FormCatalogFieldType` (`Text`, `WholeNumber`, `DecimalNumber`, `Date`, `YesNo` y `Catalog`) y `FormCatalogPresentationIssue` en el dominio; en el contrato, `FormCatalogFieldDetail`, `FormCatalogPresentationDetail`, `FormCatalogPresentationIssues`, `FormCatalogFieldIdentity`, `FormCatalogFieldHttpInput` y `FormCatalogPresentationHttpInput`, para no chocar con las clases del dominio de B1.
- **Valores:** `fields` de una opción y su delta son un mapa del `id` del campo a texto o booleano; en el delta, `null` limpia.
- **`sort.column`** es texto: `Text`, `Code` (sin distinguir mayúsculas) o el `id` de un campo. El validador rechaza lo demás en `sort.column`; que el campo exista, esté activo y esté guardado lo comprueba B4 bajo el bloqueo.
- **Topes:** `fields` del detalle trae hasta 100, entre activos y quitados (`MaxStoredFields`, lo pide `GrowingCollectionContractTests`), y B1 lo hace cumplir con `TooManyFields`; cada lugar de la presentación, hasta 24; `ids`, hasta 100; `invalidRows`, hasta 50.
- **Textos con un dato** (`{name}`, `{count}`, `{type}`, `{n}`, `{min}`, `{max}`): el servicio los arma con la cultura del pedido y los pasa en `message` a la fábrica de `FormCatalogErrors`, como dice [result-y-errores](../rules/result-y-errores.md); `invalidCount` e `invalidRows` van en el problema, con su motivo en minúsculas.
- **La guarda de ejemplos** lista los errores nuevos y el 404 de `template` en `NotReachableYet`; la tarea que produce cada uno graba su ejemplo y lo saca de ahí.

## Para decidir

Nada.
