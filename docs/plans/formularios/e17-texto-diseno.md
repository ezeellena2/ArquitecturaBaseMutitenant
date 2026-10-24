# E17 · Texto para el chat de Diseño

Se pega tal cual en el chat de Diseño, **después de cerrar los tableros de la E12, la E13 y la E16**: el editor de la plataforma reusa sus piezas.

---

Te paso los tableros de la E17, la biblioteca de plantillas, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Los tableros de la E17 están aprobados en la v126, la v130 y la v134. Con las decisiones 34 a 46 del 2026-10-24 cambian varias cosas:
- **40:** la biblioteca de plantillas es también la biblioteca de formularios estándar. Una plantilla puede traer su «Lectura de documentos», que se programa con la E19;
- **35 y 45:** el contenido no tiene idioma. Las plantillas y las categorías son un solo texto, sin «Textos en», sin pares en inglés y sin el inglés obligatorio;
- **43:** botones habilitados o grisados, sin textos al lado ni debajo, sin «Cambios sin guardar»; si falta un dato obligatorio, el botón sigue habilitado y al tocarlo se marca lo que falta;
- **el estado de una plantilla** (ajuste de la Guía con la 43): la lista y la banda dicen solo «Borrador», «Publicada» o «Retirada». Sale «Cambios sin publicar», porque «Publicar» habilitado ya lo dice, y el número de versión va solo en el historial de «Publicación».

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`:
1. **La spec:** `docs/plans/formularios/e17-biblioteca.md`, sobre todo la §8 (tableros, qué se redibuja y en qué mandan).
2. **La hoja de ruta:** `docs/plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md`, §5, decisiones 34 a 46.
3. **La evaluación:** `docs/plans/2026-10-24-formularios-evaluacion-traspaso-2.md`, §2.1, §2.2 y §5.
4. **ArquitecturaBase, como referencia de contenido:** DOC1 en el anexo D del traspaso 2 (`../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/anexo-d-tableros-desde-17-10.md`). Hacé `git fetch` antes.

**Qué se redibuja:**
- `Plat-FRM-Plantilla`, `Plat-FRM-Publicar`, `Plat-FRM-Retirar`, `Plat-FRM-Plantilla-Reglas`, `Plat-FRM-Pdf`, `Plat-FRM-Categorias` y `M-FRM-Plantilla`, con las reglas de arriba;
- `Plat-FRM-Biblioteca`, `M-Plat-FRM-Biblioteca` y `Plat-FRM-Plantilla-Nueva`, por el estado de la plantilla;
- en `Plat-FRM-Categorias`, el nombre es un solo campo.

**Qué se dibuja nuevo:** `DOC1` · La biblioteca y la lectura. La disposición se copia sin mostrar alternativas, con nuestras piezas y criterios (decisión 18):
- `FRM-Biblioteca` como la biblioteca de formularios estándar, con en qué formularios propios se usó cada plantilla;
- la sección «Lectura de documentos» de una plantilla, en la pestaña «Plantilla» de `Plat-FRM-Plantilla`: «Cómo reconocerlo», qué pregunta recibe cada dato estándar y las validaciones fijas, con candado.

**Datos de ejemplo:** las categorías Ventas, Logística y Mantenimiento; «Visita a cliente» (Ventas), publicada, con cambios guardados sin publicar, que la usaron 12 organizaciones; «Control de vehículo» (Logística), borrador; «Orden de trabajo» (Mantenimiento), retirada; y «Comprobante de transferencia», con su lectura de documentos.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado de la etapa (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
