# E20 · Cargar una respuesta desde la web (resumen)

**Estado:** resumen de la etapa 20 del bloque (decisiones 73 y 78), después de la E19. No tiene equivalente en ArquitecturaBase.

## Qué se construye

- **Back:**
  - **Un canal más para responder:** un empleado completa un formulario desde la web, a su nombre, como si fuera por WhatsApp. La respuesta guarda el canal «web».
  - **Quién:** un miembro activo con `formrecords.create`, y solo formularios que son para empleados o para los dos (50). Un cliente sigue respondiendo por WhatsApp.
  - **La misma captura:** valida contra la versión activa sellada, con las mismas reglas que por WhatsApp: catálogos (la oferta se arma para quien carga, con sus filtros por destinatario de la E16), archivos (E18), condiciones, cálculos y líneas (E19). La numeración es la misma.
  - **Archivos:** se suben a la Api con los mismos topes y la misma verificación por contenido que los que llegan por WhatsApp (E18).
  - **`[Idempotent]`** en el `POST` que la crea, para que un doble clic no cree dos respuestas.
- **Front:**
  - «Cargar respuesta» en la pestaña «Respuestas» de cada formulario publicado (E15), y en el teléfono, en su menú;
  - en la planilla, el canal «web» en su columna y en sus filtros;
  - el formulario armado desde la versión activa, con todas sus preguntas, condiciones, líneas y archivos;
  - al guardar, se ve el número de la respuesta y su tarjeta, como en el chat.
- **Tableros que dibuja Diseño,** con nuestras piezas y la decisión 43:
  - `FRM-Cargar-Respuesta` y su teléfono: el formulario en la web, con las preguntas que aparecen según otra respuesta, las líneas y los archivos;
  - en `FRM-Respuestas`, «Cargar respuesta» y el canal «web».

## Decisiones tomadas (no se vuelven a preguntar)

- **Un empleado, a su nombre** (73). Cargar a nombre de otra persona queda para después.
- **La misma captura que WhatsApp:** un solo lugar valida y numera.

## Fuera de esta etapa

- Cargar a nombre de otra persona, como lo que dicta un cliente.
- Importar muchas respuestas desde un Excel.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más un **recorrido real**: cargar desde la web una respuesta con una condición, dos renglones, un cálculo y una foto, y ver que queda igual que una por WhatsApp: el mismo número correlativo, los mismos valores y el cálculo, y en la planilla con el canal «web».

## Requisitos previos

La E19 cerrada.

## Ajustes ya decididos

- **Idioma:** el contenido no tiene idioma (35).
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** las tablas de la E14, con RLS. El canal «web» es un valor más de `FormRecords.Channel`.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos:** `formrecords.create`, sin permisos nuevos.
