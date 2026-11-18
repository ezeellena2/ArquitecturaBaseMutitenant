# E19 · Condiciones, cálculos y líneas: contrato, datos y pasos (spec, parte)

**Estado:** pendiente · parte de la [spec de la E19](19-condiciones-calculos-y-lineas.md), que dice qué leer y cómo se cierra.

## Contrato

**Sin rutas nuevas.** Son las de la E14 y la E15, que suman lo siguiente. Van en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

| Ruta | Qué suma |
| --- | --- |
| `GET /{id}` y `PUT /{id}/draft` | tipos `Calculation` y `Rows`. `questions[]` suma `visibleIf?[]`, con `{fieldId, operator, values[]}`; `calculation?`, con `{tokens[], currency?, decimals}`, y `rows?`, con `{minRows, maxRows}`; las preguntas del bloque llevan `rowsOf`, el `fieldId` del bloque, y una de catálogo, sus `recipientFilters[]`, como una suelta (168). Los campos nuevos se omiten cuando no aplican, así lo sellado y sus huellas no cambian |
| `POST /{id}/publication-checks` | suma los errores de abajo y `componentCount`; `changes[]` suma `kind` `movedToBlock`, que es «Pasan al bloque»; `warnings[]` suma `RowsOverLimit`, con `limit` y `max`, y los avisos de la E16 en las preguntas de catálogo del bloque |
| `GET /{id}/records/columns` | tipos nuevos, y `currency?`. La columna del bloque suma `questions[]`, las columnas de sus preguntas, que no tienen columna propia |
| `POST …/records/search` y el detalle | los valores de abajo. `sort` admite `field:<id>` de un cálculo, no del bloque ni de sus preguntas |
| `POST …/records/search` y `facets` | las preguntas del bloque filtran por algún ítem: un `EXISTS` sobre sus valores, sin multiplicar la respuesta. Varias del mismo bloque van en el mismo ítem, también en los conteos; `answered`, `unanswered` y `notAsked` miran la respuesta entera |
| `POST …/records/export` | `rowsPer`, `item` (por defecto) o `record`: una sola hoja, con una fila por ítem o por respuesta (227); una celda «No se mostró». Las filas se cuentan antes de escribir, y más de 100.000 es `Forms.RecordExport.TooManyRows`: las copias atienden a todas las organizaciones, y allá un millón de filas tardó 84 s y 2,8 GB, y cien mil, 11 s y 0,4 GB |
| `POST …/records/dashboard` | `numbers[]` suma los cálculos y `currency?` |
| `GET …/conversations/{id}/forms` | `optionCounts` y `offerProblems` cuentan también las preguntas de catálogo del bloque, con sus filtros por destinatario |

**Un dato de la fórmula** es `{kind: "number", value}`, `{kind: "question", fieldId}`, `{kind: "catalogField", fieldId, catalogFieldId}` o `{kind: "rowSum", fieldId}`, con el `fieldId` de una pregunta numérica del bloque, también un cálculo. Las otras piezas son `{kind: "op", op}`, `{kind: "open"}` y `{kind: "close"}`. **El alcance** (233): un cálculo del bloque, con `rowsOf`, usa números fijos y preguntas y campos del catálogo de su ítem, sin `rowSum`, y se calcula en cada ítem; uno de afuera usa una pregunta del bloque solo dentro de un `rowSum`.

`catalogFieldId` es la identidad del campo, la misma del contrato de la E13: la clave queda adentro. **Lo que usa un campo o un catálogo cuenta:** `Forms.Catalog.FieldInUse` y «Lo usan» suman las fórmulas y las preguntas de catálogo del bloque, así quitar un campo no deja los totales en «Sin respuesta» sin avisar.

**Valores nuevos,** en el mapa de la E15:
- `{kind: "hidden"}`: no se mostró;
- `{kind: "money", amount, currency}`, para un cálculo con moneda, con el monto como en `Money`;
- `{kind: "rows", count, rows?[{position, values}]}`: la planilla muestra `count` («3 ítems», 206), y `rows` va solo en el detalle, con todos los ítems y todas las preguntas del bloque de su versión, en orden y `null` en las vacías, también en un ítem vacío.

