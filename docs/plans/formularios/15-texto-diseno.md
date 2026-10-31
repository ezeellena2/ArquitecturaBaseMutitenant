# E15 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E15, respuestas, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** La E15 suma lo de las respuestas: la lista «Formularios» con sus columnas, las pestañas «Respuestas» y «Tablero» de cada formulario, la planilla, «Más filtros», la vista rápida, la pantalla completa, exportar y el tablero. **Los tableros muestran solo lo que existe hasta acá** (78): nada de fotos, documentos, aprobación, estado, PDF, corregir ni «Cargar». Valen las fichas del front: un dato por celda, en un renglón; fechas completas, con hora; sin cantidades en los encabezados de sección; sin rótulos que expliquen, y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/15-respuestas.md`, sobre todo «Qué se construye» y «Pantallas».

**Qué se corrige en lo que ya está dibujado:**
1. **`FRM-Respuestas`, `M-FRM-Respuestas` y `FRM-Respuestas-Exportar`:** salen las columnas «Fotos de la visita» y «Remito firmado», y «Por aprobar · 3» del panel. Canal va en su propia columna, después de Cargó, y dice «WhatsApp»: hoy es un ícono dentro de Cargó.
2. **`FRM-Respuestas-Filtros`:** sale la sección «Fotos de la visita». Mientras cuenta, el botón queda grisado con su mismo texto, sin «Contando…».
3. **`FRM-Respuestas-Exportar`:** mientras exporta, el botón queda grisado con su mismo texto, sin «Exportando…» ni el círculo.
4. **`FRM-Respuesta` y `M-FRM-Respuesta`:** salen la pestaña «Fotos» y «Ver una foto», y las fotos y el remito de la grilla. En `FRM-Respuesta` se suma «Abrir en pantalla completa» en la cabecera. `M-FRM-Respuesta` es la pantalla completa en el teléfono, con «Respuesta» y «Actividad» plegables.
5. **`FRM-Tablero`:** sale la tarjeta «Por estado». «Fallas» es la tarjeta de una pregunta de elegir varias.
6. **`FRM-Formularios-15`:** el estado «Solo lectura» es quien tiene `forms.read` sin `formrecords.read`: sin las columnas de respuestas.

**Qué se suma:**
1. **Un cliente en la planilla:** una fila con la marca «Cliente» y Empresas en «—», y una con «Persona sin información». En el teléfono, igual.
2. **`CA5` · «Elegir varias»:** en `FRM-Respuestas`, la columna «Fallas» con «Frío, Ruido» y el filtro «Fallas: alguna de 2»; en `FRM-Respuestas-Filtros`, sus opciones con cantidad, y una respuesta con dos opciones cuenta en las dos; en `FRM-Respuesta`, una opción por renglón, en el orden en que se eligieron.
3. **`CE5` · Los campos sellados:** en `FRM-Respuestas`, las subcolumnas «Proveedor · CUIT» y «Proveedor · Plazo de pago» después de su pregunta, con «—» en una respuesta de antes de los campos, y en «Columnas», para prenderlas o apagarlas; en `FRM-Respuesta`, cada opción con sus campos.
4. **`FRM-Respuesta-Completa` (nuevo, `DC0` y `DC1` ajustado):** la pantalla completa, a todo el ancho, en `/org/formularios/:id/respuestas/:recordId/completa`. El formulario va en las migas. Arriba, el número, «1 de 24» con ↑ ↓ y «Abrir conversación». Debajo, «Respuesta» y «Actividad», plegables. Actividad cuenta quién la mandó y quién la respondió, con fecha y hora. Sin «Descargar ▾», que hoy no tendría nada para bajar. Estados: las dos abiertas, las dos plegadas (cada una dice en una línea lo que tiene), una de una versión anterior, abierta desde un enlace (sin filtros privados), cargando y no existe.
5. **`M-FRM-Formularios-15` (nuevo):** la lista en el teléfono, con «N respuestas · estado» debajo del nombre.

**Recorrido guiado.** El de la etapa recorre la lista, la planilla, los filtros, la vista rápida, la pantalla completa y el tablero. Suma los circuitos `CA6` (de pegar los proveedores a verlos en la planilla y el Excel) y `CE6` (de armar los campos a ver el CUIT sellado en la planilla).

**Datos de ejemplo:** Grupo Delta. «Visita a cliente» para lo general; «Pedir repuesto», con «Fallas» y «Proveedor» de un catálogo con CUIT y Plazo de pago, para `CA5` y `CE5`.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
