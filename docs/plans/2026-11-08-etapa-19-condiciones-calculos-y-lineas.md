# E19 · Condiciones, cálculos y líneas · plan ejecutable

**Estado:** pendiente · Spec: `docs/plans/formularios/19-condiciones-calculos-y-lineas.md` y su parte `19-parte-contrato-datos-y-pasos.md`. Tableros aprobados en el lienzo v175, con el recorrido guiado `un-pedido-con-renglones`, salvo el aviso de una fecha anterior o posterior, que falta en `FRM-Editor-Condiciones` y `FRM-Publicar-19` y se dibuja antes de F2; los que la spec redibuja con el traspaso 18 (262 a 264 y 266) se dibujan antes del front.

| Parte | Tareas | Estado |
| --- | --- | --- |
| Contrato | K0–K2 | Pendiente |
| Back | B1–B10 | Pendiente |
| Front | F1–F7 | Pendiente |

## Forma de trabajo

Modo rápido ([etapa en paralelo](../guides/etapa-en-paralelo.md) y [ADR 0071](../decisions/0071-conductor-en-carriles.md)): contrato primero; después los carriles del back y el front a la vez; la revisión en paralelo, con los tests enteros y los detectores; los arreglos; la última revisión de código y el cierre. No hay puerta por etapa: el E2E real, la galería y el recorrido van en la prueba integral del bloque (abajo). Un arreglo corre solo lo que toca. La tabla de estado la actualiza el cierre del conductor ([conducir una etapa](../guides/conducir-una-etapa.md)). El evaluador, la fórmula, la captura y el Flow arrancan con su test en rojo.

**Revisión adversarial** ([AGENTS.md](../../AGENTS.md)): una vez, en la revisión de la etapa, un revisor por cada tema de riesgo de las tareas **(R)**.

**Reglas de cada chat:** todo en main, sin ramas, worktrees, stash, reset ni push; commits propios con `git commit -- <rutas>`, en español y conventional, con la línea `Tarea: 19-condiciones-calculos-y-lineas/<id>`; con la app levantada, nadie edita el `src` del front; TDD donde hay lógica. Cada tarea corre su test dirigido y el build (en el front, `tsc -b` y el lint de lo que tocó), con `node scripts/arneses/verificar.mjs` en el back y `node scripts/verificar.mjs` en el front; los tests enteros, los de integración y los de seguridad corren una vez, en la revisión de la etapa. El contrato no cambia sin anotarlo en «Cambios al contrato»; ninguna clave real pasa por vos; al usuario, solo lo de producto, por la Guía.

## Qué se verificó al arrancar

| Fila de la spec | Qué se encontró | Qué cambia |
| --- | --- | --- |
| [Meta] Una pantalla por ítem | Probado allá el 6 y el 7/11, con el `routing_model`. **El bloque al final** pasó el 9/11 solo con la pantalla de cierre, y 10 ítems de 600 caracteres con eñes, 34,6 KB, llegaron enteros (traspaso 12, §4). | B4 suma la pantalla de cierre (204). K2 la prueba. La respuesta grande, con número propio, va a la I1 de la E10 (123). |
| [Meta] `If` y su expresión | Allá, «Mostrar si» sobre una sola pregunta (CO1-A), probado el 2026-11-11: una comparación por `If`, sin `&&` ni `\|\|`, y hasta tres anidados. Según Meta (2026-11-08), `>`, `>=`, `<` y `<=` comparan solo números, `==` pide el mismo tipo y `${screen.<pantalla>.form.<campo>}` existe desde Flow JSON 4.0; los números y las fechas del Flow son texto. | B4 («Cómo se arma el Flow» de la parte); lo demás frena, como dibuja `FRM-Publicar-19`. K2 lo prueba. |
| [Meta] Archivo dentro de un `If` | Meta no admite `PhotoPicker` ni `DocumentPicker` en un `If`; allá aceptó un `If` con un `Footer` por rama. | B4 y K2. |
| Allá: renglones | Probado de punta a punta con Meta el 9/11. Los ítems van en una columna `Row`; acá, en `FormRecordRowValues`. Sirven 450 campos, cómo empareja «Pasan al bloque», el mismo ítem para varias condiciones, el Excel por ítem (227) y el cálculo del ítem (233). | B3, B4, B7 y B8 los toman. |
| Datos de referencia | `ICurrencyCatalog.FindAsync` da `MinorUnits` (`int?`); `ValidCurrency()` está en `ValidationRules.cs`; `Money.Round` usa `AwayFromZero`. | B2 (232). |

