# E19 · Condiciones, cálculos y líneas (spec)

**Estado:** pendiente · spec de la etapa 19 del bloque (decisiones 53, 54 y 78), con el [formato](plantilla.md) de las etapas. No tiene equivalente en ArquitecturaBase, salvo el editor de condiciones de las reglas, del que sale «Mostrar si». **Antes de programar** se prueba con Meta cómo se ven en el celular una pregunta que aparece según otra, una pantalla por renglón con «¿Agregás otro renglón?» y una pregunta de archivo dentro de un `If`, y `/plan-etapa` parte de ese resultado, después de la E18.

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T9, T11, T13, T24, la §3, la §6, punto 7, y la §14; la [E15](15-respuestas.md): filtros, valores, Excel y tablero; la [E12](12-catalogos.md): C5, y la [E13](13-campos.md): los tipos de campo.
2. **Decisiones** de la hoja de ruta: 35, 43, 53, 54 y 78.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [numeros-y-moneda](../../rules/numeros-y-moneda.md) | `decimal`, `Money`, redondeo `AwayFromZero` y los decimales de cada moneda |
   | [datos-de-referencia](../../rules/datos-de-referencia.md) | la moneda de un cálculo, nunca una lista en el código |
   | [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | los errores de cada pregunta |
   | [persistencia-ef](../../rules/persistencia-ef.md) y [multitenancy](../../rules/multitenancy.md) | la tabla nueva y las columnas |
   | [api-http](../../rules/api-http.md) | las rutas que cambian y sus ejemplos |
   | [logs](../../rules/logs.md) | ni fórmulas con datos ni valores en los logs |
   | [textos-y-traducciones](../../rules/textos-y-traducciones.md) | «No se mostró» y las palabras del editor, en es y en |
   | [tests](../../rules/tests.md) | qué lleva test y dónde va |

4. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
5. **Referencia de producto:** no hay: esta etapa es nueva. La de los Flows es la §14 de la E14.

## Qué se construye

**Preguntas condicionales:**
- una pregunta puede tener «Mostrar si», con hasta 10 condiciones unidas con «y», sobre preguntas anteriores: sí/no y elegir una (`in`), elegir varias (`anyOf`), número y fecha (`equals`, `gte`, `lte`), y respondida o no (`answered`, `unanswered`). Es la gramática de la E12 (C5), con la misma regla de la E15;
- en WhatsApp, la pregunta aparece o se oculta mientras la persona completa el formulario, con el componente `If` **[Meta]**. Un título de sección cuyas preguntas quedan todas ocultas tampoco se ve;
- **lo que el canal no pueda mostrar no se publica:** la comprobación lo marca en su pregunta, y nunca se manda sin la condición;
- **el servidor vuelve a evaluar al capturar y nunca confía en lo que llega.** Evalúa en orden: una pregunta oculta no tiene valor para las que dependen de ella, así que se ocultan en cadena. Una pregunta oculta no guarda valor, y «obligatoria» vale solo si se mostró;
- **«No se mostró»:** la respuesta guarda qué preguntas quedaron ocultas. En la planilla se ve como «No se mostró», distinto de «Sin respuesta» y de «—». Las condiciones suman `notShown`, y `unanswered` también lo incluye; la planilla no lo filtra (143);
- una pregunta condicional de catálogo con un problema de oferta frena el envío igual: no se sabe de antemano si se va a ver.

**Cálculos:**
- un tipo de pregunta «Cálculo», que no se ve en WhatsApp ni cuenta en sus topes;
- su fórmula usa un número fijo, un número o un cálculo anterior de la misma respuesta, un campo numérico de la opción elegida en un catálogo y la suma de una pregunta de los renglones, que se puede multiplicar renglón por renglón por otra pregunta de los renglones o por un campo del catálogo elegido en ese renglón (Cantidad × Precio);
- **solo las cuatro operaciones** y paréntesis, armadas en el editor sin escribir código;
- se calcula **al llegar la respuesta,** con los valores sellados, en `decimal` y con redondeo `AwayFromZero` a los decimales del cálculo. Si es un monto, el formulario fija su moneda, de los datos de referencia, con los decimales de esa moneda;
- **una división por cero, un dato que falta, también el de una pregunta que no se mostró, o un número demasiado grande dejan el cálculo «Sin respuesta»,** nunca un error de la captura. En una suma de renglones, un renglón sin ese dato no cuenta, y en un monto, cada renglón se redondea antes de sumar, como manda la ficha;
- el resultado se guarda: editar el catálogo o el formulario después no lo cambia;
- se ve en la tarjeta del chat, y en la planilla, el detalle, el Excel y el tablero, como un número que se filtra y se ordena. Un monto se ve con su moneda, y el tablero lo totaliza en ella.

**Renglones** (146, que cambia la 54):
- un tipo de pregunta «Renglones»: un recuadro con sus preguntas adentro (5.1, 5.2…), con nombre, ayuda y un mínimo y un máximo de renglones. El mínimo va de 0 al máximo y el máximo llega hasta 100; uno nuevo arranca con 1 y 20, y con mínimo 0 es opcional. La (i) del máximo dice el tope de WhatsApp;
- **las preguntas se eligen con «Se carga varias veces»,** una casilla debajo de «Obligatoria», con su (i). Las marcadas se juntan en el bloque; si no hay uno, se crea en el lugar de la primera, llamado «Renglones» hasta que el autor le ponga otro nombre. Desmarcar una la saca, y queda justo después del bloque. Una pregunta que ya existía pasa como pregunta nueva, con otra identidad: lo cargado antes conserva su columna, y publicar lo dice en «Pasan a renglones»;
- un bloque por formulario, sin bloques adentro. Entran texto corto y largo, número, fecha, sí o no y elegir una, propia o de un catálogo; elegir varias, las fotos y los documentos quedan afuera. Sus preguntas cuentan para el tope de preguntas del formulario, y un bloque sin preguntas se guarda pero no se publica;
- **en WhatsApp, una pantalla por renglón,** hasta 10 o el máximo si es menor, con el número en el título («Artículos · renglón 2»). Con un mínimo de 0, el bloque abre con una pantalla con su nombre y «¿Agregás un renglón?» antes del primero; con un mínimo de 1 o más, esa pantalla no aparece (RG8-A). Desde el mínimo, cada renglón termina con «¿Agregás otro renglón?», sí o no; el último no pregunta, y la flecha vuelve al anterior. Es el Flow estático con más pantallas, sin intercambio con Meta **[Meta]**. Con un máximo mayor que 10, publicar avisa sin frenar;
- **el tope de la respuesta:** al publicar se suma lo más largo que podría mandar cada pregunta, una vez por renglón de WhatsApp. Un formulario con bloque tiene el tope de uno con archivos, 64 KiB en lugar de 16 KiB; si igual no entra, publicar frena como hoy (RG7-A). Falta confirmar con Meta que una respuesta de unos 40 KB llega entera **[Meta]**;
- **el servidor valida la cantidad** contra el mínimo y el máximo, y por WhatsApp contra el tope, y cada renglón como preguntas sueltas: obligatorias, tipos, largos y opciones ofrecidas. Un renglón vacío cuenta como renglón. Cada renglón se guarda con su posición, y los catálogos se sellan como en una pregunta suelta;
- en la tarjeta del chat, un renglón por línea. La planilla sigue con una fila por respuesta: la columna del bloque dice cuántos renglones tiene y los valores de su primera pregunta, cortados con «…», y no se ordena. En «Filtros», las preguntas del bloque van debajo de su nombre y filtran por algún renglón. El detalle muestra cada renglón con su número y su primer valor, y los demás datos alineados debajo (139); la pantalla completa, una tabla. El Excel suma una hoja con el nombre del bloque y una fila por renglón: N°, Renglón y sus preguntas.

**Copiar un formulario** copia las condiciones, los cálculos y los renglones: el borrador se copia con sus identidades, así que no hay nada que traducir.

**Al editar:**
- el borrador se guarda incompleto, como siempre. **Una referencia rota,** a una pregunta quitada, que ahora va después o que cambió de tipo, se guarda y frena al publicar, con «Ir a la pregunta»;
- el selector de cada condición y de cada dato de la fórmula ofrece solo las preguntas anteriores que sirven;
- el front avisa lo que ve, pero no tapa al servidor: la comprobación que vale es la de publicar.

## Pantallas

Aprobados y publicados en el lienzo v153, con el recorrido guiado `un-pedido-con-renglones`. El ejemplo es «Pedido de materiales». Muestran solo lo que existe hasta acá (78). Con el traspaso 5 se redibujan, con sus teléfonos, `FRM-Editor-Lineas`, `FRM-Respuestas-19`, `FRM-Respuesta-19` y `WA-Chats-Respuesta-19`, y también `FRM-Publicar-19`: los renglones (146) y el detalle alineado (139).

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Editor-Condiciones` y `M-FRM-Editor-Condiciones` | «Mostrar si» en una pregunta, y el teléfono al costado, con la pregunta que aparece |
| `FRM-Editor-Calculo` y `M-FRM-Editor-Calculo` | la pregunta «Cálculo»: las piezas de la fórmula, la moneda y los decimales |
| `FRM-Editor-Lineas` y `M-FRM-Editor-Lineas` | el bloque «Renglones», con «Se carga varias veces», el mínimo y el máximo, y una pantalla por renglón en el teléfono |
| `FRM-Respuestas-19` y `M-FRM-Respuestas-19` | la planilla, con «No se mostró», el cálculo y la columna del bloque, y en «Filtros» las preguntas del bloque |
| `FRM-Respuesta-19` y `M-FRM-Respuesta-19` | el detalle, con cada renglón y sus datos alineados y los cálculos, y la tabla en la pantalla completa |
| `WA-Chats-Respuesta-19` y `M-WA-Conversacion-Respuesta-19` | la tarjeta, con los renglones y el total |
| `FRM-Tablero-19` y `M-FRM-Tablero-19` | el tablero, con un monto y su moneda |
| `FRM-Publicar-19` | una condición que WhatsApp no puede mostrar, más de 50 componentes, «Pasan a renglones» y un máximo de más de 10 |

**Estados que se dibujan:**
- **Editor:** una condición con cada tipo de pregunta; una fórmula incompleta, con el motivo debajo de su pregunta; una referencia rota; el contador de componentes cerca de 50, y sin preguntas anteriores que sirvan;
- **Publicar:** problemas por pregunta, con «Ir a la pregunta»;
- **Planilla y detalle:** una respuesta con una pregunta oculta, y un cálculo que quedó «Sin respuesta».

**«Mostrar si»:** va en la pregunta abierta, en su caja, como las condiciones de las reglas de ArquitecturaBase: Pregunta, Condición, Valor y el tacho, y «Agregar condición». Sin preguntas anteriores que sirvan, «Agregar condición» queda grisado, con el motivo en su tooltip. Una referencia rota queda en rojo, con el motivo debajo, y la pregunta dice «Corregir».

**La fórmula:** piezas en una caja, con «Agregar dato ▾» (un número, una pregunta, un campo del catálogo o la suma de una columna) y + − × ÷ ( ). La suma de una columna se elige en un diálogo. «Es un monto» pide la moneda, y los decimales salen de ella.

**El bloque en el editor:** un recuadro en la lista de preguntas, con las suyas adentro, ↑ ↓ ✎ 🗑 en cada una y su propio «Agregar pregunta» en la banda «Preguntas de cada renglón». El ✎ del bloque edita el nombre, la ayuda, el mínimo y el máximo, y con el bloque abierto el teléfono al costado muestra la pantalla de un renglón. Con un bloque, «Renglones» queda apagado en «Agregar pregunta», con el motivo en su tooltip. Cerca de 50 componentes en una pantalla, se lee «46 de 50 componentes».

**Mandan sobre esta spec en:** las fichas del front. La fórmula se arma con piezas, sin un campo para escribirla. Un dato por celda, y un monto con su moneda.

## Contrato

**Sin rutas nuevas.** Son las de la E14 y la E15, que suman lo siguiente. Van en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

| Ruta | Qué suma |
| --- | --- |
| `GET /{id}` y `PUT /{id}/draft` | tipos `Calculation` y `Rows`. `questions[]` suma `visibleIf?[]`, con `{fieldId, operator, values[]}`; `calculation?`, con `{tokens[], currency?, decimals}`, y `rows?`, con `{minRows, maxRows}`; las preguntas del bloque llevan `rowsOf`, el `fieldId` del bloque |
| `POST /{id}/publication-checks` | suma los errores de abajo, `componentCount`, «Pasan a renglones», el bloque sin preguntas y el aviso de más de 10 renglones |
| `GET /{id}/records/columns` | tipos nuevos, y `currency?` |
| `POST …/records/search` y el detalle | los valores de abajo. `sort` admite `field:<id>` de un cálculo, no de un grupo |
| `POST …/records/search` y `facets` | las preguntas del bloque filtran por algún renglón: un `EXISTS` sobre sus valores, sin multiplicar la respuesta |
| `POST …/records/export` | la hoja de cada bloque; una celda «No se mostró» |
| `POST …/records/dashboard` | `numbers[]` suma los cálculos y `currency?` |
| `GET …/conversations/{id}/forms` | `optionCounts` y `offerProblems` cuentan también las preguntas de catálogo del bloque |

**Un dato de la fórmula** es `{kind: "number", value}`, `{kind: "question", fieldId}`, `{kind: "catalogField", fieldId, catalogFieldId}` o `{kind: "rowSum", fieldId, times?}`, con el `fieldId` de una pregunta del bloque, y `times` es `{kind: "question", fieldId}` o `{kind: "catalogField", fieldId, catalogFieldId}`. Las otras piezas son `{kind: "op", op}`, `{kind: "open"}` y `{kind: "close"}`.

`catalogFieldId` es la identidad del campo, la misma del contrato de la E13: la clave queda adentro. **Lo que usa un campo o un catálogo cuenta:** `Forms.Catalog.FieldInUse` y «Lo usan» suman las fórmulas y las preguntas de catálogo del bloque, así quitar un campo no deja los totales en «Sin respuesta» sin avisar.

**Valores nuevos,** en el mapa de la E15:
- `{kind: "hidden"}`: no se mostró;
- `{kind: "money", amount, currency}`, para un cálculo con moneda, con el monto como en `Money`;
- `{kind: "rows", count, preview, rows?[{position, values}]}`: `preview` son los valores de su primera pregunta, para la planilla, y `rows` va solo en el detalle.

**Puertos:** `StructuredFormVersion.Questions` suma `VisibleIf`, `Calculation` y `Rows`. `StructuredRecordCapture` suma `Rows[BlockFieldId, RowPosition, FieldId, Text?|Number?|Date?|Bool?|Choice?]`, con la opción como en la E14. **Una pregunta de catálogo del bloque tiene su oferta,** con el mismo tope que una suelta, y todos los renglones usan la misma lista. `StructuredRecordCard` suma los renglones y los cálculos. `IStructuredFormChannel.Check` valida el perfil nuevo.

**Errores** (con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Draft.ConditionInvalid` | Validation (`questions[i].visibleIf[j]`) | más de 10, una pregunta que no está antes o no sirve, o un operador o valor fuera de la gramática |
| `Forms.Draft.FormulaInvalid` | Validation (`questions[i].calculation`) | vacía, mal armada, un dato que no es numérico o no está antes, o una moneda fuera de los datos de referencia |
| `Forms.Draft.RowsInvalid` | Validation (`questions[i].rows`) | un mínimo o un máximo fuera de rango, un segundo bloque, un tipo que no entra o un catálogo que no se puede usar |
| `WhatsApp.Flow.ConditionNotSupported` | Validation (`questions[i].visibleIf[j]`) | el canal no la puede mostrar |
| `Forms.Record.RowsInvalid` | Validation | menos renglones que el mínimo, más que el máximo o que el tope de WhatsApp, o un renglón sin una obligatoria |

`WhatsApp.Flow.TooManyComponents` y `ResponseTooLarge` (E14) cuentan cada pantalla de renglón. Al capturar, una respuesta con un valor en una pregunta oculta no da error: el valor se descarta.

## Datos

Una migración aditiva, `<fecha>_FormLogic.cs`. Lo de una pregunta vive en el jsonb del borrador y de la versión, sin columnas nuevas.

| Tabla | Columna | Tipo | Regla |
| --- | --- | --- | --- |
| `FormRecords` | `HiddenFieldIds` | uuid[], vacío por defecto | las preguntas que no se mostraron |
| `FormRecordValues` | `Currency` | texto 3? | en un cálculo con moneda; CHECK que lo ata a `NumberValue`. Se valida con `ValidCurrency()`, sin FK |

**FormRecordRowValues** (`ITenantOwned`, esquema `tenant`, con RLS, sin auditoría: es parte de la respuesta)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `RecordId`, `BlockFieldId`, `RowPosition`, `FieldId` | uuid y smallint | PK; FK compuesta a `FormRecords`. `RowPosition` va de 0 a 99 |
| `Type` | texto | el tipo de la pregunta |
| `TextValue`, `NumberValue`, `DateValue`, `BoolValue` | los de `FormRecordValues` | CHECK: exactamente un valor |
| `ChoiceId`, `ChoiceLabel`, `CatalogId`, `ChoiceCode`, `ChoiceDetail`, `ChoiceValue`, `ChoiceFields` | los de `FormRecordValues` | sellados, como en una pregunta suelta |

Una tabla aparte, y no una columna más en `FormRecordValues`: la clave de esa tabla cambiaría para todas las respuestas, y lo de la E15 lee esa fila por `FieldId` y `Position`.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front, y `[E14]` a `[E18]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Evaluar condiciones en memoria, con la regla de la E15 | `D/Forms/FormConditionEvaluator.cs`, junto a `FormRecordCondition.cs` [E15] | `FormRecordFiltersPolicy.cs` [E15] | cada operador; en cadena; la tabla de verdad igual que en SQL |
| B2 | Calcular una fórmula | `D/Forms/FormFormula.cs` y `FormFormulaEvaluator.cs` | — | precedencia y paréntesis; división por cero; dato que falta; desborde; `AwayFromZero`; los decimales de la moneda; una suma de renglones con `times` |
| B3 | Las preguntas en el borrador | suma a `FormQuestionType.cs`, `FormDraft.cs`, `FormDraftEditor.cs`, `FormVersionCreator.cs` y al control de `FieldInUse` [E14]; `A/Modules/Forms/Services/FormLogicPolicy.cs` | `FormRecipientFilterPolicy.cs` [E16] | cada motivo de los tres errores; referencia rota al quitar, mover y cambiar el tipo; guardar incompleto sigue andando; un campo que usa una fórmula no se quita; una pregunta de catálogo del bloque cuenta en «Lo usan»; pasar una pregunta al bloque y sacarla, con otra identidad |
| B4 | Publicar y el Flow con `If` y una pantalla por renglón, con un perfil nuevo | suma a `WhatsAppFlowBuilder.cs`, a `Check` y a `FormOfferBuilder.cs` [E14] | el mismo | la condición que no se puede mostrar; 50 componentes; 16 KiB; título oculto; lo sellado no se recompila; «¿Agregás otro renglón?» desde el mínimo; el aviso de más de 10; el bloque sin preguntas frena; la oferta de una pregunta de catálogo del bloque |
| B5 | La captura: visibilidad, descarte, renglones, sellado y cálculo, en ese orden | `A/Modules/Forms/Services/FormRecordLogicPolicy.cs`; suma a `FormRecordService.cs`, `FormRecordWriter.cs` [E14] y `WhatsAppFlowReplyVerifier.cs` | esos mismos | un valor en una oculta se descarta; obligatoria oculta; la cantidad contra el mínimo, el máximo y el tope; un renglón vacío cuenta; catálogo en un renglón; dos capturas iguales |
| B6 | Tabla, columnas y migración | `D/Forms/FormRecordRowValue.cs`; `I/…/Configurations/FormRecordRowValueConfiguration.cs`; la migración | `FormRecordValueConfiguration.cs` [E14] | aislamiento y migración, también hacia atrás |
| B7 | Lo que se lee: planilla, conteos, detalle y navegación | suma a `FormRecordColumnsBuilder.cs`, `FormRecordReader.cs`, `FormRecordQueryFilters.cs` [E15] | esos mismos | `notShown` y `unanswered`; un cálculo se filtra y se ordena; algún renglón con un `EXISTS`; renglones sin consultas por fila |
| B8 | Excel y tablero | suma a `FormRecordSheetBuilder.cs`, `FormRecordSpreadsheet.cs` y `FormDashboardBuilder.cs` [E15] | esos mismos | una hoja por bloque; el monto como número con su moneda en el encabezado; el total en su moneda |
| B9 | La tarjeta del chat y «Mis mensajes» | suma a `FormRecordSource.cs` y a la copia de la tarjeta [E14] | esos mismos | los renglones y el total; sin `formrecords.read`, solo el número |
| B10 | Contratos y ejemplos | suma a `Contracts/*`, `FormsContractContributor.cs` y los modelos de `StructuredData` | esos mismos [E14] | ejemplos exportados de los tipos nuevos; copiar conserva todo |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` | la API de la E18 | zod estricto con los ejemplos |
| F2 | «Mostrar si» | `components/logic/ConditionsEditor.tsx`, `lib/conditions.ts` | su tablero | solo preguntas anteriores; referencia rota; hasta 10 |
| F3 | La pregunta «Cálculo» | `components/logic/CalculationEditor.tsx`, `lib/formula.ts`, `lib/decimal.ts` | su tablero | armar con piezas; el teléfono no la muestra; moneda de los datos de referencia |
| F4 | Los renglones | `components/logic/RowsBlockEditor.tsx`; suma «Se carga varias veces» a `QuestionEditor.tsx` y la pantalla de un renglón al teléfono al costado [E14] | su tablero | marcar y desmarcar; mínimo y máximo; un solo bloque; axe |
| F5 | Planilla, detalle, tablero y tarjeta | suma a `FormRecordsPage.tsx`, `RecordValues.tsx`, `FormDashboard.tsx` y `lib/values.ts` [E15], y a la tarjeta [E14] | su tablero | «No se mostró», «Sin respuesta» y «—»; los renglones alineados y su tabla; un monto con su moneda |

**Al cerrar:** `docs/features/formularios.md` suma la sección.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | `If` y su expresión, antes de programar: comparar un `Dropdown`, una casilla de `CheckboxGroup`, un número escrito en `TextInput` y una fecha, entre pantallas, con cuánta anidación, y dónde se admite. | Lo que no se pueda, `Check` lo frena al publicar. Los números hoy son texto (§14 de la E14), y puede no haber comparación numérica. |
| [Meta] | Una pregunta de archivo dentro de un `If`, en su pantalla. | Si no se puede saltear, se la frena al publicar con `ConditionNotSupported`. |
| [Meta] | Una pantalla por renglón: el límite de ramas, cómo llega lo de las pantallas que no se visitaron, que la cantidad de renglones viaje en el envío final y que las opciones de un catálogo sirvan en cada pantalla. | Alcanzan 10 renglones sin intercambio; si no, el tope baja. |
| [E14] | El orden de la captura y `FormRecordValues`. | Se pueden sumar los pasos, y el texto sellado existe. |
| [E15] | La función de filtros, la lectura en memoria de qué versiones tienen cada pregunta y `Number`. | `notShown` entra en la misma función. |
| [E18] | El perfil del Flow. | Este suma el siguiente. |
| Datos de referencia | Cómo se valida una moneda y sus decimales. | `ICurrencyCatalog` da los decimales. |

## Lo que puede salir mal (no repetir)

No hay revisión de allá. Cada punto lleva su test:
1. **Confiar en lo que llega:** un cliente puede mandar el valor de una pregunta oculta o saltear una obligatoria.
2. **Una regla en memoria y otra en SQL:** `unanswered` da lo mismo en las dos.
3. **Un cálculo que rompe la captura:** nada de una fórmula puede frenar una respuesta.
4. **`double` en un paso intermedio:** todo en `decimal`, y sin `Math.Round` sin modo.
5. **Un cálculo que cambia solo:** se guarda el resultado, no se recalcula al leer.
6. **Un comprobante con varios ítems no entraba en una respuesta.** Allá, en la prueba del 2026-11-02, un remito de tres artículos fueron tres respuestas iguales salvo el artículo. Lo resuelven los renglones de esta etapa (146).

## Decisiones tomadas (no se vuelven a preguntar)

- **Un monto calculado se guarda en `NumberValue`,** con su `Currency`, y se lee como `Money`: es la columna de todos los números, y así se filtra y se ordena con la misma regla. Se redondea a los decimales de la moneda, y un monto de más de 13 dígitos enteros, el tope de un importe, cuenta como demasiado grande.
- **Una etapa propia** (54), después de las respuestas, así lo que suma se ve en lo que ya existe (78).
- **Los cálculos se hacen al llegar la respuesta:** quien completa no ve el total en WhatsApp antes de enviar (54).
- **Los renglones** (146): un mínimo y un máximo de hasta 100, y en WhatsApp una pantalla por renglón, hasta 10. Cambia el tope de 1 a 10 de la 54.
- **Genérico:** ninguna fórmula ni bloque va escrito en el código.

## Fuera de esta etapa

- Más de un bloque, bloques dentro de un bloque y condiciones dentro de un bloque.
- Mostrar el total en WhatsApp antes de enviar.
- Funciones más allá de las cuatro operaciones y la suma de una pregunta de los renglones.
- Un filtro por destinatario en una pregunta de catálogo del bloque.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba, abriendo cada variante en un celular:
1. «Monto» aparece solo si «¿Se concretó la venta?» es Sí; con No, la respuesta llega sin monto.
2. Un pedido con 3 renglones (producto del catálogo y cantidad), con su total calculado con el precio del catálogo, en la tarjeta del chat.
3. Las dos respuestas en la planilla, el detalle y el Excel: «No se mostró», los renglones y el total.

## Requisitos previos

- La E18 cerrada.
- La prueba con Meta del componente `If`, de una pantalla por renglón y de una pregunta de archivo dentro de un `If`.

## En el multitenant

- **Datos de la organización:** `FormRecordRowValues` y las columnas nuevas son `ITenantOwned`, con RLS.
- **Workers:** no hay; el cálculo corre en la captura.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos:** los de la E14, sin permisos nuevos.
- **Cuotas:** las rutas cuentan en las cuotas generales.
