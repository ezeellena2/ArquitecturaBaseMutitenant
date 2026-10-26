# E19 · Condiciones, cálculos y líneas (resumen)

**Estado:** resumen de la etapa 19 del bloque (decisiones 53, 54 y 78), después de la E18. No tiene equivalente en ArquitecturaBase. **Antes de planificar** se prueba con Meta cómo se ven en el celular una pregunta que aparece según otra, un grupo de renglones y una pregunta de archivo en su pantalla dentro de un `If`, y el plan parte de ese resultado.

## Qué se construye

- **Back:**
  - **Preguntas condicionales:**
    - una pregunta puede tener «Mostrar si», con hasta 10 condiciones unidas con «y», con la gramática de condiciones de la E12 (C5) sobre preguntas anteriores: sí/no (`is`), elegir una (`in`), elegir varias (`anyOf`, «alguna de»), número y fecha (`equals`, `gte`, `lte`) y respondida o no (`answered`, `unanswered`);
    - en WhatsApp, la pregunta aparece o se oculta mientras la persona completa el formulario (el componente `If` del Flow) **[Meta]**;
    - **el servidor vuelve a evaluar las condiciones al capturar:** nunca confía en lo que llega. Una pregunta oculta no guarda valor, y «obligatoria» vale solo si se mostró;
    - **«No se mostró»:** la respuesta guarda qué preguntas quedaron ocultas (`FormRecords.HiddenFieldIds`). En la planilla de la E15 se ven como «No se mostró», distintas de «Sin respuesta» y de «—» (no existía en esa versión), y el filtro suma `notShown`.
  - **Cálculos:**
    - un tipo de pregunta «Cálculo», que no se ve en WhatsApp. Su fórmula usa números de la misma respuesta, campos de las opciones elegidas en un catálogo (por ejemplo, el precio de un producto, E13) y la suma de una columna de las líneas;
    - **solo las cuatro operaciones** y paréntesis, armadas en el editor sin escribir código;
    - se calcula **al llegar la respuesta,** con los valores sellados, en `decimal` y con redondeo `AwayFromZero` ([números y moneda](../../rules/numeros-y-moneda.md)). Si es un monto, el formulario fija su moneda, que sale de los datos de referencia;
    - el resultado se ve en la tarjeta del chat, y en la planilla, el detalle, el Excel y el tablero de la E15, como un número que se filtra;
    - una división por cero o un dato que falta dejan el cálculo «Sin respuesta», nunca un error de la captura.
  - **Líneas:**
    - un tipo de pregunta «Grupo de líneas» con sus columnas: texto, número, fecha, sí/no, elegir una y elegir de un catálogo;
    - **un tope de renglones** que elige el formulario, de 1 a 10;
    - en WhatsApp, renglones que se completan uno debajo del otro, con los que sobran vacíos y opcionales **[Meta]**. Cuentan en el tope de 50 componentes y en el presupuesto de 16 KiB de la E14;
    - la respuesta guarda cada renglón con su posición. Un renglón vacío no se guarda;
    - en la tarjeta del chat se ven los renglones. En la planilla, la columna de un grupo dice cuántos renglones tiene; el detalle los muestra en una tabla; el Excel suma una hoja «Líneas», con un renglón por línea y el número de la respuesta.
  - **Copiar un formulario** (E17) copia las condiciones, los cálculos y las líneas.
- **Front:**
  - en el editor: «Mostrar si» en cada pregunta, la pregunta «Cálculo» con su fórmula y el «Grupo de líneas» con sus columnas y su tope;
  - en el teléfono al costado: la pregunta que aparece y los renglones;
  - en la planilla, el detalle y el Excel: «No se mostró», la tabla de líneas y los cálculos.
- **Tableros que dibuja Diseño,** con nuestras piezas y la decisión 43:
  - `FRM-Editor-Condiciones`: «Mostrar si» en una pregunta;
  - `FRM-Editor-Calculo`: la pregunta «Cálculo» y su fórmula;
  - `FRM-Editor-Lineas`: el grupo de líneas, sus columnas y su tope;
  - `FRM-Respuesta-Lineas`: la tabla de líneas y los cálculos en el detalle, y «No se mostró» y las columnas nuevas en la planilla;
  - y sus teléfonos.

## Decisiones tomadas (no se vuelven a preguntar)

- **Una etapa propia** (54), después de las respuestas, así lo que suma se ve en la planilla, el detalle y el Excel que ya existen (78).
- **Los cálculos se hacen al llegar la respuesta:** quien completa no ve el total en WhatsApp antes de enviar (54).
- **Las líneas llevan un tope de renglones** (54).
- **Genérico:** ninguna fórmula ni grupo va escrito en el código; los arma cada organización.

## Fuera de esta etapa

- Líneas dentro de líneas, y condiciones dentro de un grupo de líneas.
- Mostrar el total en WhatsApp antes de enviar.
- Funciones más allá de las cuatro operaciones y la suma de una columna.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba, abriendo cada variante en un celular:
1. «Monto» aparece solo si «¿Se concretó la venta?» es Sí; con No, la respuesta llega sin monto.
2. Un pedido con 3 renglones (producto del catálogo y cantidad), con su total calculado con el precio del catálogo, en la tarjeta del chat.
3. Las dos respuestas en la planilla, el detalle y el Excel: «No se mostró», los renglones y el total.

## Requisitos previos

- La E18 cerrada.
- La prueba con Meta del componente `If`, de los renglones y de una pregunta de archivo dentro de un `If`.

## Ajustes ya decididos

- **Idioma:** el contenido no tiene idioma (35).
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** las líneas y los cálculos van en las tablas de la E14, `ITenantOwned` con RLS.
- **Workers:** no hay; el cálculo corre en la captura.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos:** los de la E14, sin permisos nuevos.