## Contrato

**Sin rutas, permisos ni acciones del asistente nuevos** (el editor arma una definición entera: 0057 y 0068). Cambian rutas de la E12 a la E16, con `[Access(Access.Business)]` y `[FeatureGate("forms")]`, salvo las del chat, del módulo WhatsApp. Cuotas generales; la concurrencia y la idempotencia, como estaban.

| Método y ruta (`/api/organization/forms`) | Permiso | Qué suma | Errores nuevos |
| --- | --- | --- | --- |
| GET `/{id}` | `forms.read` | tipos `Calculation` y `Rows`; `visibleIf?[]`, `calculation?`, `rows?` y `rowsOf?`; `recipientFilters[]` en el bloque | — |
| PUT `/{id}/draft`, con `version` y 409 | `forms.manage` | lo mismo | `Forms.Draft.ConditionInvalid`, `FormulaInvalid` y `RowsInvalid`, solo de forma: una referencia rota se guarda |
| POST `/{id}/publication-checks` | `forms.publish` | `componentCount`; `changes[].kind` `movedToBlock`; `warnings[]` `RowsOverLimit` (`limit`, `max`) y los de la E16 en el bloque | los de arriba, `Forms.Draft.RowsEmpty`, `WhatsApp.Flow.ConditionNotSupported` y `RowsMinimumOverLimit` |
| POST `/{id}/publications`, `[Idempotent]` | `forms.publish` | vuelve a comprobar | los mismos; de Meta, `WhatsApp.Flow.ValidationUnlocated` |
| GET `/{id}/records/columns` | `formrecords.read` | tipos nuevos y `currency?`; la columna del bloque con `questions[]` | — |
| POST `/{id}/records/search`, `facets`, `facet-options` y `/{recordId}/navigation` | `formrecords.read` | valores nuevos; `notShown`; `sort` por un cálculo; una pregunta del bloque, en algún ítem, y varias, en el mismo | `Forms.RecordFilter.Invalid` sobre el bloque o al ordenar por sus preguntas |
| GET `/{id}/records/{recordId}` | `formrecords.read` | `rows`, con todos los ítems | — |
| POST `/{id}/records/export` | `formrecords.export` | `rowsPer` (`item` o `record`): una fila por ítem; «No se mostró» | `Forms.RecordExport.TooManyRows` |
| POST `/{id}/records/dashboard` | `formrecords.read` | `numbers[]` con los cálculos y `currency?` | — |
| GET y PUT `/catalogs/{id}`, GET `/catalogs/{id}/used-by` | los de la E12 a la E14 | `FieldInUse`, `ItemInUse` y «Lo usan» cuentan las fórmulas y el bloque | — |
| GET `/api/organization/whatsapp/{customers\|internal}/conversations/{id}/forms` | «Atender internos» o «Atender clientes» | `optionCounts` y `offerProblems` con el bloque | — |
| Mensajes del chat: la tarjeta `form` | los de la E14 | los ítems y los cálculos | — |

Cuerpos, valores y puertos: los de la parte de la spec, «Contrato», más esto: `componentCount` es el de la pantalla más cargada; en el Excel, un monto lleva su moneda en el encabezado («Total (ARS)»).

**Textos de error,** en `FormsTexts.resx` y `.en.resx`, con los códigos en `FormDraftErrors.cs` y `FormRecordErrors.cs` [E14] y `FormRecordExportErrors.cs` [E15]; los de `WhatsApp.Flow`, en `D/WhatsApp/WhatsAppErrors.cs` y `WhatsAppTexts.resx`:

