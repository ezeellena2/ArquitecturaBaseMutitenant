# E21 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E21, reglas, aprobación, PDF y avisos, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** La E21 suma lo que pasa después de que llega una respuesta: reglas, aprobación de un nivel, un PDF como copia fija, avisos por WhatsApp y correo, corregir y anular. **Los tableros muestran solo lo que existe hasta acá** (78): nada de plantillas de la biblioteca, asistente ni sugerencias. Valen las fichas del front: un dato por celda, fechas completas, sin rótulos que expliquen y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/21-reglas-y-aprobacion.md`, sobre todo «Qué se construye» y «Pantallas». Corregir y anular ya están decididos (91 a 93): se dibujan con la spec y nuestras reglas y piezas (108). Si una pantalla nueva no tiene modelo en nuestro lienzo y la spec no alcanza para dibujarla, mostrale opciones al usuario.

**Qué se redibuja:**
1. **`FRM-Editor-Reglas` y `M-FRM-Reglas`:** una pestaña más del formulario (79). Salen «Cambios sin guardar» y «Revisión N» junto a Guardar. Suma las acciones «Avisar a una persona», «Mandar por correo» y «Mandar el PDF a un cliente», y la plantilla para fuera de las 24 horas, con sus variables.
2. **`FRM-Pdf`:** salen «Cambios sin guardar», «Vista previa en inglés» y «Falta el inglés». Con el logo y los datos de la página pública, el color elegido, secciones, firmas, los campos «Dato», las líneas y los cálculos. **Mostrale al usuario dos o tres estilos** para que elija (59).
3. **`M-FRM-Respuesta-Aprobacion`:** sin lo que ya no va.

**Qué se revisa:** `FRM-Formularios-21` y su teléfono, `FRM-Respuesta-Aprobacion`, `WA-Chats-Avisos` y `M-WA-Conversacion-Avisos`, contra las reglas del 25 y el 26/10. `FRM-Formularios-21` y su teléfono parten de `FRM-Formularios-20`: «Cargar», «Solo en la app», quien solo carga y «Borrar». En `WA-Chats-Avisos`, `FRM-Respuesta-Aprobacion` y sus teléfonos sale «No se pudo: pasaron más de 24 horas»: fuera de las 24 horas va con plantilla (61), y el «No se pudo» es «Sin plantilla aprobada». `FRM-Por-Aprobar` y `M-FRM-Por-Aprobar` pasan al Archivo. `WA-Chats-Avisos` lleva la barra de la bandeja y las fechas como `WA-Chats`, con el buscador y los filtros en una fila y las fechas completas, también en el separador de días.

**Qué se dibuja:**
1. **`FRM-Respuestas-21` y su teléfono:** sobre `FRM-Respuestas-20`, sin ningún cartel arriba de la planilla: la columna Estado, el filtro «Estado», con «Anulada», y la marca de la copia PDF. Lo que espera aprobación se abre desde `Campana-21` y «Para resolver», y ↑ ↓ recorren esa cola. Suma el estado «Quien solo aprueba»: la planilla es su cola, con el número y la fecha, sin los valores, y decide en la vista rápida (100).
2. **`FRM-Tablero-21` y su teléfono:** la tarjeta «Por estado».
3. **`FRM-Respuesta-Corregir` y su teléfono:** la corrección con lo de antes a la vista y el motivo, «Corregida» en la fila y el detalle, la versión anterior en Actividad, una aprobada que vuelve a «Por aprobar», y quien la cargó, que corrige mientras no esté aprobada.
4. **`FRM-Respuesta-Anular` y su teléfono:** «Anular» al final del ⋮, el motivo, la fila que sale de la vista, el filtro «Anulada» y el Excel con «Incluir las anuladas».
5. **`FRM-Respuestas-Propias-21` y su teléfono:** lo propio con el estado, el PDF y el motivo de rechazo.
6. **`FRM-Editor-21` y `FRM-Cargar-Respuesta-21`, con sus teléfonos:** la pregunta «Correo».
7. **`Campana-21` y su teléfono:** «N respuestas por aprobar» y «No se pudo mandar el aviso de …».
8. **`FRM-Dar-De-Baja-21` y `FRM-Editor-Baja-21`:** «Lo que se conserva» suma el PDF y las aprobaciones, y lo que espera decisión se puede seguir decidiendo.

**Recorrido guiado.** Armar una regla, llega una respuesta, aviso, aprobar desde «Para resolver», el PDF en el celular y corregirla.

**Datos de ejemplo:** Grupo Delta. «Visita a cliente», con el rol Supervisor.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
