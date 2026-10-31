# E19 · Condiciones, cálculos y líneas (spec)

**Estado:** pendiente · spec de la etapa 19 del bloque (decisiones 53, 54 y 78), con el [formato](plantilla.md) de las etapas. No tiene equivalente en ArquitecturaBase. **Antes de programar** se prueba con Meta cómo se ven en el celular una pregunta que aparece según otra, un grupo de renglones y una pregunta de archivo dentro de un `If`, y `/plan-etapa` parte de ese resultado, después de la E18.

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
6. **La maqueta,** en `../MaquetaMultitenant/src/areas/business/forms/`: `components/logic/ConditionsEditor.tsx`, `CalculationEditor.tsx` y `LinesEditor.tsx`, `lib/conditions.ts`, `lib/formula.ts` y `lib/decimal.ts`, y `LinesTable` en `components/records/RecordValues.tsx`. Sus reglas de cálculo y condiciones son el modelo para escribir las del servidor, que son las que valen.

## Qué se construye

**Preguntas condicionales:**
- una pregunta puede tener «Mostrar si», con hasta 10 condiciones unidas con «y», sobre preguntas anteriores: sí/no y elegir una (`in`), elegir varias (`anyOf`), número y fecha (`equals`, `gte`, `lte`), y respondida o no (`answered`, `unanswered`). Es la gramática de la E12 (C5), con la misma regla de la E15;
- en WhatsApp, la pregunta aparece o se oculta mientras la persona completa el formulario, con el componente `If` **[Meta]**. Un título de sección cuyas preguntas quedan todas ocultas tampoco se ve;
- **lo que el canal no pueda mostrar no se publica:** la comprobación lo marca en su pregunta, y nunca se manda sin la condición;
- **el servidor vuelve a evaluar al capturar y nunca confía en lo que llega.** Evalúa en orden: una pregunta oculta no tiene valor para las que dependen de ella, así que se ocultan en cadena. Una pregunta oculta no guarda valor, y «obligatoria» vale solo si se mostró;
- **«No se mostró»:** la respuesta guarda qué preguntas quedaron ocultas. En la planilla se ve como «No se mostró», distinto de «Sin respuesta» y de «—». El filtro suma `notShown`, y `unanswered` también lo incluye;
- una pregunta condicional de catálogo con un problema de oferta frena el envío igual: no se sabe de antemano si se va a ver.

**Cálculos:**
- un tipo de pregunta «Cálculo», que no se ve en WhatsApp ni cuenta en sus topes;
- su fórmula usa un número fijo, un número o un cálculo anterior de la misma respuesta, un campo numérico de la opción elegida en un catálogo y la suma de una columna de líneas, que se puede multiplicar renglón por renglón por otra columna o por un campo del catálogo elegido en ese renglón (Cantidad × Precio);
- **solo las cuatro operaciones** y paréntesis, armadas en el editor sin escribir código;
- se calcula **al llegar la respuesta,** con los valores sellados, en `decimal` y con redondeo `AwayFromZero` a los decimales del cálculo. Si es un monto, el formulario fija su moneda, de los datos de referencia, con los decimales de esa moneda;
- **una división por cero, un dato que falta o un número demasiado grande dejan el cálculo «Sin respuesta»,** nunca un error de la captura. En una suma de líneas, un renglón sin ese dato no cuenta, y en un monto, cada renglón se redondea antes de sumar, como manda la ficha;
- el resultado se guarda: editar el catálogo o el formulario después no lo cambia;
- se ve en la tarjeta del chat, y en la planilla, el detalle, el Excel y el tablero, como un número que se filtra y se ordena. Un monto se ve con su moneda, y el tablero lo totaliza en ella.

**Líneas:**
- un tipo de pregunta «Grupo de líneas», con hasta 6 columnas: texto, número, fecha, sí/no, elegir una y elegir de un catálogo;
- **un tope de renglones** que elige el formulario, de 1 a 10;
- en WhatsApp, renglones que se completan uno debajo del otro, con los que sobran vacíos y opcionales **[Meta]**. Cada celda cuenta en el tope de 50 componentes y en el presupuesto de 16 KiB;
- **el servidor valida renglón por renglón:** un renglón vacío no se guarda, y uno con algo escrito tiene que tener sus columnas obligatorias. Una pregunta obligatoria necesita al menos un renglón;
- cada renglón se guarda con su posición, y los catálogos se sellan como en una pregunta suelta;
- en la tarjeta del chat se ven los renglones. En la planilla, la columna del grupo dice cuántos tiene, y no se ordena; el detalle los muestra en una tabla; el Excel suma una hoja por grupo, «Líneas», «Líneas 2», con un renglón por línea y el número de la respuesta.

**Copiar un formulario** copia las condiciones, los cálculos y las líneas: el borrador se copia con sus identidades, así que no hay nada que traducir.