| Código | es | en |
| --- | --- | --- |
| `Forms.Draft.ConditionInvalid` | Revisá la condición: usá una pregunta anterior que sirva, una opción que exista y hasta 10 condiciones. | Check the condition: use an earlier question that fits, an option that exists and up to 10 conditions. |
| `Forms.Draft.FormulaInvalid` | Revisá la fórmula: le falta un dato o usa uno que ya no está. | Check the formula: a value is missing or no longer exists. |
| `Forms.Draft.RowsInvalid` | Revisá el bloque: su mínimo y su máximo, sus preguntas o su catálogo. | Check the block: its minimum and maximum, its questions or its catalog. |
| `Forms.Draft.RowsEmpty` | Agregá al menos una pregunta al bloque. | Add at least one question to the block. |
| `WhatsApp.Flow.ConditionNotSupported` | WhatsApp no puede mostrar esta condición. | WhatsApp can't show this condition. |
| `WhatsApp.Flow.RowsMinimumOverLimit` | En WhatsApp se cargan hasta {0} ítems; el mínimo es {1}. | WhatsApp loads up to {0} items; the minimum is {1}. |
| `Forms.Record.RowsInvalid` | Los ítems no cumplen el mínimo, el máximo o una respuesta obligatoria. | The items don't meet the minimum, the maximum or a required answer. |

`WhatsApp.Flow.ValidationUnlocated` y `Forms.RecordExport.TooManyRows` llevan los de «Los textos del bloque» de la spec. El front arma cada problema con su ruta y sus datos (T26 de la E14), con los textos de `FRM-Publicar-19` y de esa tabla.

### K0 · Lo que dejó la etapa anterior

El chat del contrato confirma cada nombre en el código; lo que difiera va a «Cambios al contrato» antes de programar.
- **E14:** `FormQuestionType`, `FormDraft` (`titles[]` con `start`), `FormVersion` y `FormRecordValues` (clave `(TenantId, RecordId, FieldId, Position)`, `NumberValue numeric(28,10)`); `FormDraftEditor`, `FormVersionCreator`, `FormPublicationService`, `FormRecordService.CaptureAsync`, `FormRecordWriter`, `FormOfferBuilder` y `FormRecordSource`; `WhatsAppFlowBuilder`, `WhatsAppFlowReplyVerifier` (16 KiB, 50 campos), `WhatsAppStructuredFormChannel.Check` y `WhatsAppFlowBindings.Profile`; `StructuredFormVersion`, `StructuredRecordCapture`, `StructuredRecordCard` y `StructuredSendableForm`; `issues[]` y `changes[]`; `Forms.Record.InvalidResponse`, `WhatsApp.Flow.TooManyComponents`, `ResponseTooLarge`, `Forms.Catalog.FieldInUse` e `ItemInUse`; `FormsAccountDeletionParticipant`, `FormCard` y el Meta simulado (`TestFeatures/WhatsAppBrowser/`); en el front, `QuestionEditor.tsx`, `PublishDialog.tsx`, `FormPreviewPhone.tsx` y `FormMessageCard.tsx`.
- **E15:** `FormRecordCondition` y `FormRecordFiltersPolicy`, con la tabla de verdad de `unanswered` y `notAsked`; `FormRecordQueryFilters`, `FormRecordReader`, `FormRecordQueryService`, `FormRecordColumnsBuilder`, `FormRecordSheetBuilder`, `IFormRecordSpreadsheet`, `FormRecordExportService` y `FormDashboardBuilder`; la hoja «Respuestas» en los recursos; el índice trigram y `SearchIndexTests`; los valores `{kind}`; en el front, `FormRecordsPage.tsx`, `lib/values.ts`, `RecordFiltersMenu.tsx`, `QuickView.tsx`, `RecordValues.tsx`, `RecordFullPage.tsx`, `ExportDialog.tsx`, `FormDashboard.tsx` y la forma de solo lectura de una respuesta, `lib/recordForm.ts` y `RecordForm.tsx` (266).
- **E16:** `FormRecipientFilterPolicy`, `recipientFilters[]` (`fixed`, `recipient`), `warnings[]`, `offerProblems`, `CatalogQuestionFields.tsx` y la zona «Avisos».
- **E17:** `FormDefinitionCopyCreator`, que copia el borrador con sus identidades, y las lecturas que parten de la definición.
- **E18:** el perfil `flow-static-7.3-r10`, el último, donde cada pregunta de archivo abre su pantalla y las siguientes la comparten; 64 KiB con archivos, y los tipos `Photo` y `Document`.
- **E12 y E13:** `FormsContractContributor.cs`, `fields[].id`, con su tipo numérico, y `parseDecimalText`.
- **Base 2:** `numberAmbiguous` (207), la pieza `Picker` (262) y el orden por columnas (264).

### K1 · Publicar el contrato

El chat del contrato publica lo que cambia, genera `docs/contracts/openapi.json` y exporta ejemplos reales de cada cuerpo nuevo, con cada problema y cada aviso. El front corre `npm run contracts`.

