# E12 · Texto para el chat de Diseño

**Estado:** cerrado · Diseño ya dibujó estos tableros.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E12, catálogos, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Los tableros de catálogos están aprobados en la v129 y la v130. Con las decisiones del 2026-10-24 y del 2026-10-25, la E12 es la primera etapa de formularios y llega antes que los formularios (78): es el ABM de catálogos, con tipo de valor, carga de una vez (pegar, importar y exportar) y hasta 10.000 opciones. **Los tableros de esta etapa muestran solo lo que existe hasta ella:** en el panel de «Gestión de formularios», solo «Catálogos»; nada de preguntas, formularios, «Lo usan» ni respuestas. Además:
- **35:** el contenido no tiene idioma: un solo texto por campo, sin «Textos en» ni pares en inglés;
- **43:** botones habilitados o grisados, sin textos al lado ni debajo, sin «Cambios sin guardar»; lo que necesita algo antes queda grisado, con el motivo en su tooltip;
- **el origen y el tipo de valor** de un catálogo van como la A11: en su campo, apagados y en gris, sin «No se cambia»;
- **55:** un catálogo puede salir también de los contactos de WhatsApp de la organización, con filtros por etiqueta, mostrando solo el nombre;
- **81:** un contacto sin nombre aparece con el de su perfil de WhatsApp; el teléfono nunca se muestra.

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`:
1. **La spec:** `docs/plans/formularios/12-catalogos.md`, sobre todo la §10 (tableros y estados).
2. **La hoja de ruta:** `docs/plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md`, §5, decisiones 34 a 44, 55, 78 y 81.
3. **La evaluación:** `docs/plans/2026-10-24-formularios-evaluacion-traspaso-2.md`, §2.1, §2.2 y §5 (con los números viejos de las etapas; la equivalencia está en `docs/plans/formularios/README.md`).
4. **ArquitecturaBase, como referencia de contenido:** el anexo D del traspaso 2 (`../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/anexo-d-tableros-desde-17-10.md`), CA1-A a CA3. Varios de allá violan la regla de botones: se dibujan corregidos. Hacé `git fetch` antes.

**Qué se redibuja:** `FRM-Catalogo`, `M-FRM-Catalogo`, `FRM-Catalogo-Filtros` y `M-FRM-Catalogo-Filtros`, sin «Textos en», sin «Cambios sin guardar» y con el origen como la A11. `FRM-Catalogos` y `M-FRM-Catalogos`, sin la columna «Lo usan».

**Qué se dibuja nuevo** (la disposición se copia, sin mostrar alternativas, con nuestras piezas y criterios; decisión 18):
1. **`CA1-A` · Todo a la vista:** el editor de un catálogo a mano con la sección «Opciones»: en su banda, «+ Agregar opción» y un ⋮ con Pegar desde Excel, Importar, Exportar, Descargar la plantilla, Ordenar de la A a la Z y Generar los códigos que faltan; abajo, solo «Buscar opción» y el filtro de estado, sin cantidades en el encabezado; la tabla editable en la compu, la selección de filas, y el teléfono al costado, sin «Una opción | Varias»: lo decide la pregunta. En cada fila, solo el ⋮, con «Editar» adentro (v143). Casos: vacío, 1.200 opciones con páginas, editar una opción, un texto repetido, solo lectura y el celular.
2. **`CA2` · Importar (y pegar):** archivo o filas pegadas, revisar (Errores, Altas, Cambios y Bajas, por fila y columna) y aplicar, con la confirmación de bajas, «Aplicar sin las N filas con errores», un valor ambiguo, el 409 y otra importación en curso.
3. **`CA3` · Tipo de valor:** se elige al crear, la columna «Valor» y el error en el campo.
4. **`FRM-Catalogo-Contactos`:** un catálogo «De los contactos de WhatsApp», con filtros por etiqueta y vista previa, y la tercera tarjeta en `FRM-Catalogo-Nuevo`.

**Datos de ejemplo:** Grupo Delta. «Proveedores», a mano, de texto, con 1.097 opciones; «Recargo por zona», a mano, decimal; «Supervisores», de los miembros con filtros; y «Clientes», de los contactos, con uno que tomó el nombre de su perfil.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado de la etapa (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