**Al editar:**
- el borrador se guarda incompleto, como siempre. **Una referencia rota,** a una pregunta quitada, que ahora va después o que cambió de tipo, se guarda y frena al publicar, con «Ir a la pregunta»;
- el selector de cada condición y de cada dato de la fórmula ofrece solo las preguntas anteriores que sirven;
- el front avisa lo que ve, pero no tapa al servidor: la comprobación que vale es la de publicar.

## Pantallas

Ninguno está dibujado. Diseño los dibuja con el [texto de la etapa](19-texto-diseno.md), con el recorrido guiado. Muestran solo lo que existe hasta acá (78).

| Tablero | Qué muestra |
| --- | --- |
| `FRM-Editor-Condiciones` y `M-FRM-Editor-Condiciones` | «Mostrar si» en una pregunta, y el teléfono al costado, con la pregunta que aparece |
| `FRM-Editor-Calculo` y `M-FRM-Editor-Calculo` | la pregunta «Cálculo»: las piezas de la fórmula, la moneda y los decimales |
| `FRM-Editor-Lineas` y `M-FRM-Editor-Lineas` | el grupo de líneas, sus columnas y su tope, y los renglones en el teléfono |
| `FRM-Respuestas-19` y `M-FRM-Respuestas-19` | la planilla, con «No se mostró», el cálculo y la columna de renglones, y el filtro en «Más filtros» |
| `FRM-Respuesta-19` y `M-FRM-Respuesta-19` | el detalle, con la tabla de líneas y los cálculos, también en la pantalla completa |
| `WA-Chats-Respuesta-19` y `M-WA-Conversacion-Respuesta-19` | la tarjeta, con los renglones y el total |
| `FRM-Tablero-19` | el tablero, con un monto y su moneda |

**Estados que se dibujan:**
- **Editor:** una condición con cada tipo de pregunta; una fórmula incompleta, con el motivo debajo de su pregunta; una referencia rota; el contador de componentes cerca de 50, y sin preguntas anteriores que sirvan;
- **Publicar:** una condición que WhatsApp no puede mostrar, y más de 50 componentes;
- **Planilla y detalle:** una respuesta con una pregunta oculta, y un cálculo que quedó «Sin respuesta».

**Mandan sobre esta spec en:** las fichas del front. La fórmula se arma con piezas, sin un campo para escribirla. Un dato por celda, y un monto con su moneda.

## Contrato

**Sin rutas nuevas.** Son las de la E14 y la E15, que suman lo siguiente. Van en `FormsContractContributor.cs`, con ejemplos exportados de una corrida real.

| Ruta | Qué suma |
| --- | --- |
| `GET /{id}` y `PUT /{id}/draft` | tipos `Calculation` y `Lines`. `questions[]` suma `visibleIf?[]`, con `{fieldId, operator, values[]}`; `calculation?`, con `{tokens[], currency?, decimals}`, y `lines?`, con `{columns[{columnId, label, type, required, decimals?, options?, catalogId?}], maxRows}` |
| `POST /{id}/publication-checks` | suma los errores de abajo, y `componentCount` |
| `GET /{id}/records/columns` | tipos nuevos, y `currency?` |
| `POST …/records/search` y el detalle | los valores de abajo. `sort` admite `field:<id>` de un cálculo, no de un grupo |
| `POST …/records/facets` | `notShown?` en las preguntas condicionales |
| `POST …/records/export` | la hoja de líneas; una celda «No se mostró» |
| `POST …/records/dashboard` | `numbers[]` suma los cálculos y `currency?` |

**Un dato de la fórmula** es `{kind: "number", value}`, `{kind: "question", fieldId}`, `{kind: "catalogField", fieldId, fieldKey}` o `{kind: "lineSum", fieldId, columnId, times?}`, y `times` es `{kind: "column", columnId}` o `{kind: "catalogField", columnId, fieldKey}`. Las otras piezas son `{kind: "op", op}`, `{kind: "open"}` y `{kind: "close"}`.

**Valores nuevos,** en el mapa de la E15:
- `{kind: "hidden"}`: no se mostró;
- `{kind: "money", amount, currency}`, para un cálculo con moneda, con el monto como en `Money`;
- `{kind: "lines", count, rows?[{position, values}]}`: `rows` solo en el detalle.

**Puertos:** `StructuredFormVersion.Questions` suma `VisibleIf`, `Calculation` y `Lines`. `StructuredRecordCapture` suma `Lines[FieldId, RowPosition, ColumnId, Text?|Number?|Date?|Bool?|ChoiceId?, CatalogId?, ChoiceCode?, ChoiceDetail?, ChoiceValue?]`. `StructuredRecordCard` suma las líneas y los cálculos. `IStructuredFormChannel.Check` valida el perfil nuevo.