### K2 · Lo que falta probar con Meta

En el Flow Builder de la cuenta de desarrollo de la E11, sin la app ni claves: el chat escribe los Flow JSON en `.local/` y el usuario los sube y recorre la vista previa, con pasos clic por clic. B4 da por hecho:
1. `==` de un `Dropdown`, de un `RadioButtonsGroup` y de un `CalendarPicker` contra un texto fijo y contra un `${data.…}`;
2. `!= ''` y `== ''` en un `TextInput`, un `Dropdown` y un `CalendarPicker` opcionales vacíos;
3. `${screen.<pantalla>.form.<campo>}` desde una pantalla posterior;
4. un `CalendarPicker` dentro de un `If`, y tres `If` anidados, sin `else`;
5. la marca `<clave>_shown`, fijada con `update_data` por las opciones de un `RadioButtonsGroup` y de un `Dropdown`, leída en la misma pantalla y en una posterior;
6. un `If` con un `Footer` por rama que saltea una pantalla con `PhotoPicker`, con su `routing_model`, también con la foto al final, donde la rama que la saltea va a la pantalla de cierre;
7. el bloque al final: el «No» y el último ítem navegan a la pantalla de cierre, y ninguna pantalla de ítem manda.

Cada Flow se publica en la cuenta de desarrollo y vale solo sin `validation_errors`. Lo que no dé así va a «Cambios al contrato» antes de B4, y esa combinación frena con `ConditionNotSupported`.

**Texto para el chat del Contrato**

```text
Leé AGENTS.md, este plan y la spec de la E19 con su parte, con las decisiones 264 y 266 de la hoja de ruta. K0: confirmá cada nombre de la lista en el código y anotá lo que difiera en «Cambios al contrato». K1: publicá el contrato, generá openapi.json, exportá los ejemplos reales y avisá a la Guía. K2: vos escribís los Flow JSON en .local/ y el usuario los sube y los publica en la cuenta de desarrollo, con tus pasos clic por clic; un Flow vale solo sin validation_errors; no manejás su PC. Reglas: las de «Forma de trabajo» de este plan.
```

## Back

Cada tarea copia su molde, con `Result` y ProblemDetails, `OperationLog.RunAsync`, `[LoggerMessage]` (nunca una fórmula con datos ni un valor), `TimeProvider`, textos es/en y una UoW por escritura. Forms y WhatsApp se hablan solo por los puertos de `StructuredData`. Ninguna fórmula ni bloque va en el código, y nada que escriba una persona se ejecuta como SQL (ADR 0053).

1. **B1 · Condiciones en memoria.** `D/Forms/FormConditionEvaluator.cs`, junto a `FormRecordCondition.cs` [E15], que suma `notShown`. Copia `FormRecordFiltersPolicy.cs` [E15]. Tests: cada operador por tipo; en cadena; `unanswered` sobre una oculta se cumple; una tabla de casos que B7 corre también en SQL.
2. **B2 · La fórmula.** `D/Forms/FormFormula.cs` y `FormFormulaEvaluator.cs`: precedencia, todo en `decimal`. Cada cálculo, también el de cada ítem, se redondea al par (`MidpointRounding.ToEven`, 232) a sus decimales sellados, y el que suma otros suma lo ya redondeado, sin tocar `Money.Round`. «Demasiado grande» es más de 13 dígitos enteros en un monto y de 15 en un número (T9 de la E14). Copia `D/ValueObjects/Money.cs`. Tests: los de B2 de la spec, más un dato de una oculta, el importe de cada ítem con el campo del catálogo elegido en él y un ítem sin el dato, que no cuenta.
3. **B3 · Las preguntas en el borrador (R: datos, por los bloqueos).** Suma a `FormQuestionType.cs`, `FormDraft.cs`, `FormDraftEditor.cs`, `FormVersionCreator.cs` y al control de `FieldInUse` [E14]; `A/Modules/Forms/Services/FormLogicPolicy.cs`. Copia `FormRecipientFilterPolicy.cs` [E16]. Decisiones:
   - una condición no usa un cálculo ni una pregunta del bloque, y ni el bloque ni sus preguntas llevan «Mostrar si»;
   - un cálculo del bloque usa solo lo de su ítem, y uno de afuera, una pregunta del bloque solo en una suma (233); una fórmula no mezcla montos de dos monedas;
   - un monto guarda `decimals` igual a los `MinorUnits` de su moneda, y una sin `MinorUnits` es `FormulaInvalid`; cambiar la moneda, o pasar de número a monto, da otra identidad (como T17);
   - `ItemInUse` cuenta una opción elegida en un ítem (129) y suma a `uses: forms` las condiciones; guardar un catálogo con opciones por borrar toma antes el bloqueo de las definiciones, en el orden de la E16, y una condición con una opción de catálogo que no existe es `ConditionInvalid`;
   - el `start` de un título cuenta el bloque como una pregunta, sin las suyas, así un título nunca cae adentro; pasar una pregunta al bloque o sacarla corre los `start` siguientes.

   Tests: cada motivo de los tres errores; borrar una opción mientras se guarda una condición que la nombra, con dos actores; la referencia rota se guarda y frena al publicar; un campo de una fórmula no se quita; «Lo usan» con la fórmula y el catálogo del bloque; pasar una pregunta al bloque y sacarla, con otra identidad y los títulos en su lugar.
