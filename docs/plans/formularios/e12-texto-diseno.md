# E12 · Texto para el chat de Diseño

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E12 para redibujar en el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Los tableros de la E12 están aprobados en la v128. El 2026-10-24 el usuario tomó las decisiones 34 a 44 de la hoja de ruta de formularios, y cuatro tableros de la E12 chocan con ellas:
- **35:** el contenido no tiene idioma. Un formulario es un solo texto por campo, como lo escribe la persona. Sin «Textos en», sin pares en inglés, sin vista previa en dos idiomas y sin avisos de traducción;
- **38:** «elegir varias» es un tipo de pregunta de la E12, con mínimo y máximo, de 1 a 20 opciones;
- **43:** un botón está habilitado o grisado, sin textos al lado ni debajo, y mientras trabaja queda grisado con su mismo texto. Nunca «Cambios sin guardar», ni números de revisión junto a Guardar. Si falta un dato obligatorio, el botón sigue habilitado y al tocarlo se marca el campo;
- **34:** el menú principal dice «Gestión de formularios». En esta etapa su panel tiene solo «Diseñar formularios».

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`:
1. **La spec:** `docs/plans/formularios/e12-formularios.md`, sobre todo la §9 (tableros, estados y en qué mandan).
2. **La hoja de ruta:** `docs/plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md`, §5, decisiones 34 a 44.
3. **La evaluación:** `docs/plans/2026-10-24-formularios-evaluacion-traspaso-2.md`, §2.1, §2.2 y §5.1.
4. **ArquitecturaBase, como referencia de contenido:** el anexo D del traspaso 2 (`../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/anexo-d-tableros-desde-17-10.md`), CA4 para «elegir varias». Hacé `git fetch` antes.

**Qué se redibuja** (la disposición se copia, sin mostrar alternativas, con nuestras piezas y criterios; decisión 18):
1. **`FRM-Editor`:** el formulario (nombre, texto del botón final y prefijo), las preguntas en el lugar con ⋮, «elegir varias» con «Mínimo» y «Máximo», y «Así lo ve el empleado» al costado, con las casillas y «Elegí hasta N».
2. **`M-FRM-Editor`:** lo mismo en el teléfono, con la pregunta en una hoja y los botones fijos abajo.
3. **`FRM-Publicar`:** «Publicar la versión N en WhatsApp», con qué cambia y qué queda fijo, sin el idioma en que sale.
4. **`FRM-Publicando`:** cómo termina, con los pasos Aceptada, Enviada a WhatsApp, WhatsApp la revisa y Lista para mandar.

**Estados que se dibujan** (la lista completa está en la §9 de la spec):
- **Editor:** guardar; no se guardó (lo escrito se conserva); otra persona guardó; prefijo ocupado; quitar una pregunta; cambiar el tipo; descartar; solo lectura; cargando y no existe.
- **Publicar:** primera publicación; comprobando; publicar grisado con cambios sin guardar; una pregunta que no se puede mostrar; cambió después de comprobar; ya se está publicando.
- **Cómo termina:** publicado; rechazada con «Ir a la pregunta»; sin confirmar con «Consultar ahora»; necesita revisión.

**Lo que ya no va:** «Guardado sin confirmar» con «Consultar guardado», «Consultar envío» y «La comprobación venció». Si no se sabe si algo salió, la pantalla vuelve a leer sola (43).

**Datos de ejemplo:** Grupo Delta, Lucía Fernández y un empleado del tablero «Usuarios». «Visita a cliente» (VIS), publicado, versión 3, con Cliente (texto corto, obligatoria), Fecha de la visita, ¿Se concretó la venta?, Monto estimado (2 decimales), Motivo (elegir una), Fallas (elegir varias, de 1 a 3) y Observaciones.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado de la etapa (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
