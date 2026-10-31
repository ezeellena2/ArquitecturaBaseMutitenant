# E13 · Texto para el chat de Diseño

**Estado:** pendiente · faltan los tres estados de `FRM-Catalogo-13` que dice este texto; lo demás ya está dibujado.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E13, campos de los catálogos, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** La E13 suma campos a los catálogos a mano de la E12: sus valores, sus columnas al pegar, importar y exportar, y qué se ve en WhatsApp. **Los tableros muestran solo el catálogo** (78): nada de preguntas, formularios ni respuestas. Valen las fichas del front: un dato por celda, sin cantidades en los encabezados, sin rótulos que expliquen y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/13-campos.md`, sobre todo «Front» y «Tableros».

**Qué se dibuja:**
1. **`CE1` · Los campos, en la forma B:** una tabla que se edita ahí mismo, sin diálogo, con Campo, Tipo, Obligatorio, Único, Límite y ⋮ (Subir, Bajar y Quitar). «Campo» fijo a la izquierda y el ⋮ a la derecha. Los tipos son Texto, Número entero, Número con decimales, Fecha, Sí/No y «De un catálogo: …». La clave no se muestra. Los errores van debajo de su celda, al guardar. «Agregar campo» se apaga al llegar a 20.
2. **`CE2` · Los valores:** una columna por campo en la tabla de opciones, con el control de su tipo; la columna de un campo nuevo, grisada hasta guardar el catálogo.
3. **`CE3-B` · «En WhatsApp», en la forma C** (`FRM-Catalogo-13`, v145):
   - dos renglones, «Título» y «Descripción»;
   - el título, con un solo desplegable que arranca en «Texto»;
   - la descripción, con un desplegable por lugar, un ✕ para quitarlo y un «+» que suma otro; los lugares se unen con « · »;
   - cada desplegable lista Texto, Código, el valor si el catálogo lo tiene, Detalle y los campos: lo que todavía no está en ese mismo renglón, más lo que ya tiene elegido. Ninguno aparece apagado;
   - sin «Por ejemplo», sin «No salen del sistema» y sin los largos: el teléfono al costado muestra cómo queda.

**Estados que faltan en `FRM-Catalogo-13`,** que la spec pide y no están dibujados:
- un valor de una opción dada de baja, con su texto, que se puede guardar;
- solo `formcatalogs.read`: la tabla de lectura, sin edición;
- un título que no entra, marcado en esa opción y en el teléfono, que se puede guardar.

**Datos de ejemplo:** Grupo Delta. «Proveedores», a mano, con los campos CUIT, Zona (de un catálogo: «Zonas») y Plazo de pago, y el CUIT en la descripción.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado de la etapa (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