4. **B4 · Publicar y el Flow, con `flow-static-7.3-r11` (R: Meta).** Suma a `WhatsAppFlowBuilder.cs`, `Check` de `WhatsAppStructuredFormChannel.cs`, los topes de `WhatsAppFlowReplyVerifier.cs`, `FormOfferBuilder.cs` y `FormPublicationService.cs` [E14]. Copia esos mismos.
   - Sin condiciones ni bloque, el Flow sale con los bytes de `r10` (E18); con alguno, con `r11`.
   - «Mostrar si» y los títulos siguen «Cómo se arma el Flow» de la parte, sin copiar el código de allá (18): **una comparación por `If`, nunca `&&` ni `||`, y hasta tres `If` anidados**. Lo que no entra es `ConditionNotSupported`, con los casos de la parte.
   - Una pregunta de archivo con «Mostrar si» va sola en su pantalla, y la anterior la saltea con un `If` de un `Footer` por rama; si la anterior ya ramifica (un ítem u otra pantalla que se saltea), es `ConditionNotSupported`.
   - El bloque: una pantalla por ítem hasta 10 o el máximo, con el mismo `name`; «¿Agregás otro ítem?» desde el mínimo, con un `Footer` por rama; el último no pregunta; con mínimo 0, «¿Agregás un ítem?». El «No» y el último ítem van a lo de después del bloque. El `routing_model` declara cada `navigate`, con las dos ramas. Los títulos de los ítems, «¿Agregás…?» y el texto de la pantalla de cierre viajan en `data`, en el idioma de quien recibe (T6).
   - **Ninguna pantalla que manda navega en otra rama** (Meta lo rechaza): si el bloque o una pregunta de archivo salteada es lo último, esa rama va a la **pantalla de cierre** (204). Ninguna pantalla de ítem manda.
   - Topes: 50 componentes por pantalla, contando cada `If`; 64 KiB con bloque, con cada pregunta una vez por ítem y su clave `_r<n>`; 450 campos en la respuesta.
   - Un `validation_errors` que no cae en una pregunta es `ValidationUnlocated`, y la verificación lee los dos `GET` de la parte con el token de la organización.
   - El Meta simulado [E14] arma respuestas con ítems, «No», el bloque al final y ocultas, para el E2E; no prueba a Meta (K2 y la I1 de la E10).

   Tests: los de B4 de la spec, más cada combinación de condiciones; ningún control en dos ramas; el cuarto `If` en cadena; la marca en cada `navigate`; el archivo salteado al final, que termina en la pantalla de cierre; los bytes sin condiciones ni bloque, y la pantalla salteada.