**Puertos:** `StructuredFormVersion.Questions` suma `VisibleIf`, `Calculation` y `Rows`. `StructuredRecordCapture` suma `Rows[BlockFieldId, Items]`: un arreglo por ítem, en orden, con sus `[FieldId, Text?|Number?|Date?|Bool?|Choice?]` y la opción como en la E14; un ítem vacío es `[]`, como allá. **Una pregunta de catálogo del bloque tiene su oferta,** con sus filtros por destinatario (168) y el mismo tope que una suelta, y todos los ítems usan la misma lista. `StructuredRecordCard` suma los ítems y los cálculos. `IStructuredFormChannel.Check` valida el perfil nuevo: el `routing_model`, el mínimo y el máximo contra el tope de 10, los 64 KiB y cada condición, con las reglas de abajo.

**Cómo se arma el Flow** (lo que Meta acepta; allá, traspaso 12 §4 y `docs/features/whatsapp.md`: el perfil de la pantalla de cierre, probado el 9/11, y el de las condiciones dentro de un `If`, el 11/11, cuyos números no son los de acá). Es la regla, no código para copiar (18):
- **Una comparación por `If`.** El validador de Meta rechaza `&&` y `||` entre dos comparaciones, aun con paréntesis. Un valor de sí/no o de elegir una es `==` (en un catálogo, contra el id local del valor, que viaja en `data`, y `-` si no se ofreció); una fecha, `==`; `answered`, `!= ''`, y `unanswered`, `== ''`. Desde otra pantalla se lee `${screen.<pantalla>.form.<campo>}`.
- **Varios valores** de una pregunta con opciones propias leen una marca, `<clave>_shown`, que cada opción fija con `update_data` (`true` en las que la muestran). Arranca en `false`, vive desde la pantalla de la pregunta que la fija hasta la última que la lee, y cada `navigate` la pasa. Las opciones de un catálogo llegan con el mensaje y no pueden fijarla.
- **«y» son `If` anidados,** uno por condición y sin `else`. La pregunta cuya condición mira otra que también tiene condición va dentro de los `If` de aquella, así se oculta en cadena (B1). Meta anida hasta tres: la cadena entera, con las condiciones de las preguntas de las que depende, no pasa de tres.
- **Un control va en un solo `If`,** nunca en dos ramas. Un título va dentro de los `If` de sus preguntas si todas tienen la misma condición; si no, va afuera.
- **Una pantalla que manda no navega en ninguna rama.** Allá, con el bloque al final, la pantalla del ítem mandaba con «No» y navegaba con «Sí», y Meta lo rechazó. Por eso, cuando lo que sigue es mandar (el «No» y el último ítem de un bloque al final, o la rama que saltea una pregunta de archivo que era la última), la rama va a la **pantalla de cierre** (204): el nombre del formulario, el texto de abajo y el botón final, que manda todo lo visitado. Ninguna pantalla de ítem manda. Sin eso, no hay pantalla de cierre y el Flow no cambia.
- **Lo que dijo Meta se lee, no se adivina.** Un `validation_errors` que no cae en ninguna pregunta es `WhatsApp.Flow.ValidationUnlocated`, sin «Ir a la pregunta». La verificación lee `GET /{flow-id}?fields=status,json_version,validation_errors` y `GET /{flow-id}/assets`, con el token de la organización.

