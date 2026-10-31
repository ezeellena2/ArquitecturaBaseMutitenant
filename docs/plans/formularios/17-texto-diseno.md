# E17 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E17, ciclo de vida y copiar, para el lienzo del multitenant. No hay que programar nada ni dibujar tableros nuevos: solo revisar los que ya están.

**Contexto.** La E17 suma dar de baja, reactivar, borrar y copiar un formulario. **Los tableros muestran solo lo que existe hasta acá** (78): todavía no hay fotos, documentos, PDF, aprobaciones, asistente ni sugerencias. Valen las fichas del front: un dato por celda, fechas completas, sin rótulos que expliquen, y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/17-ciclo-de-vida.md`, sobre todo «Qué se construye» y «Pantallas».

**Qué se corrige:**
1. **«Eliminar» pasa a «Borrar»** en `FRM-Formularios-17`, `FRM-Editor-Baja`, `FRM-Reactivar-Eliminar` y `FRM-Copiar`: en el ⋮, «¿Borrar «…»?» en el diálogo y «Borramos «…».» en el aviso. Es regla del sistema. El nombre del tablero `FRM-Reactivar-Eliminar` queda.
2. **`FRM-Dar-De-Baja`:** «Qué deja de pasar» dice solo que no se manda más desde el chat, sin el asistente ni las sugerencias. «Lo que se conserva» son las respuestas, sin fotos, PDF ni aprobaciones.
3. **`FRM-Editor-Baja` y `M-FRM-Editor-Baja`:** el aviso, sin el asistente, el PDF ni las aprobaciones.
4. **`FRM-Reactivar-Eliminar`:** sin el asistente.
5. **`FRM-Formularios-17`, estado «Quien solo ve respuestas»:** suma el filtro «Estado», con «Activos» y «Dados de baja». Sin él, esa persona no llega a las respuestas de un formulario dado de baja. Igual en `M-FRM-Formularios-17`.
6. **`FRM-Reactivar-Eliminar`:** suma borrar un publicado sin respuestas, con el aviso de que sus Flows se deprecan y eso no se deshace, y borrar con error.

**Recorrido guiado.** `BA5` va como recorrido de la guía: activo, dar de baja, un envío abierto que se responde, al vencer deprecados en WhatsApp y reactivar, con la numeración que sigue. Suma copiar un formulario.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión, para anotarla en la spec.