5. **B5 · Tabla, columnas y migración (R: datos, migración y aislamiento).** `D/Forms/FormRecordRowValue.cs`, `I/Modules/Forms/Persistence/Configurations/FormRecordRowValueConfiguration.cs` y `<fecha>_FormLogic.cs`, con los datos de la spec; suma a `FormRecordConfiguration.cs` (`HiddenFieldIds`) y `FormRecordValueConfiguration.cs` (`Currency`) [E14]. Copia `FormRecordValueConfiguration.cs` [E14]. `Currency` es `char(3)`, con el CHECK `Currency IS NULL OR NumberValue IS NOT NULL`, también en `FormRecordRowValues`, que lleva los índices de `FormRecordValues`, con `(TenantId, CatalogId, ChoiceId)` y el trigram de los textos largos. El `Down` se niega si hay ítems, ocultas o montos. Tests: aislamiento y RLS; ida y vuelta, y el `Down` que se niega; la cantidad en la fila del bloque; `SearchIndexTests`; `DecimalPrecisionTests`.
6. **B6 · La captura (R: datos).** `A/Modules/Forms/Services/FormRecordLogicPolicy.cs`; suma a `FormRecordService.cs`, `FormRecordWriter.cs`, `WhatsAppFlowReplyVerifier.cs` y `FormsAccountDeletionParticipant.cs` con su cleanup [E14]. Copia esos mismos. Orden: visibilidad, descarte, ítems, sellado y cálculo, primero los de cada ítem. Tests: los de la captura en la spec, más un cálculo imposible que queda «Sin respuesta» sin frenar la respuesta, `HiddenFieldIds` y la baja de una cuenta, también en `ChoiceLabel` de los ítems.
7. **B7 · Lo que se lee.** Suma a `FormRecordColumnsBuilder.cs`, `FormRecordReader.cs`, `FormRecordQueryFilters.cs` y `FormRecordQueryService.cs` [E15]; los ítems se leen desde la definición (E17), y la forma de la versión del detalle suma `calculation` y `rowsOf` (266). Copia esos mismos. Tests: los de B7 de la spec, con la tabla de B1; ordenar por el bloque es `Forms.RecordFilter.Invalid`; una copia del formulario, con los mismos `FieldId`, no mezcla sus ítems; EXPLAIN con 10.000 respuestas de 10 ítems.
8. **B8 · Excel y tablero (R: archivos).** Suma a `FormRecordExportService.cs`, `FormRecordSheetBuilder.cs`, `FormRecordSpreadsheet.cs` y `FormDashboardBuilder.cs` [E15]. Copia esos mismos. Una sola hoja (227); con `rowsPer` `record`, o sin el bloque entre las columnas, una fila por respuesta, sin las del ítem. Las filas se cuentan con una consulta antes de escribir: más de 100.000 es `TooManyRows`. Tests: cada forma de la fila; 100.000 y 100.001 filas, sin escribir; la memoria con 100.000 (allá, 11 s y 0,4 GB); el monto; «No se mostró»; el total en su moneda; los bytes abiertos con ClosedXML.
9. **B9 · La tarjeta y «Mensajes» (R: permisos).** Suma a `FormRecordSource.cs` y a `FormCard` [E14]. Copia esos mismos. Tests: un ítem por línea, numerado, con « · » y sin las vacías; un ítem vacío, solo con su número; los cálculos; sin `formrecords.read`, solo el número.
10. **B10 · Contratos y ejemplos.** Suma a los contratos de la E14 y la E15, sin archivos nuevos, a `FormsContractContributor.cs` y a `A/Models/StructuredData/StructuredFormVersion.cs`, `StructuredRecordCapture.cs` y `StructuredRecordCard.cs` [E14]. Tests: los ejemplos de K1; copiar conserva las condiciones, los cálculos y el bloque, con sus identidades; `ResourceParityTests`.

**Texto para el chat del Back**

```text
Leé AGENTS.md, este plan, la spec de la E19 con su parte, las decisiones 264 y 266 de la hoja de ruta y las fichas de su «Qué leer». Esperá K0 y K1, y K2 antes de B4. Hacé B1 a B10 en orden, copiando el molde de cada tarea; no toques el front ni la tabla de estado del plan. La revisión adversarial de las tareas (R) va después, en la revisión de la etapa. Reglas: las de «Forma de trabajo» de este plan.
```

## Front

Las pantallas copian los tableros v175, con i18n en es y en, tipos generados, `shared/format` y TanStack Query; el front no calcula, muestra. Todo texto dice «ítem» («item»). Tests con MSW, los ejemplos exportados, zod estricto y axe.

