# E18 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E18, fotos y documentos, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** La E18 suma las preguntas de foto y de documento, con los archivos en la planilla y el detalle, y los archivos en los chats de atención. **Los tableros muestran solo lo que existe hasta acá** (78): nada de líneas, cálculos, aprobación, PDF, «Cargar» ni asistente. Valen las fichas del front: un dato por celda, en un renglón; fechas completas; sin rótulos que expliquen, y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/18-fotos-y-documentos.md`, sobre todo «Qué se construye» y «Pantallas».

**Qué se revisa:** `FRM-Editor-18`, `M-FRM-Editor-18`, `FRM-Publicar-18`, `WA-Chats-Respuesta-18`, `M-WA-Conversacion-Respuesta-18`, `WA-Chats-Visor` y `M-WA-Chats-Visor`, contra las reglas del 25 y el 26/10. Se dibujaron en la v130.

**Qué se dibuja:**
1. **`FRM-Respuestas-18` y `M-FRM-Respuestas-18`:** sobre `FRM-Respuestas`, las columnas «Fotos de la visita» (la primera miniatura y la cantidad, que abre el visor) y «Remito firmado» (un ícono y cuántos, con sus tipos). En «Más filtros», su sección con «Con respuesta», «Sin respuesta» y «La pregunta no existía». En exportar, el ⓘ: las fotos y los documentos no van dentro del Excel, y cada celda dice cuántos hay y sus nombres.
2. **`FRM-Respuesta-18` y `M-FRM-Respuesta-18`:** sobre `FRM-Respuesta`, las pestañas «Respuesta» y «Fotos», con la cantidad; en «Respuesta», las miniaturas en su pregunta y los documentos con nombre, tamaño y Descargar; «Ver una foto» abre `WA-Chats-Visor`.
3. **`FRM-Respuesta-Completa-18`:** sobre `FRM-Respuesta-Completa`, las fotos y el remito en «Respuesta», y «Descargar ▾» arriba, con los documentos.
4. **`WA-Chats-Adjuntos` y `M-WA-Chats-Adjuntos`:** los archivos en un chat de atención, para Clientes, Internos y Plataforma.
   - En el compositor, el clip para adjuntar. En el teléfono, en el ⋮ o al lado del campo, como el resto del chat.
   - Una vista del archivo antes de mandarlo, con un texto opcional.
   - Estados: subiendo (el botón grisado con su mismo texto), enviado, no se pudo (con ⓘ, sin reintento), demasiado grande y fuera de las 24 horas (adjuntar grisado, con el motivo en su tooltip).
   - Lo que llega: una foto con su miniatura, que abre el visor; un audio y un video que se escuchan o se ven; un documento con nombre, tamaño y Descargar; «Bajando», y un documento de un tipo que no se guarda, que dice «Mandó un documento», sin descarga.

**Recorrido guiado.** Armar una pregunta de foto, publicar, responder con la cámara, verla en el chat, en la planilla y en el visor. Después, en un chat de Clientes, ver lo que mandó el contacto y mandarle un PDF.

**Datos de ejemplo:** Grupo Delta. «Visita a cliente», con «Fotos de la visita» y «Remito firmado».

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
