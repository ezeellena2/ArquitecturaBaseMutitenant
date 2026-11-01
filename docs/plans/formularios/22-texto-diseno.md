# E22 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, **después de cerrar los tableros de la E12, la E14 y la E21**: el editor de la plataforma reusa sus piezas.

---

Te paso los tableros de la E22, la biblioteca de plantillas, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Los tableros de la E22 están aprobados en la v126, la v130 y la v134. Con las decisiones 34 a 82 del 2026-10-24 y del 2026-10-25 cambian varias cosas. **Los tableros de una etapa muestran solo lo que existe hasta ella** (78): nada de la lectura de documentos, que llega con la E25. Además:
- **40:** la biblioteca de plantillas es también la biblioteca de formularios estándar;
- **79:** en la organización, «Nuevo formulario» de la lista «Formularios» pasa a «Nuevo formulario ▾», con «Desde cero» y «Desde una plantilla»;
- **82:** el logo y los datos del encabezado del PDF salen de la página pública de cada organización, y el color se elige en el diseño del PDF de cada formulario;
- **35 y 45:** el contenido no tiene idioma. Las plantillas y las categorías son un solo texto, sin «Textos en», sin pares en inglés y sin el inglés obligatorio;
- **43:** botones habilitados o grisados, sin textos al lado ni debajo, sin «Cambios sin guardar»; si falta un dato obligatorio, el botón sigue habilitado y al tocarlo se marca lo que falta;
- **50:** cada plantilla dice para quién es su formulario: empleados, clientes o los dos;
- **59:** el diseño del PDF tiene una base con secciones, mostrar u ocultar preguntas y firmas; el logo y el color los pone cada organización;
- **61 y 62:** en las reglas de ejemplo, la persona de «Avisar a una persona», la dirección de «Mandar por correo» y la plantilla de WhatsApp quedan «Lo elige cada organización», como el rol;
- **64:** una plantilla puede traer catálogos de los miembros y de los contactos, sin filtros; la pestaña «Catálogos» muestra el origen de cada uno;
- **el estado de una plantilla** (ajuste de la Guía con la 43): la lista y la banda dicen solo «Borrador», «Publicada» o «Retirada». Sale «Cambios sin publicar», porque «Publicar» habilitado ya lo dice, y el número de versión va solo en el historial de «Publicación».

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`:
1. **La spec:** `docs/plans/formularios/22-biblioteca.md`, sobre todo la §8 (tableros, qué se redibuja y en qué mandan).
2. **La hoja de ruta:** `docs/plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md`, §5, decisiones 34 a 82.
3. **La evaluación:** `docs/plans/2026-10-24-formularios-evaluacion-traspaso-2.md`, §2.1, §2.2 y §5.

**Qué se redibuja:**
- `Plat-FRM-Plantilla`, `Plat-FRM-Publicar`, `Plat-FRM-Retirar`, `Plat-FRM-Plantilla-Reglas`, `Plat-FRM-Pdf`, `Plat-FRM-Categorias` y `M-FRM-Plantilla`, con las reglas de arriba;
- `Plat-FRM-Biblioteca`, `M-Plat-FRM-Biblioteca` y `Plat-FRM-Plantilla-Nueva`, por el estado de la plantilla;
- `FRM-Editor-Reglas-Plantilla` y `M-FRM-Reglas-Plantilla`: las reglas que llegan de una plantilla, en la pestaña de reglas del formulario;
- en `Plat-FRM-Categorias`, el nombre es un solo campo.

**Qué se revisa:** `FRM-Formularios-22`, que parte de `FRM-Formularios-21`, con «Cargar», «Solo en la app», quien solo carga y «Borrar», y `FRM-Plantilla`, `FRM-Plantilla-Usar` y sus teléfonos.

**También se redibuja** `FRM-Biblioteca`, sin su sección de lectura de documentos y sin mostrar alternativas, con nuestras piezas y criterios: la biblioteca de formularios estándar, con en qué formularios propios se usó cada plantilla, a la que se llega desde «Desde una plantilla».

**Datos de ejemplo:** las categorías Ventas, Logística y Mantenimiento; «Visita a cliente» (Ventas), publicada, con cambios guardados sin publicar, que la usaron 12 organizaciones; «Control de vehículo» (Logística), borrador; «Orden de trabajo» (Mantenimiento), retirada; y «Comprobante de transferencia», publicada.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado de la etapa (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
