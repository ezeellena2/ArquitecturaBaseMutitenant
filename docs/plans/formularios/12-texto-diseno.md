# E12 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma antes de seguir con las etapas siguientes. Sale de la evaluación del traspaso 4, del 2026-11-02, sobre la v147.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso lo que cambia en el lienzo del multitenant por la evaluación del traspaso 4 de ArquitecturaBase: las reglas de todas las pantallas y los tableros de la E12, catálogos. No hay que programar nada: solo tableros.

**Contexto.** El usuario probó ArquitecturaBase de punta a punta y ajustó varias cosas, y eligió cuáles se adoptan acá. Son las decisiones 109 a 122 de la hoja de ruta, y no se vuelven a preguntar. **Los tableros de una etapa muestran solo lo que existe hasta ella** (78).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/12-catalogos.md`, §10, y la hoja de ruta, decisiones 109 a 122. En el repo del front, `docs/rules/pantallas-y-ui.md` y `formularios.md`, que ya dicen las reglas nuevas.

**Con qué se dibuja.** Con la spec, esas decisiones y nuestras reglas y piezas (108). De ejemplo está el lienzo de ArquitecturaBase, <https://claude.ai/artifact/Kvbr5KFJrGT951JLiu8i4M>, en la fila que nombra cada punto: lo que sirva se adapta a nuestro sistema, no se copia tal cual, y si choca con nuestras reglas, mandan ellas.

**Reglas para todos los tableros, de acá en adelante:**
- **Sin leyendas (109):** etiqueta y valor o control. El renglón de abajo es solo para el error o para un dato, y lo que no ayuda a decidir no va.
- **La ayuda, en la (i) (116 y 122):** con tooltip, junto a la etiqueta, al título o al final de una opción; en el teléfono, tocarla abre una hoja chica con el texto. Lo que no se puede hacer sigue en el aviso importante, con su ⓘ.
- **Toda espera se ve (115):** el botón con el aro y su gerundio, con su color; la confirmación abierta hasta la respuesta, y la tabla que vuelve a leer, con las filas atenuadas y una línea que corre.
- **Lo apagado dice por qué (110 y 122):** en su tooltip, también una casilla o la ✕ de un diálogo; en el teléfono, tocarlo lo muestra en la franja oscura de abajo.

**Qué se ajusta en los tableros «Criterio»,** que mandan sobre todos (fila «Leyendas y carga»):
1. **`Criterio-Botones`:** «Mientras trabaja» pasa al aro y «Guardando…», con su color.
2. **`Criterio-Formularios`:** la (i) junto a una etiqueta y a un título de sección, ninguna leyenda debajo, y la hoja que abre la (i) en el teléfono.
3. **`Criterio-Tablas`:** la tabla que vuelve a leer.
4. **`Criterio-Dialogos`:** la confirmación que espera, y la ✕ apagada con su motivo.
5. **`Criterio-Avisos`:** cuándo va la (i) y cuándo la ⓘ, y el motivo de lo apagado en la franja del teléfono.

**Qué se redibuja:**
1. **`FRM-Catalogo` y `M-FRM-Catalogo`:** sale «Pegar desde Excel» del ⋮. «Agregar opción» y «Editar» abren el modal, y se van la fila vacía y «opción sin completar» (fila «Catálogos · agregar una opción en un modal»). Sin «hay más columnas a la derecha»: la barra de desplazamiento alcanza.
2. **`FRM-Catalogo-Importar`:** «Origen» y «Revisión» a la vista, sin pasos numerados, con la grilla de pegar y las esperas (fila «Catálogos · importar opciones»). Estados: la grilla vacía, una columna ajena, revisando, aplicando y los que ya tiene.

**Qué se dibuja:**
1. **`FRM-Catalogo-Opciones` y `M-FRM-Catalogo-Opciones`:** el modal, con la lista a la izquierda y la opción a la derecha; en el teléfono, primero la lista. Una opción guardada no se quita: su Estado dice «Activa | De baja». Estados: una nueva, una guardada, una dada de baja, errores con el triángulo y «Volver a la lista».
2. **`M-FRM-Catalogo-Importar`:** importar en el teléfono, que ocupa la pantalla.

**Datos de ejemplo:** Grupo Delta. «Proveedores», a mano.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo y avisás la versión y la lista de tableros, para anotarlos en la spec.