**Errores** (con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Draft.ConditionInvalid` | Validation (`questions[i].visibleIf[j]`) | más de 10, una pregunta que no está antes o no sirve, un operador o valor fuera de la gramática, o una opción de catálogo que no existe |
| `Forms.Draft.FormulaInvalid` | Validation (`questions[i].calculation`) | vacía, mal armada, un dato que no es numérico o no está antes, uno fuera de su alcance, montos de dos monedas, o una moneda fuera de los datos de referencia |
| `Forms.Draft.RowsInvalid` | Validation (`questions[i].rows`) | un mínimo o un máximo fuera de rango, un segundo bloque, un tipo que no entra o un catálogo que no se puede usar |
| `Forms.Draft.RowsEmpty` | Validation (`questions[i]`) | publicar un bloque sin preguntas |
| `WhatsApp.Flow.ConditionNotSupported` | Validation (`questions[i].visibleIf[j]`) | el canal no la puede mostrar: un número, elegir varias, varios valores de una pregunta de catálogo, más de tres `If` en cadena, `unanswered` sobre una pregunta que también tiene condición (oculta cuenta como sin responder, y eso pediría un «o»), o un archivo que no se puede saltear |
| `WhatsApp.Flow.RowsMinimumOverLimit` | Validation (`questions[i].rows`) | un mínimo de más de 10, con `limit` y `minimum` (178) |
| `Forms.Record.RowsInvalid` | Validation | menos ítems que el mínimo, más que el máximo o que el tope de WhatsApp, o un ítem sin una obligatoria |
| `WhatsApp.Flow.ValidationUnlocated` | Failure (en `channel`, como `ValidationFailed` de la E14) | Meta rechazó el Flow con errores que no caen en ninguna pregunta |
| `Forms.RecordExport.TooManyRows` | Conflict, como `TooMany` de la E15 | el Excel tendría más de 100.000 filas |

**Una opción que se borra mientras una condición la nombra** (como allá): `Forms.Catalog.ItemInUse` suma a `uses: forms` las condiciones del borrador y de la versión activa, y guardar un catálogo con opciones por borrar toma antes el bloqueo de las definiciones de la organización, en el orden de la E16.

`WhatsApp.Flow.TooManyComponents` y `ResponseTooLarge` (E14) cuentan cada pantalla de ítem. Al capturar, una respuesta con un valor en una pregunta oculta no da error: el valor se descarta. Una clave de ítem que no se puede leer (un `_r` de una pregunta de afuera del bloque o de un ítem que no pregunta, o «¿Agregás otro ítem?» sin sí ni no) es `Forms.Record.InvalidResponse`, que no consume número.

## Datos

Una migración aditiva, `<fecha>_FormLogic.cs`. Lo de una pregunta vive en el jsonb del borrador y de la versión, sin columnas nuevas.

| Tabla | Columna | Tipo | Regla |
| --- | --- | --- | --- |
| `FormRecords` | `HiddenFieldIds` | uuid[], vacío por defecto | las preguntas que no se mostraron |
| `FormRecordValues` | `Currency` | texto 3? | en un cálculo con moneda; CHECK que lo ata a `NumberValue`. Se valida con `ValidCurrency()`, sin FK |
| `FormRecordValues` | `NumberValue`, en la fila del bloque | — | la cantidad de ítems, de 1 a 100, con `Type` `Rows`; sin ítems no hay fila, y es «Sin respuesta», como una opcional |

**FormRecordRowValues** (`ITenantOwned`, esquema `tenant`, con RLS, sin auditoría: es parte de la respuesta)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `RecordId`, `BlockFieldId`, `RowPosition`, `FieldId` | uuid y smallint | PK; FK compuesta a `FormRecords`. `RowPosition` va de 0 a la cantidad del bloque menos uno; un ítem vacío no tiene filas |
| `Type` | texto | el tipo de la pregunta |
| `TextValue`, `NumberValue`, `DateValue`, `BoolValue` | los de `FormRecordValues` | CHECK: exactamente un valor |
| `Currency` | texto 3? | el de un cálculo del ítem con moneda, como en `FormRecordValues` |
| `ChoiceId`, `ChoiceLabel`, `CatalogId`, `ChoiceCode`, `ChoiceDetail`, `ChoiceValue`, `ChoiceFields` | los de `FormRecordValues` | sellados, como en una pregunta suelta |

Una tabla aparte, y no una columna más en `FormRecordValues`: la clave de esa tabla cambiaría para todas las respuestas, y lo de la E15 lee esa fila por `FieldId` y `Position`. La misma opción de un catálogo puede elegirse en dos ítems: la clave lleva `RowPosition`.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E18]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Evaluar condiciones en memoria, con la regla de la E15 | `D/Forms/FormConditionEvaluator.cs`, junto a `FormRecordCondition.cs` [E15] | `FormRecordFiltersPolicy.cs` [E15] | cada operador; en cadena, y `unanswered` sobre una oculta se cumple; la tabla de verdad igual que en SQL |
| B2 | Calcular una fórmula | `D/Forms/FormFormula.cs` y `FormFormulaEvaluator.cs` | — | precedencia y paréntesis; división por cero; dato que falta; desborde; al par, 2,205 da 2,20; los decimales de la moneda; el cálculo de cada ítem y su suma afuera |
| B3 | Las preguntas en el borrador | suma a `FormQuestionType.cs`, `FormDraft.cs`, `FormDraftEditor.cs`, `FormVersionCreator.cs` y al control de `FieldInUse` [E14]; `A/Modules/Forms/Services/FormLogicPolicy.cs` | `FormRecipientFilterPolicy.cs` [E16] | cada motivo de los errores; referencia rota al quitar, mover y cambiar el tipo; guardar incompleto sigue andando; un campo que usa una fórmula no se quita; una pregunta de catálogo del bloque cuenta en «Lo usan» y guarda sus filtros por destinatario; pasar una pregunta al bloque y sacarla, con otra identidad; el alcance de un cálculo del bloque; una condición con una opción que no existe, y borrarla mientras se guarda, con dos actores |
| B4 | Publicar y el Flow con `If`, una pantalla por ítem y el `routing_model`, con un perfil nuevo | suma a `WhatsAppFlowBuilder.cs`, a `Check` y a `FormOfferBuilder.cs` [E14] | el mismo | la condición que no se puede mostrar; 50 componentes; 16 y 64 KiB; título oculto; lo sellado no se recompila; «¿Agregás otro ítem?» desde el mínimo; las dos ramas de cada `If` en el `routing_model`; una comparación por `If`, sin `&&` ni `||`, y hasta tres anidados; la marca de varios valores; la pantalla de cierre con el bloque al final, y ninguna pantalla que manda y navega; el aviso del máximo y el freno del mínimo de más de 10; el bloque sin preguntas frena; «Pasan al bloque»; la oferta y los avisos de un catálogo del bloque, con sus filtros por destinatario; un error de Meta sin pregunta |
| B5 | La captura: visibilidad, descarte, ítems, sellado y cálculo, en ese orden | `A/Modules/Forms/Services/FormRecordLogicPolicy.cs`; suma a `FormRecordService.cs`, `FormRecordWriter.cs` [E14] y `WhatsAppFlowReplyVerifier.cs` | esos mismos | un valor en una oculta se descarta; obligatoria oculta; la cantidad contra el mínimo, el máximo y el tope; un ítem vacío cuenta y se guarda, también el último; catálogo en un ítem; las claves `_r<n>`, sin claves de lo no visitado y sin lo de después del «No»; un ítem que no se lee queda «no se pudo guardar», nunca «no reconocida»; dos capturas iguales |
| B6 | Tabla, columnas y migración | `D/Forms/FormRecordRowValue.cs`; `I/…/Configurations/FormRecordRowValueConfiguration.cs`; la migración | `FormRecordValueConfiguration.cs` [E14] | aislamiento y migración, también hacia atrás; la cantidad en la fila del bloque |
| B7 | Lo que se lee: planilla, conteos, detalle y navegación | suma a `FormRecordColumnsBuilder.cs`, `FormRecordReader.cs`, `FormRecordQueryFilters.cs` [E15] | esos mismos | `notShown` y `unanswered`; un cálculo se filtra y se ordena; algún ítem con un `EXISTS`; varias del bloque en el mismo ítem, también en los conteos; la búsqueda en los ítems; ítems sin consultas por fila |
| B8 | Excel y tablero | suma a `FormRecordSheetBuilder.cs`, `FormRecordSpreadsheet.cs` y `FormDashboardBuilder.cs` [E15] | esos mismos | una fila por ítem, con los datos repetidos; «Ítem» vacío sin ítems y «—» sin el bloque; «Una por respuesta»; 100.000 y 100.001 filas, sin escribir; el monto como número con su moneda en el encabezado; el total en su moneda |
| B9 | La tarjeta del chat y «Mensajes» | suma a `FormRecordSource.cs` y a la copia de la tarjeta [E14] | esos mismos | un ítem por línea y el total; sin `formrecords.read`, solo el número |
| B10 | Contratos y ejemplos | suma a `Contracts/*`, `FormsContractContributor.cs` y los modelos de `StructuredData` | esos mismos [E14] | ejemplos exportados de los tipos nuevos, también la columna del bloque; copiar conserva todo |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` | la API de la E18 | zod estricto con los ejemplos, también el bloque |
| F2 | «Mostrar si» | `components/logic/ConditionsEditor.tsx`, `lib/conditions.ts` | su tablero | solo preguntas anteriores; referencia rota; hasta 10; el aviso de WhatsApp (235) |
| F3 | La pregunta «Cálculo» | `components/logic/CalculationEditor.tsx`, `lib/formula.ts`, `lib/decimal.ts` | su tablero | armar con piezas; en el bloque, solo lo de su ítem; el teléfono no la muestra; moneda de los datos de referencia |
| F4 | El bloque | `components/logic/RowsBlockEditor.tsx`; suma «Agregar bloque» a la banda y «Se carga varias veces» a `QuestionEditor.tsx`, y la pantalla de un ítem al teléfono al costado [E14] | su tablero | «Agregar bloque» lo abre y se apaga con uno; «Tipo de respuesta» no lo ofrece; lo que no entra, apagado con su tooltip; marcar y desmarcar; mínimo y máximo; quitar pide confirmar; el catálogo con sus filtros; los textos con «ítem»; axe |
| F5 | Planilla, detalle, tablero y tarjeta | suma a `FormRecordsPage.tsx`, `RecordValues.tsx`, `FormDashboard.tsx` y `lib/values.ts` [E15], y a la tarjeta [E14] | su tablero | «No se mostró», «Sin respuesta» y «—»; «3 ítems» y «1 ítem»; «En algún ítem de…»; los ítems alineados y su tabla, y en el teléfono, alineados; un monto con su moneda |
| F6 | Exportar | suma «Filas» y el chip del bloque a `ExportDialog.tsx` [E15] | su tablero | las dos filas; el chip «(en algún ítem de Productos)»; el tope con su texto |

**Al cerrar:** `docs/features/formularios.md` suma la sección.

## Los textos del bloque

Todo texto que ve el usuario dice «ítem» («item» en inglés) donde allá decía «renglón» (177). «Ítem» lleva tilde y mayúscula solo al empezar, y va con «otro» y «un», así se lee bien con cualquier nombre de bloque. «Renglón» queda donde es una línea. Son los de esta etapa:

| Dónde | Dice | En inglés |
| --- | --- | --- |
| El botón, y apagado | «Agregar bloque» · «Ya hay un bloque.» | «Add block» · «There is already a block.» |
| El nombre con que arranca | «Bloque» | «Block» |
| La lista del editor | «Bloque · de 1 a 10 ítems · 3 preguntas»; con el mínimo igual al máximo, «Bloque · 1 ítem · 2 preguntas», y con mínimo 0, «Bloque · hasta 10 ítems · 3 preguntas» | «Block · 1 to 10 items · 3 questions», «Block · 1 item · 2 questions», «Block · up to 10 items · 3 questions» |
| El recuadro, y sobre «Mínimo» y «Máximo» | «Preguntas de cada ítem» · «Ítems» | «Questions for each item» · «Items» |
| La (i) del máximo | «En WhatsApp se cargan hasta 10 ítems.» | «WhatsApp loads up to 10 items.» |
| La casilla y su (i) | «Se carga varias veces» · «Se carga una vez por ítem.» | «Loaded several times» · «Loaded once per item.» |
| El tooltip de Foto y Documento, apagadas en el bloque | «No se carga varias veces.» | «Not loaded several times.» |
| El tooltip de Elegir varias y de «Permitir elegir varias», apagadas en el bloque | «En un bloque se elige una sola opción.» | «A block picks a single option.» |
| Un rango que no sirve | «Elegí un mínimo entre 0 y el máximo, y un máximo entre 1 y 100.» | «Choose a minimum between 0 and the maximum, and a maximum between 1 and 100.» |
| Quitar el bloque | «¿Quitar el bloque?», «Se quitan el bloque «Productos» y sus 2 preguntas.» y «Quitar el bloque» | «Remove the block?», «The block «Productos» and its 2 questions are removed.» and «Remove the block» |
| La fórmula | «La suma de una pregunta del bloque» | «The sum of a block question» |
| «Qué se publica» | «Pasan al bloque» | «Move into the block» |
| Publicar, sin preguntas | «Agregá al menos una pregunta al bloque.» | «Add at least one question to the block.» |
| Publicar, el máximo y el mínimo | «En WhatsApp se cargan hasta 10 ítems; el máximo es 20.» · «…; el mínimo es 15.» | «WhatsApp loads up to 10 items; the maximum is 20.» · «…; the minimum is 15.» |
| WhatsApp: el título de cada pantalla | «Productos · ítem 2» | «Productos · item 2» |
| WhatsApp: la pregunta de cada ítem, y con mínimo 0 | «¿Agregás otro ítem?» · «¿Agregás un ítem?» | «Add another item?» · «Add an item?» |
| WhatsApp: la pantalla de cierre, con el nombre del formulario de título (204) | «Tocá «{botón final}» para mandar el formulario.» | «Tap “{final button}” to send the form.» |
| «Filtros» | «En algún ítem de Productos» | «In any item of Productos» |
| El Excel y su diálogo (227) | la columna «Ítem»; «Filas», «Una por ítem de Productos» y «Una por respuesta»; el chip «Estado: Dañado (en algún ítem de Productos)»; el tope (`TooManyRows`), «El Excel tendría más de 100.000 filas, una por ítem. Acotá los filtros o sacá el bloque de las columnas.» | «Item»; «Rows», «One per item of Productos» and «One per response»; «Estado: Dañado (in any item of Productos)»; «The Excel file would have more than 100,000 rows, one per item. Narrow the filters or remove the block from the columns.» |
| Publicar, un error de Meta sin pregunta (`ValidationUnlocated`) | «WhatsApp no aceptó cómo la app armó el formulario. No es un problema de tus preguntas: si vuelve a pasar, avisá a soporte.» | «WhatsApp didn't accept how the app built the form. It isn't a problem with your questions: if it happens again, contact support.» |
| El aviso de «Mostrar si» (235), con cada motivo de `ConditionNotSupported` | «WhatsApp no muestra una condición con números.» | «WhatsApp doesn't show a condition with numbers.» |

La lista del editor elige la forma por el mínimo y el máximo: iguales, la cantidad («1 ítem», «3 ítems»); mínimo 0, «hasta N ítems»; si no, «de N a M ítems». El plural sigue al último número. El nombre del bloque es contenido, y va como se escribió (35). «¿Agregás otro ítem?» usa 19 de los 30 caracteres que Meta admite en un sí o no.

## Lo que puede salir mal (no repetir)

Los cálculos no tienen revisión de allá; el bloque y lo que Meta no acepta en una condición, sí. Cada punto lleva su test:
1. **Confiar en lo que llega:** un cliente puede mandar el valor de una pregunta oculta o saltear una obligatoria.
2. **Una regla en memoria y otra en SQL:** `unanswered` da lo mismo en las dos.
3. **Un cálculo que rompe la captura:** nada de una fórmula puede frenar una respuesta.
4. **`double` en un paso intermedio:** todo en `decimal`, y sin `Math.Round` sin modo.
5. **Un cálculo que cambia solo:** se guarda el resultado, no se recalcula al leer.
6. **Un comprobante con varios ítems no entraba en una respuesta.** Allá, en la prueba del 2026-11-02, un remito de tres artículos fueron tres respuestas iguales salvo el artículo. Lo resuelve el bloque (146).
7. **Sin `routing_model`, «No» cortaba el formulario.** Allá, el 2026-11-06, el celular decía «Se produjo un error»: Meta deducía las rutas de los `navigate` y dejaba afuera la rama «No» del `If`. El test fija las dos ramas de cada `If`.
8. **La captura que no entiende el bloque.** Allá la respuesta con ítems se descartó, porque la captura no aceptaba el tipo del bloque, y el chat dijo «no pudimos relacionar con un envío». Acá la captura y su aviso nacen con el bloque.
9. **La planilla que no conoce el bloque.** Allá el backend sumó la columna del bloque, el front la rechazó y la planilla dijo «No pudimos cargar las respuestas.». La columna sale con su ejemplo exportado y el esquema del front, a la vez.
10. **Un mínimo que WhatsApp no puede cumplir:** con 15, nadie completaba el formulario por WhatsApp. Publicar frena (178), y la captura valida siempre el mínimo.
11. **Dar por aceptado lo que Meta no aceptó.** Allá se escribió que Meta había aceptado el bloque al final, y no era cierto: rechazó la publicación con `INVALID_PROPERTY_VALUE` («Expected 'complete'»), y lo desmintieron `GET /{flow-id}?fields=status,json_version,validation_errors` y `GET /{flow-id}/assets`. Una documentación no prueba que Meta aceptó algo, y tampoco los dobles de prueba ni el Meta simulado del E2E: cada forma nueva del Flow se publica con Meta y se mira `validation_errors` antes de darla por buena. El test fija que ninguna pantalla que manda navega.
12. **`&&` o `||` en una condición, o más de tres `If` anidados.** Meta los rechaza (allá, 2026-11-11). El test busca que ningún Flow compilado los tenga.
13. **Contar el bloque entre las 40 preguntas.** No cuenta; sí cada una de las suyas. El test llega al tope con un bloque.
14. **Una condición que nombra una opción borrada.** Guardar la comprueba, y borrar toma antes el bloqueo de las definiciones; el test va con dos actores.
15. **Un Excel sin tope de filas.** Una copia atiende a todas las organizaciones (lo medido está en «Contrato»): el tope se cuenta antes de escribir, y el test lo pasa por uno.
