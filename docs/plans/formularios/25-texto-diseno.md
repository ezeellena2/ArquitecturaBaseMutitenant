# E25 · Texto para el chat de Diseño

**Estado:** pendiente · Diseño lo toma en su turno.

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de la E25, documentos por WhatsApp, para el lienzo del multitenant. No hay que programar nada: solo tableros.

**Contexto.** Cuando un empleado o un cliente manda un documento, la IA reconoce qué es, saca los datos y propone el formulario ya completo. La persona confirma. Valen las fichas del front: un dato por celda, fechas completas, sin rótulos que expliquen, y botones habilitados o grisados (43).

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`: la spec `docs/plans/formularios/25-documentos.md`. La referencia son `DOC1` a `DOC6`, en `ArquitecturaBaseFront/docs/design/documentos.md`, y la maqueta, en `areas/business/forms/documents/`.

**Qué se dibuja:**
1. **`WA-Chats-Documento` y su teléfono (`DOC2` y `DOC4`):** seguro, con duda (botones, o lista si un nombre no entra) y sin reconocer. PDF, Word, Excel con varias filas, ubicación y contacto. Los casos difíciles: foto movida, PDF con contraseña, repetido, CBU mal leído, formulario que no se le puede mandar, formatos viejos y un número que no es contacto. «Por qué lo propuso» debajo del documento.
2. **`M-WA-Flow-Prellenado` (`DOC3`):** el formulario en el celular: lo dudoso con «Revisá», lo no leído vacío con «No se leyó» y el documento adjunto. Dibujá las dos variantes: con «Cambiar documento», y con «Adjuntamos tu documento», para el caso en que Meta no deje precargar un archivo.
3. **`FRM-Documentos` y su teléfono (`DOC5-A`):** en el panel de «Gestión de formularios», debajo de «Formularios». La bandeja, una planilla con panel: el documento con acercar, girar, páginas y descargar; sus datos; cambiar el formulario reconocido; descartar con confirmación; pasar al formulario; ↑ ↓. Filtros: formulario, quién lo mandó, cuándo llegó, estado y «Empresa ▾». Estados: propuesto, sin clasificar, confirmado, descartado y «Cargada automáticamente». Con solo `formrecords.read`, sin revisar.
4. **`FRM-Editor-Lectura` y su teléfono (`DOC1`):** la sección «Lectura de documentos» en el editor: «Cómo reconocerlo», qué pregunta recibe cada dato, las validaciones fijas, el cruce con catálogos, los duplicados, el interruptor «Cargar solo si es seguro» y cuántas cargas sin correcciones lleva.
5. **`Plat-FRM-Plantilla-Lectura` (`DOC1`):** la misma sección en la pestaña «Plantilla» de Plataforma, con las validaciones con candado.
6. **`FRM-Armar-Con-IA` y su teléfono (`DOC6`):** desde un sin clasificar o desde «Llegaron N documentos parecidos»: la propuesta, con los documentos a la vista, y «Crear borrador».
7. **`FRM-Respuesta-25` y `M-FRM-Respuesta-25`:** sobre `FRM-Respuesta`, de qué documento salió y quién la revisó.
8. **`Campana-25` y su teléfono:** los documentos por revisar y «Llegaron N documentos parecidos».
9. **`WA-Asistente-Casos-25`:** sobre `WA-Asistente-Casos`, el tipo de entrada «documento».
10. **`Asistente-Org-25` y su teléfono:** en el chat del Inicio, el asistente recibe un PDF y arma un borrador, con los catálogos por aprobar, y lleva al editor.

**Recorrido guiado.** Un comprobante de transferencia: llega, se reconoce, se propone, se confirma y queda en la planilla. Y otro que no se reconoce, que termina en «Armar formulario con la IA».

**Datos de ejemplo:** Grupo Delta. «Comprobante de transferencia», con varios bancos y billeteras. Siempre datos ficticios.

**Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo con el recorrido guiado (44) y avisás la versión y la lista de tableros, para anotarlos en la spec.
