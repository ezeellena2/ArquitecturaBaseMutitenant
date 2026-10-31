# E14 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno. Junta lo que falta de la E12, la base de la 12 y la E14, que salió de la lectura de punta a punta del 2026-10-31.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso lo que falta en los tableros de la E12, la base y la E14, para el lienzo del multitenant. No hay que programar nada: solo tableros. Casi todo es retocar lo que ya está aprobado.

**Contexto.** Comparamos las specs con el lienzo, y hay tableros que muestran algo de una etapa posterior, o a los que les falta un estado. **Los tableros de una etapa muestran solo lo que existe hasta ella** (78). Valen las fichas del front: un dato por celda, fechas completas, sin rótulos que expliquen y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/14-formularios.md`, §9, y `docs/plans/formularios/base-de-la-12.md`, «Pantallas».

**Qué se redibuja:**
1. **`FRM-Catalogos` y `FRM-Catalogos-14`, con sus teléfonos:** el filtro «Estado» dice «Activos» y «Dados de baja». Hoy dice «Activo», «No se puede usar» y «De baja».
2. **`Campana`, `M-Campana` y `M-Campana-Personal`:** solo los avisos de la base: un método de ingreso, una organización pendiente, una recuperación pedida y la versión nueva. Salen «WhatsApp rechazó la versión 3», «respuestas por aprobar» y «No se pudo mandar el aviso»: cada etapa los dibuja en su versión.
3. **`FRM-Publicar`:** «hace un momento» pasa a fecha y hora completas. Suma el estado «Sin número conectado», con «Publicar» grisado y el motivo en su tooltip.
4. **`FRM-Publicando`:** en «Necesita revisión», los botones «Salió» y «No salió».

**Qué se dibuja:**
1. **`FRM-Catalogo-14` y `M-FRM-Catalogo-14`:** sobre `FRM-Catalogo-13`, la sección «Lo usan», «Quitar» apagado en un campo en uso, con el motivo en su tooltip, y la advertencia al darlo de baja: los formularios publicados que lo usan no se pueden mandar.
2. **`Campana-14` y su teléfono:** «WhatsApp rechazó la versión N de …», que lleva al formulario.
3. **`M-FRM-Catalogo-Contactos`:** un catálogo de los contactos en el teléfono, que falta.

**Datos de ejemplo:** Grupo Delta. «Visita a cliente» y el catálogo «Productos».

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo y avisás la versión y la lista de tableros, para anotarlos en la spec.
