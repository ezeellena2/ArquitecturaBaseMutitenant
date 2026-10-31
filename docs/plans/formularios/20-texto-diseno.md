# E20 · Texto para el chat de Diseño

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo. Va en su turno, después del bloque 0 y de la E19 del plan de diseño del 27/10. El punto 1 cambia un tablero de la E14, y lo de «Después, con la E21» se dibuja con esa etapa.

---

Te paso los tableros de la carga desde la web para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Un empleado completa un formulario desde la web, a su nombre, con la misma captura que por WhatsApp. ArquitecturaBase ya lo decidió y programó su primera parte, y el usuario sumó decisiones propias para acá (83 a 98 de la hoja de ruta). **Los tableros muestran solo lo que existe hasta la E20** (78): nada de estado, «Por aprobar», PDF, corregir ni anular. Valen las fichas del front: un dato por celda, fechas completas, sin rótulos que expliquen y botones habilitados o grisados (43). Mandan nuestras piezas y nuestras reglas, también donde allá se vea otra cosa.

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: el resumen `docs/plans/formularios/20-cargar-desde-la-web.md`, sobre todo «Cómo se arma cada pregunta» y «Tableros». En `ArquitecturaBaseFront`: `docs/design/carga-desde-la-app.md`.

**La referencia.** Los tableros elegidos del lienzo de ArquitecturaBase, <https://claude.ai/artifact/Kvbr5KFJrGT951JLiu8i4M>. Desde la cuenta de Claude Code no se abre. Si tampoco se te abre a vos, pedíle al usuario que te lo comparta antes de dibujar: CW7, CW10-A, CW11-A y CW15-B no están escritos con detalle en ningún documento. **La maqueta no sirve de base para esto:** tiene una versión anterior, con «Cargar respuesta» en el ⋮ y el canal «Web».

**Qué se dibuja:**
1. **`FRM-Editor` y `M-FRM-Editor` (E14):** «Agregar título» en el editor, el título entre las preguntas con su ⋮ (Editar, Subir, Bajar y Quitar) y el subtítulo en el teléfono al costado (CW15-B).
2. **`FRM-Formularios-20` y su teléfono (CW1-B):**
   - «Cargar» al final de cada fila; no aparece en los dados de baja, y en uno solo para clientes queda grisado, con el motivo en su tooltip;
   - «Solo en la app» en la columna «En WhatsApp»;
   - lo que ve quien solo carga: sus formularios y «Cargar», sin las columnas de respuestas;
   - en el teléfono, con nuestra forma de fila, no una tabla con desplazamiento.
3. **`FRM-Cargar-Respuesta` y su teléfono (CW2-A, CW12, CW13-A y CW16):**
   - «Nueva respuesta» en su propia página, con el formulario en las migas;
   - Cancelar y el botón del formulario en la banda; en el teléfono, ese botón fijo al pie;
   - la grilla de hasta tres por renglón, con los anchos de la tabla del resumen, las zonas plegables con «N de M respondidas», y los títulos de sección;
   - lo de la E19, que allá no existe: la pregunta que aparece con los renglones recalculados, el grupo de líneas en el renglón entero, y sin cálculo a la vista;
   - los errores en su pregunta, la zona plegada que se abre sola y la confirmación al salir.
4. **`FRM-Cargar-Panel` (CW2-B):** el panel junto a la planilla, en una columna, con «Mantener las respuestas para la siguiente» y «Pantalla completa». Solo en la compu.
5. **`FRM-Cargar-Lista` y su teléfono (CW3 y CW7):** la lista con búsqueda, el total, la descripción y «Mostrar 50 más»; una sola opción, ya elegida; sin el dato de la persona; elegir varias; la lista que espera otra respuesta y se limpia si esa respuesta cambia. En el teléfono ocupa la pantalla.
6. **`FRM-Cargar-Archivos` y su teléfono (CW4):** arrastrar o elegir, el contador, los errores antes de enviar, el botón grisado con su texto y **una barra de avance sin palabras** (95, no copiar «Enviando… 64 %»), el corte de conexión y «Tomar foto» primero en el teléfono.
7. **`FRM-Cargar-Enviada` y su teléfono (CW5):** «Enviada» con lo que se mandó, los cálculos, el número y la hora, con «Cargar otra» y «Ver en la planilla». Y los casos: versión nueva, con «Pasar a la versión nueva»; dado de baja; una opción dada de baja, en su pregunta; sin conexión.
8. **`FRM-Respuestas-20` y `FRM-Respuesta-20`, con sus teléfonos (CW6-A):** «Agregar respuesta» en el encabezado, «App» en «Canal» y en su filtro, y en el detalle «Recibida · … · en la app, la cargó …», sin «Abrir conversación».
9. **`FRM-Respuestas-Propias` y su teléfono (CW6-C):** la misma planilla con lo suyo, sin «Mis cargas» aparte.
10. **`FRM-Editor-Listas` y su teléfono (CW7):** «Según otra respuesta» en «Qué opciones ve cada persona», con la pregunta de la que depende.
11. **`FRM-Publicar-20` y su teléfono:** es nuevo, no tiene modelo allá: mostrale opciones al usuario. Publicar solo para la app en tres casos: sin número conectado, con una lista dependiente y con un catálogo de más de 200 opciones. Y un formulario solo para clientes, que no se puede publicar. Sin la vista oficial de Meta.

**Después, con la E21:**
- `FRM-Respuesta-Corregir` (CW10-A): la corrección con lo de antes a la vista y el motivo, «Corregida» en la fila y el detalle, la versión anterior, una aprobada que vuelve a «Por aprobar», y quien la cargó;
- `FRM-Respuesta-Anular` (CW11-A): «Anular» al final del ⋮, el motivo, la fila que sale de la vista, el filtro «Anulada» y el Excel con «Incluir las anuladas»;
- `FRM-Respuestas-Propias-21`: lo propio con el estado, el PDF y el motivo de rechazo.

**Datos de ejemplo:** Grupo Delta, con «Pedido de materiales» (empleados), un catálogo «Productos» con campos y un formulario con fotos.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado de la etapa (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