1. **F1 · Tipos y llamadas.** Suma a `F/src/modules/forms/api/forms.ts` [E14] y `records.ts` [E15], y a `F/src/modules/whatsapp/chats/api/chats.ts` [E14]. Copia la API de la E18. Tests: zod estricto con los ejemplos de K1, también la columna del bloque y los valores `hidden`, `money` y `rows`.
2. **F2 · «Mostrar si».** `components/logic/ConditionsEditor.tsx` y `lib/conditions.ts`, que evalúa como B1. Tableros `FRM-Editor-Condiciones` y su teléfono. Tests: pregunta, condición y valor en la pieza (262); solo las anteriores que sirven; el aviso de WhatsApp debajo de la condición, con los casos de `ConditionNotSupported` que se ven en el borrador y el texto de su `reason`, también una fecha anterior o posterior (235); «Agregar condición» grisado con «No hay preguntas anteriores que sirvan.»; la referencia rota, con «Corregir»; hasta 10; «1.500» marca `numberAmbiguous` en su campo (207); `FormPreviewPhone.tsx` muestra y oculta; la tabla de casos de B1.
3. **F3 · La pregunta «Cálculo».** `components/logic/CalculationEditor.tsx`, `lib/formula.ts` y `lib/decimal.ts`, la aritmética decimal exacta con el redondeo al par (232), que usa la vista previa de la E20; lo escrito, acá o en una condición, se lee con `parseDecimalText` de `shared/format/` [E12] (207), con los dígitos de su pregunta (T9), y `lib/decimal.ts` opera sobre ese texto. Tableros `FRM-Editor-Calculo` y su teléfono. Tests: piezas y «Agregar dato ▾»; «1.500» ambiguo en un número fijo; en el bloque, solo lo de su ítem; la pregunta, la suma y la moneda en la pieza, sin diálogo (262), también con un cálculo del ítem; «Falta un dato después de «+».» y el dato quitado; «Es un monto», con la moneda de los datos de referencia; el teléfono no la muestra.
4. **F4 · El bloque.** `components/logic/RowsBlockEditor.tsx`; suma «Agregar bloque» a la banda, «Se carga varias veces» a `QuestionEditor.tsx` y la pantalla de un ítem a `FormPreviewPhone.tsx` [E14]. Tableros `FRM-Editor-Lineas` y su teléfono, y «Pasar al bloque» en `FRM-Editor-Condiciones`. «46 de 50 componentes» sale de `componentCount` del último `publication-checks`, desde 45: el front no cuenta. Tests: los de F4 de la spec, con «Ya hay un bloque.» y «¿Pasar «Monto» al bloque?».
5. **F5 · Publicar.** Suma a `PublishDialog.tsx` [E14 y E16]. Tablero `FRM-Publicar-19`. Tests: los de F5 de la spec, con cada `reason` de `ConditionNotSupported`.
6. **F6 · Planilla, detalle, tablero y tarjeta.** El bloque, en `components/records/RecordRowsTable.tsx` y `RecordRowsCards.tsx`, nuevos y de solo lectura, sobre `Table` de `table.tsx`, que usa `RecordForm.tsx`. Suma a `FormRecordsPage.tsx`, `RecordFiltersMenu.tsx`, `QuickView.tsx`, `RecordFullPage.tsx`, `lib/recordForm.ts`, `RecordForm.tsx`, `RecordValues.tsx`, `FormDashboard.tsx` y `lib/values.ts` [E15], y a `FormMessageCard.tsx` [E14]. Tableros `FRM-Respuestas-19`, `FRM-Respuesta-19`, `FRM-Tablero-19` y `WA-Chats-Respuesta-19`, con sus teléfonos. Tests: los de F6 de la spec, más el número del ítem sin encabezado y «Ítem» para el lector de pantalla; los datos del catálogo como columna; el recuadro también en la vista rápida, sin desplazarse de costado, y la tarjeta.
7. **F7 · Exportar.** Suma «Filas» a `ExportDialog.tsx` [E15]. Tablero `FRM-Respuestas-Exportar-19`. Tests: «Filas» en la pieza (262), con «Una por ítem de Productos» y «Una por respuesta»; el chip «Estado: Dañado (en algún ítem de Productos)»; `TooManyRows` con su texto.

**Texto para el chat del Front**

```text
Leé AGENTS.md del front, ../ArquitecturaBaseMutitenant/docs/plans/2026-11-08-etapa-19-condiciones-calculos-y-lineas.md y la spec de la E19 con su parte, con las decisiones 262 a 264, 266 y 269 de la hoja de ruta. Esperá K1 y corré npm run contracts. Hacé F1 a F7 contra los tipos generados, copiando los tableros v175 o los redibujados con el traspaso 18; no toques el lienzo, las specs, el back ni la tabla de estado del plan. Al cerrar la parte, también npm run build. Reglas: las de «Forma de trabajo» de este plan.
```

