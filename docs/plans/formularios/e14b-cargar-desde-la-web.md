# E14b · Cargar una respuesta desde la web (resumen)

**Estado:** resumen de una etapa nueva del bloque (decisión 73), después de la E14. No tiene equivalente en ArquitecturaBase.

## Qué se construye

- **Back:**
  - **Un canal más para responder:** un empleado completa un formulario desde la web, a su nombre, como si fuera por WhatsApp. La respuesta guarda el canal «web».
  - **Quién:** un miembro activo con `formrecords.create`, y solo formularios que son para empleados o para los dos (50). Un cliente sigue respondiendo por WhatsApp.
  - **La misma captura:** valida contra la versión activa sellada, con las mismas reglas que la E12, la E13d y la E14: condiciones, cálculos, líneas, catálogos (la oferta se arma para quien carga, con sus filtros) y archivos. La numeración, las reglas, la aprobación y el PDF son los mismos.
  - **Archivos:** se suben a la Api con los mismos topes y la misma verificación por contenido que los que llegan por WhatsApp (E14).
  - **`[Idempotent]`** en el `POST` que la crea, para que un doble clic no cree dos respuestas.
- **Front:**
  - «Cargar respuesta» en el ⋮ de cada formulario publicado de «Diseñar formularios», y en el teléfono, en su menú. La E15 la mueve a la planilla;
  - el formulario armado desde la versión activa, con todas sus preguntas, condiciones, líneas y archivos;
  - al guardar, se ve el número de la respuesta y su tarjeta, como en el chat.
- **Tableros que dibuja Diseño,** con nuestras piezas y la decisión 43:
  - `FRM-Cargar-Respuesta` y su teléfono: el formulario en la web, con las preguntas que aparecen según otra respuesta, las líneas y los archivos.

## Decisiones tomadas (no se vuelven a preguntar)

- **Un empleado, a su nombre** (73). Cargar a nombre de otra persona queda para después.
- **La misma captura que WhatsApp:** un solo lugar valida y numera.

## Fuera de esta etapa

- Cargar a nombre de otra persona, como lo que dicta un cliente.
- Importar muchas respuestas desde un Excel.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más un **recorrido real**: cargar desde la web una respuesta con una condición, dos renglones, un cálculo y una foto, y ver que queda igual que una por WhatsApp: el mismo número correlativo, los mismos valores y el cálculo. La planilla, el PDF y las reglas la prueban en sus etapas.

## Requisitos previos

La E13d y la E14 cerradas.

## Ajustes ya decididos

- **Idioma:** el contenido no tiene idioma (35).
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** las tablas de la E12, con RLS. El canal «web» es un valor más de `FormRecords.Channel`.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos:** `formrecords.create`, sin permisos nuevos.
