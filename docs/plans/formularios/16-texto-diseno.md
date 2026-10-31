# E16 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E16, datos de las personas y filtros por destinatario, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Una organización define datos de sus empleados, como «Zona», y una pregunta de un catálogo muestra a cada persona solo lo que le corresponde. **Los tableros muestran solo lo que existe hasta acá** (78): nada de fotos, documentos, aprobación ni «Cargar». Valen las fichas del front: un dato por celda, en un renglón; sin cantidades en los encabezados de sección; sin rótulos que expliquen, y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/16-datos-de-las-personas.md`, sobre todo «Qué se construye» y «Pantallas». La referencia de ArquitecturaBase es `CE4-A`.

**Qué se dibuja:**
1. **`FRM-Datos-Personas` y `M-FRM-Datos-Personas`:** «Datos de las personas», en Administración, con dos secciones.
   - Los datos: nombre, tipo, quién lo usa, cuántos tienen valor, estado y ⋮ (Editar, Dar de baja o Reactivar). Con 20, «Nuevo dato» queda grisado, con el motivo en su tooltip. «Estado» es un filtro, con «Dados de baja» como opción.
   - Las personas: nombre, roles, una columna por dato activo y ✎. «—» si no tiene el dato. Sin `users.read`, esta sección no aparece. Sin `users.manage`, no hay ✎.
   - **El diálogo para definir un dato:** un solo campo de nombre, el tipo (Texto o «De un catálogo») y cuál catálogo. Si el dato tiene valores y se cambia el tipo o la fuente, la confirmación dice cuántos se borran.
   - **Dar de baja un dato en uso:** grisado, con quién lo usa en su tooltip.
2. **`FRM-Usuario-Datos` y `M-FRM-Usuario-Datos`:** la ficha de `Usuario`, con las pestañas «Acceso» y «Datos». «Datos» tiene un campo por dato activo y sus propios botones, y se guarda aparte. Las pestañas existen solo porque el módulo aporta una: sin el módulo, la ficha queda como hoy. Un valor de una opción dada de baja se ve con su texto.
3. **`FRM-Catalogo-16` y `M-FRM-Catalogo-16`:** sobre `FRM-Catalogo-13`, «Campos» suma la columna «Filtro», una casilla por campo. Un campo en uso por un filtro la tiene apagada, con el motivo en su tooltip.
4. **`FRM-Editor-16` y `M-FRM-Editor-16`:** sobre `FRM-Editor`, en una pregunta de un catálogo a mano, «Qué opciones ve cada persona»:
   - un renglón por filtro, «Campo es valor», con ✕;
   - «Agregar un filtro según quien lo recibe» y «Agregar un filtro fijo»;
   - «Como si lo recibiera», con el desplegable de la persona y el resultado («→ 14 opciones»);
   - debajo, el máximo por persona y cuántos no tienen el dato, con «Cargar los datos»;
   - estados: «Le falta el dato», «El filtro no funciona», más opciones que el tope y solo lectura. Sin campos de papel «Filtro», la sección no aparece.
5. **`FRM-Publicar-16`:** sobre `FRM-Publicar`, los avisos en su lista, aparte de lo que frena: «Como mucho 18 opciones por persona» y «2 personas sin Zona». Los avisos no apagan «Publicar». En un formulario para los dos, un aviso dice que no se le va a poder mandar a un cliente.
6. **`WA-Chats-Formulario-16` y `M-WA-Chats-Formulario-16`:** sobre `WA-Chats-Formulario`, un formulario que no se puede mandar, con el motivo en rojo y el envío grisado: le falta «Zona», el filtro no funciona, y a un cliente.
7. **`FRM-Respuestas-16` y `M-FRM-Respuestas-16`:** sobre `FRM-Respuestas`, «Columnas» con la subcolumna «Proveedor · Zona» apagada: los campos de papel «Filtro» salen apagados y se prenden ahí.

**Recorrido guiado.** Definir «Zona», cargársela a dos personas, marcar «Zona» como filtro en «Proveedores», armar el filtro en la pregunta, verlo con «Como si lo recibiera», publicar con el aviso y mandarle a alguien sin el dato.

**Datos de ejemplo:** Grupo Delta. Los datos «Zona», de un catálogo «Zonas», y «Sucursal», de texto. Seis personas, dos sin zona. «Pedir repuesto», con «Proveedores a consultar»: Juan Gómez, de zona Centro, ve 14 de 1.200.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
