# E16 · Texto para el chat de Diseño

**Estado:** pendiente · dibujado en la v150, sin publicar: espera el OK del usuario.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E16, datos de las personas y filtros por destinatario, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Una organización define datos de sus empleados, como «Zona», y una pregunta de un catálogo muestra a cada persona solo lo que le corresponde. **Los tableros muestran solo lo que existe hasta acá** (78): nada de fotos, documentos, aprobación ni «Cargar». Valen las fichas del front: un dato por celda, en un renglón; sin cantidades en los encabezados de sección; sin leyendas (109), con la ayuda que sirve en la (i) (116); botones habilitados o grisados (43), y toda espera a la vista (115).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/16-datos-de-las-personas.md`, sobre todo «Qué se construye» y «Pantallas».

**Qué se dibuja:**
1. **`FRM-Datos-Personas` y `M-FRM-Datos-Personas`:** «Datos de las personas», en Administración, como en ArquitecturaBase: «Datos» (Dato, Clave, Tipo, Lo usan y ✎, con «+ Agregar dato» en su encabezado) y «Personas» (el resumen, «Buscar personas», una columna por dato activo y ✎). El diálogo lleva Nombre, Clave, Tipo y Catálogo, en el combo de `FRM-Catalogo-Campos` (111), y al editar, «Dato activo».
2. **`FRM-Usuario-Datos` y `M-FRM-Usuario-Datos`:** la ficha de `Usuario`, con las pestañas «Acceso» y «Datos». «Datos» tiene un campo por dato activo y sus propios botones, y se guarda aparte.
3. **`FRM-Catalogo-16` y `M-FRM-Catalogo-16`:** el modal de campos de la 13, con «Papel: Filtro | Dato» y la ayuda en su (i). Un campo en uso por un filtro no cambia: el motivo va debajo.
4. **`FRM-Editor-16` y `M-FRM-Editor-16`:** en una pregunta de un catálogo a mano, la caja «Qué opciones ve cada persona», con una condición por filtro, «Como si lo recibiera», el resultado y el máximo. Estados: «Le falta el dato», «El filtro no funciona», más opciones que el tope, solo lectura y sin campos de filtro.
5. **`FRM-Publicar-16`:** la zona «Avisos» en su caja, entre «Problemas» y «Qué se publica» (112).
6. **`WA-Chats-Formulario-16` y `M-WA-Chats-Formulario-16`:** un formulario que no se puede mandar, con el motivo en rojo y el envío grisado: le falta «Zona», el filtro no funciona, y a un cliente.
7. **`FRM-Respuestas-16` y `M-FRM-Respuestas-16`:** «Columnas», con la subcolumna «Proveedor · Zona» apagada.

**Recorrido guiado.** Definir «Zona», cargársela a dos personas, marcar «Zona» como filtro en «Proveedores», armar el filtro en la pregunta, verlo con «Como si lo recibiera», publicar con el aviso y mandarle a alguien sin el dato.

**Datos de ejemplo:** Grupo Delta. Los datos «Zona», de un catálogo «Zonas», y «Sucursal», de texto. Seis personas, dos sin zona. «Pedir repuesto», con «Proveedores a consultar»: Juan Gómez, de zona Centro, ve 14 de 1.200.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