## Revisión y cierre

Sin tareas: los hace el conductor ([conducir una etapa](../guides/conducir-una-etapa.md)). La lista de control de la revisión suma el riesgo de la [prueba integral de ArquitecturaBase](../backlog.md#lo-que-vio-la-prueba-integral-de-arquitecturabase): la vista previa del formulario aplica las condiciones (O17). Al cerrar, `docs/features/formularios.md` suma la sección; `docs/features/whatsapp.md`, el perfil `r11`, y nace el `AGENTS.md` de `components/logic/` en el front.

**Pendiente para la I1 de la E10.** Con el número de una organización de prueba: las pruebas de «Cómo se cierra» de la spec, con el bloque al final, una respuesta de unos 40 KB y, en un celular, una pregunta condicional y una pantalla de archivo salteada.

## Al cierre del bloque E12–E25

Lo que la prueba integral recorre de esta etapa. Cuando el cierre retira el plan, esta sección pasa a `docs/backlog.md`, a «Para después de la etapa 19-condiciones-calculos-y-lineas».

- **E2E real** (casos en `F/scripts/test-e2e-real-forms.mjs` [E12]), con «Pedido de materiales» y el Meta simulado: publicar; capturar con 3 ítems, con uno y «No», con mínimo 0 y «No», con el bloque al final y con «Monto» oculto; verlas en la planilla, el detalle, el Excel, con una fila por ítem, y el tablero; copiar el formulario.
- **Recorrido:** `un-pedido-con-renglones`, sumado a `F/scripts/recorrido-usuario-forms.mjs` [E12].
- **Galería** (`scripts/capturas/etapa-19.json`), a 1440, 390 y 320: los tableros de la etapa.

## Relación con otras etapas

**Usa de las anteriores:** lo de K0, la gramática C5 de la E12 y, de la base, los botones que trabajan (43 y 115), `PageLoading`, `FormSkeleton` y la escala (180). No suma fuentes a la campana.

**Deja para las siguientes:**
- **Borrador y versión:** los tipos `Calculation` y `Rows`; `visibleIf`, `calculation` (con `catalogFieldId`), `rows` y `rowsOf`.
- **Back:** `FormConditionEvaluator` (en memoria, sin ítems), `FormFormula`, `FormFormulaEvaluator`, `FormRecordRowValue`, `FormLogicPolicy` y `FormRecordLogicPolicy`; la tabla `FormRecordRowValues`, `FormRecords.HiddenFieldIds` y `FormRecordValues.Currency`.
- **Lectura:** los valores `hidden`, `money` y `rows`; `notShown`; «En algún ítem de…» en `FormRecordQueryFilters`, con varias condiciones en el mismo ítem.
- **Puertos y Flow:** `StructuredFormVersion` con `VisibleIf`, `Calculation` y `Rows`; `StructuredRecordCapture.Rows`; `StructuredRecordCard` con ítems; `flow-static-7.3-r11`, el último hasta la E21; el Meta simulado con ítems.
- **Errores:** los nueve de la tabla del contrato, el aviso `RowsOverLimit` y `movedToBlock`.
- **Front:** `components/logic/ConditionsEditor.tsx`, `CalculationEditor.tsx` y `RowsBlockEditor.tsx`; `components/records/RecordRowsTable.tsx` y `RecordRowsCards.tsx`, de solo lectura; `lib/conditions.ts`, `lib/formula.ts` y `lib/decimal.ts`; los textos del bloque en `forms.json`.
- **Para quién:** la E20 carga con `lib/conditions.ts` y la captura del bloque, también el cálculo de cada ítem; la E21 suma «algún ítem» en memoria, con la regla de B7, lee `FormRecordRowValues`, pone el cálculo del ítem en el PDF como una pregunta más del bloque y suma `r12`; la E22 copia la fórmula por `catalogFieldId`; la E23 saltea el bloque al prellenar, y tampoco prellena una pregunta con «Mostrar si» ni la que fija una marca, que arranca en `false` y un valor prellenado no cambia; la E25 copia `FormLogicPolicy.cs`.

## Cambios al contrato


## Para decidir

Nada: lo del traspaso 15 son las decisiones 232, 233 y 235.