**Errores** (con sus textos en `FormsTexts.resx` y `.en.resx`):

| Código | Tipo | Cuándo |
| --- | --- | --- |
| `Forms.Draft.ConditionInvalid` | Validation (`questions[i].visibleIf[j]`) | más de 10, una pregunta que no está antes o no sirve, o un operador o valor fuera de la gramática |
| `Forms.Draft.FormulaInvalid` | Validation (`questions[i].calculation`) | vacía, mal armada, un dato que no es numérico o no está antes, o una moneda fuera de los datos de referencia |
| `Forms.Draft.LinesInvalid` | Validation (`questions[i].lines`) | tope fuera de 1 a 10, sin columnas o con más de 6, o un catálogo que no se puede usar |
| `WhatsApp.Flow.ConditionNotSupported` | Validation (`questions[i].visibleIf[j]`) | el canal no la puede mostrar |
| `Forms.Record.LineInvalid` | Validation | a un renglón con algo escrito le falta una columna obligatoria, o hay más renglones que el tope |

`WhatsApp.Flow.TooManyComponents` y `ResponseTooLarge` (E14) cuentan las celdas. Al capturar, una respuesta con un valor en una pregunta oculta no da error: el valor se descarta.

## Datos

Una migración aditiva, `<fecha>_FormLogic.cs`. Lo de una pregunta vive en el jsonb del borrador y de la versión, sin columnas nuevas.

| Tabla | Columna | Tipo | Regla |
| --- | --- | --- | --- |
| `FormRecords` | `HiddenFieldIds` | uuid[], vacío por defecto | las preguntas que no se mostraron |
| `FormRecordValues` | `Currency` | char(3)? | en un cálculo con moneda; CHECK que lo ata a `NumberValue` |

**FormRecordLineValues** (`ITenantOwned`, esquema `tenant`, con RLS, sin auditoría: es parte de la respuesta)

| Columna | Tipo | Regla |
| --- | --- | --- |
| `TenantId`, `RecordId`, `FieldId`, `RowPosition`, `ColumnId` | uuid y smallint | PK; FK compuesta a `FormRecords`. `RowPosition` va de 0 a 9 |
| `Type` | texto | el tipo de la columna |
| `TextValue`, `NumberValue`, `DateValue`, `BoolValue` | los de `FormRecordValues` | CHECK: exactamente un valor |
| `ChoiceId`, `ChoiceLabel`, `CatalogId`, `ChoiceCode`, `ChoiceDetail`, `ChoiceValue`, `ChoiceFields` | los de `FormRecordValues` | sellados, como en una pregunta suelta |

Una tabla aparte, y no una columna más en `FormRecordValues`: la clave de esa tabla cambiaría para todas las respuestas, y lo de la E15 lee esa fila por `FieldId` y `Position`.

## Pasos

