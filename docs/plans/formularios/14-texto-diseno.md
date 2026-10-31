# E14 · Texto para el chat de Diseño

**Estado:** cerrado · Diseño ya dibujó estos tableros.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso lo que falta de los tableros de la E14, formularios, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Los tableros de la E14 están aprobados en la v139 (front `396c15e1`). El 2026-10-25 el usuario tomó las decisiones 78 a 82, y la E14 cambia con ellas:
- **78:** los catálogos van primero (E12 y E13), y la E14 nace con la pregunta «Elegir de un catálogo». **Los tableros de una etapa muestran solo lo que existe hasta ella:** nada de reglas, archivos, respuestas en planilla ni plantillas;
- **79:** sale «Diseñar formularios». Queda una sola lista «Formularios», con «Nuevo formulario», y cada formulario se abre con sus pestañas; en esta etapa, solo «Diseño». Ya está en la maqueta (`6d859a8`);
- **80:** lo que sale por la cola de WhatsApp se reintenta solo. Salen «Envío sin confirmar», «Ya lo revisé» y «Resolver el envío».

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`:
1. **La spec:** `docs/plans/formularios/14-formularios.md`, sobre todo la §9 (tableros, estados y en qué mandan).
2. **La hoja de ruta:** `docs/plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md`, §5, decisiones 78 a 80.
3. **ArquitecturaBase, como referencia de contenido:** el anexo D del traspaso 2 (`../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/anexo-d-tableros-desde-17-10.md`), CA4. Hacé `git fetch` antes.

**Qué se dibuja** (la disposición se copia, sin mostrar alternativas, con nuestras piezas y criterios; decisión 18):
1. **«Formularios» (79):** la lista, una tabla con Formulario, Preguntas, En WhatsApp y Último cambio, el filtro «Estado ▾» y «Nuevo formulario». El nombre abre el formulario con su pestaña «Diseño», que es el editor de `FRM-Editor`. Reemplaza a `FRM-Listado` y `M-FRM-Listado`; copiá la de la maqueta.
2. **`CA4` · Elegir de un catálogo,** en `FRM-Editor` y su teléfono: la pregunta con «De un catálogo», el selector, «Permitir elegir varias» con mínimo y máximo, «varias, hasta N» en la lista de preguntas, la vista previa con casillas, un catálogo de más de 20 y la presentación de la pregunta (la del catálogo o la suya).
3. **El envío y la respuesta,** en `WA-Chats-Formulario` y `WA-Chats-Respuesta`: la cantidad de opciones por pregunta y los problemas de la oferta (más de 200, menos que el mínimo, ninguna en una obligatoria, un catálogo que no se puede usar); el valor con su código; y una opción dada de baja, con «Mandar de nuevo».
4. **«Mis mensajes»:** la tarjeta de una respuesta propia de un cliente que vinculó su cuenta.

**Qué se ajusta:**
- `WA-Chats-Formulario`, su teléfono y sus estados: sin «Ya lo revisé», «Resolver el envío» ni «envío sin confirmar» (80);
- `FRM-Editor` y `M-FRM-Editor`: sin «4 reglas · Revisión 3»; `FRM-Publicar`: sin «Más de 10 archivos», y con «un catálogo demasiado grande»;
- `FRM-Catalogos` y `M-FRM-Catalogos`: suman la columna «Lo usan».

**Datos de ejemplo:** los de la v139, con «Proveedores» (catálogo a mano con el CUIT en la descripción) en una pregunta «Proveedor» de «Visita a cliente».

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado de la etapa (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
