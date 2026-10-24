# E13 · Texto para el chat de Diseño

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E13, catálogos base, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Los tableros de catálogos están aprobados en la v129 y la v130. Con las decisiones 34 a 44 del 2026-10-24, la E13 crece al alcance de lo que ArquitecturaBase llama E1: tipo de valor, carga de una vez (pegar, importar y exportar), «elegir varias» con un catálogo y hasta 10.000 opciones. Además:
- **35:** el contenido no tiene idioma: un solo texto por campo, sin «Textos en» ni pares en inglés;
- **43:** botones habilitados o grisados, sin textos al lado ni debajo, sin «Cambios sin guardar»; lo que necesita algo antes queda grisado, con el motivo en su tooltip;
- **el origen y el tipo de valor** de un catálogo van como la A11: en su campo, apagados y en gris, sin «No se cambia»;
- **55:** un catálogo puede salir también de los contactos de WhatsApp de la organización, con filtros por etiqueta, mostrando solo el nombre.

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`:
1. **La spec:** `docs/plans/formularios/e13-catalogos.md`, sobre todo la §10 (tableros y estados) y las decisiones C11 a C17.
2. **La hoja de ruta:** `docs/plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md`, §5, decisiones 34 a 44, 55 y 56.
3. **La evaluación:** `docs/plans/2026-10-24-formularios-evaluacion-traspaso-2.md`, §2.1, §2.2 y §5.
4. **ArquitecturaBase, como referencia de contenido:** el anexo D del traspaso 2 (`../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/anexo-d-tableros-desde-17-10.md`), CA1-A a CA6. Varios de allá violan la regla de botones: se dibujan corregidos. Hacé `git fetch` antes.

**Qué se redibuja:** `FRM-Catalogo`, `M-FRM-Catalogo`, `FRM-Catalogo-Filtros`, `M-FRM-Catalogo-Filtros` y `FRM-Catalogo-Propuesta`, sin «Textos en», sin «Cambios sin guardar» y con el origen como la A11.

**Qué se dibuja nuevo** (la disposición se copia, sin mostrar alternativas, con nuestras piezas y criterios; decisión 18):
1. **`CA1-A` · Todo a la vista:** el editor de un catálogo a mano con la barra de Opciones (Agregar opción, Pegar desde Excel, Importar, Exportar y «⋯» con la plantilla, ordenar de la A a la Z y generar los códigos que faltan), la búsqueda, la tabla editable en la compu, la selección de filas, y «Así lo ve quien lo recibe» con «Una opción / Varias». Casos: vacío, 1.200 opciones con páginas, el lápiz, un texto repetido, solo lectura y el celular.
2. **`CA2` · Importar (y pegar):** archivo o filas pegadas, revisar (Errores, Altas, Cambios y Bajas, por fila y columna) y aplicar, con la confirmación de bajas, «Aplicar sin las N filas con errores», un valor ambiguo, el 409 y otra importación en curso.
3. **`CA3` · Tipo de valor:** se elige al crear, la columna «Valor», el error en el campo, y el tipo en el editor de formularios, el Excel y el PDF.
4. **`CA4` · Elegir varias:** «Permitir elegir varias» en la pregunta de catálogo, con mínimo y máximo, «varias, hasta N» en la lista de preguntas, la vista previa con casillas y un catálogo de más de 20.
5. **`CA5` · Las respuestas con varias opciones:** la planilla con «alguna de», el detalle, el chat, el Excel con «; » y el PDF.
6. **`CA6` · El circuito de punta a punta:** de pegar los proveedores a verlos en la planilla.
7. **`FRM-Catalogo-Contactos`:** un catálogo «De los contactos de WhatsApp», con filtros por etiqueta y vista previa, y la tercera tarjeta en `FRM-Catalogo-Nuevo`.

**Datos de ejemplo:** Grupo Delta. «Proveedores», a mano, de texto, con 1.097 opciones; «Recargo por zona», a mano, decimal; «Supervisores», de los miembros con filtros; «Clientes», de los contactos; y el formulario «Pedir repuesto» con una pregunta de «elegir varias» de 1 a 3.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado de la etapa (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
