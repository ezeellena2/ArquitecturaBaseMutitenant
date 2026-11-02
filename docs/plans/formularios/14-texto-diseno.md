# E14 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno. Junta lo que falta de la E12, la base de la 12 y la E14, que salió de la lectura de punta a punta del 2026-10-31 y de la evaluación del traspaso 4, del 2026-11-02.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso lo que falta en los tableros de la E12, la base y la E14, para el lienzo del multitenant. No hay que programar nada: solo tableros. Casi todo es retocar lo que ya está aprobado.

**Contexto.** Comparamos las specs con el lienzo, y hay tableros que muestran algo de una etapa posterior, o a los que les falta un estado. **Los tableros de una etapa muestran solo lo que existe hasta ella** (78). Valen las fichas del front: un dato por celda, fechas completas, sin leyendas (109), con la ayuda que sirve en la (i) (116), botones habilitados o grisados (43) y toda espera a la vista (115).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/14-formularios.md`, §9, `docs/plans/formularios/12-catalogos.md`, §10, y `docs/plans/formularios/base-de-la-12.md`, «Pantallas».

**Qué se redibuja:**
1. **`FRM-Catalogos` y `FRM-Catalogos-14`, con sus teléfonos:** el filtro «Estado» dice «Activos» y «Dados de baja». Hoy dice «Activo», «No se puede usar» y «De baja».
2. **`Campana`, `M-Campana` y `M-Campana-Personal`:** solo los avisos de la base: un método de ingreso, una organización pendiente, una recuperación pedida y la versión nueva. Salen «WhatsApp rechazó la versión 3», «respuestas por aprobar» y «No se pudo mandar el aviso»: cada etapa los dibuja en su versión.
3. **`FRM-Publicar`:** «hace un momento» pasa a fecha y hora completas. Suma dos estados: «Sin número conectado», con «Publicar» grisado y el motivo en su tooltip, y sin cambios con una cuenta de WhatsApp conectada que no tiene el formulario, con «Publicar» habilitado (103).
4. **`FRM-Publicando`:** en «Necesita revisión», los botones «Salió» y «No salió».
5. **`WA-Chats-Formulario`, su teléfono y la versión de Clientes:** en el diálogo, un formulario que no está publicado para el número de ese chat aparece con el motivo y no se puede mandar (103). Estos tableros, y `WA-Chats-Respuesta` con su teléfono, llevan la barra de la bandeja y las fechas como `WA-Chats`, con el buscador y los filtros en una fila y las fechas completas, también en el separador de días.

**Qué se dibuja:**
1. **`FRM-Catalogo-14` y `M-FRM-Catalogo-14`:** sobre `FRM-Catalogo-13`, la sección «Lo usan», «Quitar» apagado en un campo en uso, con el motivo en su tooltip, y la advertencia al darlo de baja: los formularios publicados que lo usan no se pueden mandar.
2. **`Campana-14` y su teléfono:** «WhatsApp rechazó la versión N de …», que lleva al formulario.
3. **`M-FRM-Catalogo-Contactos`:** un catálogo de los contactos en el teléfono, que falta.

**Lo que suma la evaluación del 2026-11-02, sobre la v147** (decisiones 109 a 121). El ejemplo para adaptar es el lienzo de ArquitecturaBase, <https://claude.ai/artifact/Kvbr5KFJrGT951JLiu8i4M>, en la fila que nombra cada punto: se adapta con nuestras piezas y no se copia (108).

Reglas para todos los tableros, de acá en adelante:
- **Sin leyendas (109):** etiqueta y valor o control. El renglón de abajo es solo para el error o para un dato, y lo que no ayuda a decidir no va.
- **La ayuda, en la (i) (116):** con tooltip, junto a la etiqueta, al título o al final de una opción. Lo que no se puede hacer sigue en el aviso importante, con su ⓘ.
- **Toda espera se ve (115):** el botón con el aro y su gerundio, con su color; la confirmación abierta hasta la respuesta, y la tabla que vuelve a leer, con las filas atenuadas y una línea que corre.
- **Lo apagado dice por qué (110):** en su tooltip, también una casilla o la ✕ de un diálogo.

Los tableros «Criterio», que mandan sobre todos (fila «Leyendas y carga»):
1. **`Criterio-Botones`:** «Mientras trabaja» pasa al aro y «Guardando…», con su color.
2. **`Criterio-Formularios`:** la (i) junto a una etiqueta y a un título de sección, y ninguna leyenda debajo.
3. **`Criterio-Tablas`:** la tabla que vuelve a leer.
4. **`Criterio-Dialogos`:** la confirmación que espera, y la ✕ apagada con su motivo.
5. **`Criterio-Avisos`:** cuándo va la (i) y cuándo la ⓘ.

Lo de la E12 (`12-catalogos.md`, §10):
6. **`FRM-Catalogo` y `M-FRM-Catalogo`:** sale «Pegar desde Excel» del ⋮; «Agregar opción» y «Editar» abren el modal, y se van la fila vacía y «opción sin completar» (fila «Catálogos · agregar una opción en un modal»).
7. **`FRM-Catalogo-Opciones` y `M-FRM-Catalogo-Opciones` (nuevos):** el modal, con la lista y la opción. Una opción guardada no se quita: su Estado dice «Activa | De baja». Estados: una nueva, una guardada, una dada de baja, errores con el triángulo y «Volver a la lista» en el teléfono.
8. **`FRM-Catalogo-Importar` y `M-FRM-Catalogo-Importar` (el teléfono, nuevo):** «Origen» y «Revisión», sin pasos numerados, con la grilla de pegar y las esperas. Estados: la grilla vacía, una columna ajena, revisando, aplicando y los que ya tiene (fila «Catálogos · importar opciones»).

Lo de la E14 (`14-parte-decisiones-y-flujos.md`, T25 y T26):
9. **`FRM-Publicar`:** por zonas, sin descripción ni párrafos (fila «Formularios · publicar en WhatsApp»). «Problemas» aparece solo si hay alguno, y «Qué se publica» va siempre. Salen «Qué queda fijo» y «Qué va a pasar». Un texto largo dice «Hasta 20 caracteres; tiene 26.», y «Comprobando…» arma «Qué se publica» con un esqueleto. Sin la zona «Avisos», que llega después (78).
10. **`FRM-Editor` y `M-FRM-Editor`:** «Catálogo» es el combo de `FRM-Catalogo-13`, con los dados de baja y los que no se pueden usar apagados, con su motivo. «Las opciones se preparan al mandar el formulario.» va en la (i) de «Catálogo». Guardando dice «Guardando…».
11. **`FRM-Publicando`:** «Consultar ahora» dice «Consultando…».

**Datos de ejemplo:** Grupo Delta. «Visita a cliente» y el catálogo «Productos».

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo y avisás la versión y la lista de tableros, para anotarlos en la spec.