Abreviaturas: `D/`, `A/`, `I/` y `P/` son las capas del back; `F/`, el front; `M/`, la maqueta, y `[E14]` a `[E18]`, un archivo que deja esa etapa.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | Evaluar condiciones en memoria, con la regla de la E15 | `D/Forms/FormConditionEvaluator.cs`, junto a `FormRecordCondition.cs` [E15] | `M/…/lib/conditions.ts` | cada operador; en cadena; la tabla de verdad igual que en SQL |
| B2 | Calcular una fórmula | `D/Forms/FormFormula.cs` y `FormFormulaEvaluator.cs` | `M/…/lib/formula.ts` | precedencia y paréntesis; división por cero; dato que falta; desborde; `AwayFromZero`; los decimales de la moneda; una suma de líneas con `times` |
| B3 | Las preguntas en el borrador | suma a `FormQuestionType.cs`, `FormDraft.cs`, `FormDraftEditor.cs` y `FormVersionCreator.cs` [E14]; `A/Modules/Forms/Services/FormLogicPolicy.cs` | `FormRecipientFilterPolicy.cs` [E16] | cada motivo de los tres errores; referencia rota al quitar, mover y cambiar el tipo; guardar incompleto sigue andando |
| B4 | Publicar y el Flow con `If` y renglones, con un perfil nuevo | suma a `WhatsAppFlowBuilder.cs` y a `Check` [E14] | el mismo | la condición que no se puede mostrar; 50 componentes; 16 KiB; título oculto; lo sellado no se recompila |
| B5 | La captura: visibilidad, descarte, renglones, sellado y cálculo, en ese orden | `A/Modules/Forms/Services/FormRecordLogicPolicy.cs`; suma a `FormRecordService.cs`, `FormRecordWriter.cs` [E14] y `WhatsAppFlowReplyVerifier.cs` | esos mismos | un valor en una oculta se descarta; obligatoria oculta; renglones vacíos en el medio; catálogo en un renglón; dos capturas iguales |
| B6 | Tabla, columnas y migración | `D/Forms/FormRecordLineValue.cs`; `I/…/Configurations/FormRecordLineValueConfiguration.cs`; la migración | `FormRecordValueConfiguration.cs` [E14] | aislamiento y migración, también hacia atrás |
| B7 | Lo que se lee: planilla, conteos, detalle y navegación | suma a `FormRecordColumnsBuilder.cs`, `FormRecordReader.cs`, `FormRecordQueryFilters.cs` [E15] | esos mismos | `notShown` y `unanswered`; un cálculo se filtra y se ordena; renglones sin consultas por fila |
| B8 | Excel y tablero | suma a `FormRecordSheetBuilder.cs`, `FormRecordSpreadsheet.cs` y `FormDashboardBuilder.cs` [E15] | esos mismos | una hoja por grupo; el monto como número con su moneda en el encabezado; el total en su moneda |
| B9 | La tarjeta del chat y «Mis mensajes» | suma a `FormRecordSource.cs` y a la copia de la tarjeta [E14] | esos mismos | los renglones y el total; sin `formrecords.read`, solo el número |
| B10 | Contratos y ejemplos | suma a `Contracts/*`, `FormsContractContributor.cs` y los modelos de `StructuredData` | esos mismos [E14] | ejemplos exportados de los tipos nuevos; copiar conserva todo |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Tipos y llamadas | suma a `F/src/modules/forms/api/` | `M/…/api/types.ts` | zod estricto con los ejemplos |
| F2 | «Mostrar si» | `components/logic/ConditionsEditor.tsx`, `lib/conditions.ts` | `M/…` | solo preguntas anteriores; referencia rota; hasta 10 |
| F3 | La pregunta «Cálculo» | `components/logic/CalculationEditor.tsx`, `lib/formula.ts`, `lib/decimal.ts` | `M/…` | armar con piezas; el teléfono no la muestra; moneda de los datos de referencia |
| F4 | El grupo de líneas | `components/logic/LinesEditor.tsx`; suma a `QuestionEditor.tsx` y al teléfono al costado [E14] | `M/…` | columnas, tope y contador; axe |
| F5 | Planilla, detalle, tablero y tarjeta | suma a `FormRecordsPage.tsx`, `RecordValues.tsx`, `FormDashboard.tsx` y `lib/values.ts` [E15], y a la tarjeta [E14] | `M/…` | «No se mostró», «Sin respuesta» y «—»; `LinesTable`; un monto con su moneda |

**Al cerrar:** `docs/features/formularios.md` suma la sección.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [Meta] | `If` y su expresión, antes de programar: comparar un `Dropdown`, una casilla de `CheckboxGroup`, un número escrito en `TextInput` y una fecha, entre pantallas, con cuánta anidación, y dónde se admite. | Lo que no se pueda, `Check` lo frena al publicar. Los números hoy son texto (§14 de la E14), y puede no haber comparación numérica. |
| [Meta] | Una pregunta de archivo dentro de un `If`, en su pantalla. | Si no se puede saltear, se la frena al publicar con `ConditionNotSupported`. |
| [Meta] | Los renglones: que un `required` no trabe los que sobran. | Cada celda va opcional para Meta, y valida el servidor. |
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

## Decisiones tomadas (no se vuelven a preguntar)

- **Una etapa propia** (54), después de las respuestas, así lo que suma se ve en lo que ya existe (78).
- **Los cálculos se hacen al llegar la respuesta:** quien completa no ve el total en WhatsApp antes de enviar (54).
- **Las líneas llevan un tope de renglones** (54), de 1 a 10.
- **Genérico:** ninguna fórmula ni grupo va escrito en el código.

## Fuera de esta etapa

- Líneas dentro de líneas, y condiciones dentro de un grupo.
- Mostrar el total en WhatsApp antes de enviar.
- Funciones más allá de las cuatro operaciones y la suma de una columna.
- Filtrar la planilla por una columna de líneas.
- Un filtro por destinatario en una columna de catálogo.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba, abriendo cada variante en un celular:
1. «Monto» aparece solo si «¿Se concretó la venta?» es Sí; con No, la respuesta llega sin monto.
2. Un pedido con 3 renglones (producto del catálogo y cantidad), con su total calculado con el precio del catálogo, en la tarjeta del chat.
3. Las dos respuestas en la planilla, el detalle y el Excel: «No se mostró», los renglones y el total.

## Requisitos previos

- La E18 cerrada.
- La prueba con Meta del componente `If`, de los renglones y de una pregunta de archivo dentro de un `If`.

## En el multitenant

- **Datos de la organización:** `FormRecordLineValues` y las columnas nuevas son `ITenantOwned`, con RLS.
- **Workers:** no hay; el cálculo corre en la captura.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos:** los de la E14, sin permisos nuevos.
- **Cuotas:** las rutas cuentan en las cuotas generales.
