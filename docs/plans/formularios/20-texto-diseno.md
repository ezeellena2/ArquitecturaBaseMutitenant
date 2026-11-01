# E20 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo. Va en su turno, después del bloque 0 y de la E19 del plan de diseño del 27/10. El punto 1 cambia un tablero de la E14, y lo de «Después, con la E21» se dibuja con esa etapa.

---

Te paso los tableros de la carga desde la web para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Un empleado completa un formulario desde la web, a su nombre, con la misma captura que por WhatsApp. El usuario ya decidió cómo funciona: son las decisiones 83 a 98 de la hoja de ruta, y no se vuelven a preguntar. **Los tableros muestran solo lo que existe hasta la E20** (78): nada de estado, «Por aprobar», PDF, corregir ni anular. Valen las fichas del front: un dato por celda, fechas completas, sin rótulos que expliquen y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/20-cargar-desde-la-web.md`, sobre todo «Cómo se arma cada pregunta» y «Pantallas».

**Con qué se dibuja.** Con la spec, esas decisiones y nuestras reglas y piezas (108). Si una pantalla nueva no tiene modelo en nuestro lienzo y la spec no alcanza para dibujarla, mostrale opciones al usuario. **La maqueta no sirve de base para esto:** tiene una versión anterior, con «Cargar respuesta» en el ⋮ y el canal «Web».

**Qué se dibuja:**
1. **`FRM-Editor` y `M-FRM-Editor` (E14):** «Agregar título» en el editor, el título entre las preguntas con su ⋮ (Editar, Subir, Bajar y Quitar) y el subtítulo en el teléfono al costado.
2. **`FRM-Formularios-20` y su teléfono:**
   - «Cargar» al final de cada fila; no aparece en los borradores ni en los dados de baja, y en uno solo para clientes queda grisado, con el motivo en su tooltip;
   - «Solo en la app» en la columna «En WhatsApp»;
   - lo que ve quien solo carga: sus formularios y «Cargar», sin las columnas de respuestas, y el filtro «Estado», con «Dados de baja», donde ve lo suyo, sin «Cargar» (101);
   - en el teléfono, con nuestra forma de fila, no una tabla con desplazamiento.
3. **`FRM-Cargar-Respuesta` y su teléfono:**
   - «Nueva respuesta» en su propia página, con el formulario en las migas;
   - Cancelar y el botón del formulario en la banda; en el teléfono, ese botón fijo al pie;
   - la grilla de hasta tres por renglón, con los anchos de la tabla de la spec, las zonas plegables, sin cantidades en su banda, y los títulos de sección;
   - lo de la E19: la pregunta que aparece con los renglones recalculados, el grupo de líneas en el renglón entero, y sin cálculo a la vista;
   - los errores en su pregunta, la zona plegada que se abre sola y la confirmación al salir.
4. **`FRM-Cargar-Panel`:** el panel junto a la planilla, en una columna, con «Mantener las respuestas para la siguiente» y «Pantalla completa». Solo en la compu.
5. **`FRM-Cargar-Lista` y su teléfono:** la lista con búsqueda, el total, la descripción y «Mostrar 50 más»; una sola opción, ya elegida; sin el dato de la persona; elegir varias; la lista que espera otra respuesta y se limpia si esa respuesta cambia. En el teléfono ocupa la pantalla.
6. **`FRM-Cargar-Archivos` y su teléfono:** arrastrar o elegir, el contador, los errores antes de enviar, el botón grisado con su texto y **una barra de avance sin palabras** (95, sin «Enviando… 64 %»), el corte de conexión y «Tomar foto» primero en el teléfono.
7. **`FRM-Cargar-Enviada` y su teléfono:** «Enviada» con lo que se mandó, los cálculos, el número y la hora, con «Cargar otra» y «Ver en la planilla». Y los casos: versión nueva, con «Pasar a la versión nueva»; dado de baja; una opción dada de baja, en su pregunta; sin conexión.
8. **`FRM-Respuestas-20` y `FRM-Respuesta-20`, con sus teléfonos:** «Agregar respuesta» en el encabezado, «App» en «Canal» y en su filtro, y en el detalle «Recibida · … · en la app, la cargó …», sin «Abrir conversación».
9. **`FRM-Respuestas-Propias` y su teléfono:** la misma planilla con lo suyo, sin «Mis cargas» aparte.
10. **`FRM-Editor-Listas` y su teléfono:** «Según otra respuesta» en «Qué opciones ve cada persona», con la pregunta de la que depende.
11. **`FRM-Publicar-20` y su teléfono:** es nuevo y no tiene modelo: mostrale opciones al usuario. Publicar solo para la app en tres casos: sin número conectado, con una lista dependiente y con un catálogo a mano más grande de lo que admite WhatsApp: más de 200 opciones en «elegir una» o más de 20 en «elegir varias» (104). Y un formulario solo para clientes, que no se puede publicar. Sin la vista oficial de Meta.

**Después, con la E21:**
- `FRM-Respuesta-Corregir`: la corrección con lo de antes a la vista y el motivo, «Corregida» en la fila y el detalle, la versión anterior, una aprobada que vuelve a «Por aprobar», y quien la cargó;
- `FRM-Respuesta-Anular`: «Anular» al final del ⋮, el motivo, la fila que sale de la vista, el filtro «Anulada» y el Excel con «Incluir las anuladas»;
- `FRM-Respuestas-Propias-21`: lo propio con el estado, el PDF y el motivo de rechazo.

**Datos de ejemplo:** Grupo Delta, con «Pedido de materiales» (empleados), un catálogo «Productos» con campos y un formulario con fotos.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado de la etapa (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
