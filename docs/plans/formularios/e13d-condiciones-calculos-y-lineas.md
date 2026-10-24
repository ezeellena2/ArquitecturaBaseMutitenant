# E13d · Condiciones, cálculos y líneas (resumen)

**Estado:** resumen de una etapa nueva del bloque (decisiones 53 y 54), después de la E13c. No tiene equivalente en ArquitecturaBase. **Antes de planificar** se prueba con Meta cómo se ven en el celular una pregunta que aparece según otra y un grupo de renglones, y el plan parte de ese resultado.

## Qué se construye

- **Back:**
  - **Preguntas condicionales:**
    - una pregunta puede tener «Mostrar si», con hasta 10 condiciones unidas con «y», con la gramática de la E15 sobre preguntas anteriores: sí/no, elegir una o varias («alguna de»), número y fecha;
    - en WhatsApp, la pregunta aparece o se oculta mientras la persona completa el formulario (el componente `If` del Flow) **[Meta]**;
    - **el servidor vuelve a evaluar las condiciones al capturar:** nunca confía en lo que llega. Una pregunta oculta no guarda valor, y «obligatoria» vale solo si se mostró;
    - en la planilla, una pregunta que no correspondía se ve como «—», igual que una que no existía en esa versión (`notAsked`).
  - **Cálculos:**
    - un tipo de pregunta «Cálculo», que no se ve en WhatsApp. Su fórmula usa números de la misma respuesta, campos de las opciones elegidas en un catálogo (por ejemplo, el precio de un producto, E13b) y la suma de una columna de las líneas;
    - **solo las cuatro operaciones** y paréntesis, armadas en el editor sin escribir código;
    - se calcula **al llegar la respuesta,** con los valores sellados, en `decimal` y con redondeo `AwayFromZero` ([números y moneda](../../rules/numeros-y-moneda.md)). Si es un monto, el formulario fija su moneda, que sale de los datos de referencia;
    - el resultado se ve en la planilla, el detalle, el Excel y el PDF, y las reglas de la E16 lo usan en sus condiciones;
    - una división por cero o un dato que falta dejan el cálculo «Sin respuesta», nunca un error de la captura.
  - **Líneas:**
    - un tipo de pregunta «Grupo de líneas» con sus columnas: texto, número, fecha, sí/no, elegir una y elegir de un catálogo;
    - **un tope de renglones** que elige el formulario, de 1 a 10;
    - en WhatsApp, renglones que se completan uno debajo del otro, con los que sobran vacíos y opcionales **[Meta]**. Cuentan en el tope de 50 componentes y en el presupuesto de 16 KiB de la E12;
    - la respuesta guarda cada renglón con su posición. Un renglón vacío no se guarda;
    - en la planilla, la columna dice cuántos renglones tiene; el detalle los muestra en una tabla; el Excel suma una hoja «Líneas», con un renglón por línea y el número de la respuesta.
- **Front:**
  - en el editor: «Mostrar si» en cada pregunta, la pregunta «Cálculo» con su fórmula y el «Grupo de líneas» con sus columnas y su tope;
  - en «Así lo ve el empleado»: la pregunta que aparece y los renglones;
  - en el detalle y en el PDF: la tabla de líneas y los cálculos.
- **Tableros que dibuja Diseño,** con nuestras piezas y la decisión 43:
  - `FRM-Editor-Condiciones`: «Mostrar si» en una pregunta;
  - `FRM-Editor-Calculo`: la pregunta «Cálculo» y su fórmula;
  - `FRM-Editor-Lineas`: el grupo de líneas, sus columnas y su tope;
  - `FRM-Respuesta-Lineas`: la tabla de líneas y los cálculos en el detalle;
  - y sus teléfonos.

## Decisiones tomadas (no se vuelven a preguntar)

- **Una etapa propia,** después de la E13c, porque un cálculo usa los campos de los catálogos (54).
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
2. Un pedido con 3 renglones (producto del catálogo y cantidad), y su total calculado con el precio del catálogo, en la planilla, el Excel y el PDF.
3. Una regla que avisa a un rol si el total pasa de un monto.

## Requisitos previos

- La E13c cerrada.
- La prueba con Meta del componente `If` y de los renglones.

## Ajustes ya decididos

- **Idioma:** el contenido no tiene idioma (35).
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** las líneas y los cálculos van en las tablas de la E12, `ITenantOwned` con RLS.
- **Workers:** no hay; el cálculo corre en la captura.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos:** los de la E12, sin permisos nuevos.
