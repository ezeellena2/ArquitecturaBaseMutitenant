# 05 · Pantallas

Cómo replicar las pantallas: **la misma disposición** (qué hay en cada zona, en qué orden, qué se abre en un diálogo o en un panel, qué estados existen y qué vive en la URL) con **los componentes y estilos de este proyecto**.

- **Lo implementado** (Etapas 1 a 4) está descripto zona por zona, con sus textos, en el [anexo B](anexo-b-front.md). Esta página da el mapa y agrega lo que todavía no existe en código: el detalle D2 y las pantallas de la Etapa 5.
- **Lo dibujado:** el lienzo «Formularios por WhatsApp» tiene cada tablero con sus notas numeradas. El texto de esas notas está en el [anexo D](anexo-d-tableros.md); las capturas, en `ArquitecturaBaseFront/docs/design/evidence/formularios-whatsapp/`.

## El caparazón

- **Menú lateral fijo,** de arriba hacia abajo: Inicio, Formularios, WhatsApp y, abajo, Administración y la cuenta. Formularios y WhatsApp no se despliegan dentro del menú: abren un **subpanel** de unos 224 px pegado al menú.
- **Subpanel «Formularios», en este orden** ([anexo B §1.2](anexo-b-front.md#12-subpanel-formularios-orden-exacto)):
  1. «Por aprobar (N)», con `formapprovals.decide`.
  2. La sección **«Respuestas»**: un enlace por formulario publicado (hasta 4, dados por el servidor) y «Ver todos (N)».
  3. Una línea separadora.
  4. «Diseñar formularios».
  5. «Catálogos».
  6. «Formularios que faltan» (Etapa 5).
- **Barra superior** con migas: `Inicio › Formularios › {pantalla} › {hoja}`.
- **Cabecera de cada pantalla:** volver (opcional), título y estado a la izquierda; acciones a la derecha. En el celular, el estado y las acciones bajan de renglón.
- **Celular** (menos de 768 px): el menú es un cajón y Formularios, un grupo desplegable dentro de él.
- **Piezas de disposición** ([anexo B §2](anexo-b-front.md#2-vocabulario-de-disposición-para-leer-el-resto)):
  - superficie con banda (título en versalitas a la izquierda, acciones a la derecha);
  - barra de filtros encima de la tabla;
  - tabla con paginado debajo;
  - acciones de fila con ícono;
  - diálogo central;
  - panel lateral derecho;
  - confirmación;
  - un único toast abajo a la derecha;
  - validación junto al campo.

## Mapa de pantallas

| Pantalla | Tablero | Disposición en una línea | Detalle |
| --- | --- | --- | --- |
| Diseñar formularios | A | Cabecera con «Nuevo formulario»; barra con búsqueda y el segmentado «Todos · Publicados · Borradores»; tabla Formulario / Preguntas / En WhatsApp / Último cambio / Editar. | [B · E1.1](anexo-b-front.md#e11-diseñar-formularios-listado--tablero-a) |
| Editor del formulario | B, C, D, E, F1, K6, 03, G1 | Una sola pantalla sin pestañas, en dos columnas: a la izquierda «Textos en», «El formulario» y «Preguntas» (filas plegables); a la derecha, la vista previa en un marco de celular. Debajo, a todo el ancho, «Cuando llega una respuesta». Acciones: Descartar, Guardar borrador, Publicar en WhatsApp y Ver vista oficial. | [B · E1.2](anexo-b-front.md#e12-editor-del-formulario-alta-y-edición--tableros-b-c-d-e-e1-1) |
| Publicar | P1–P3 | Diálogo «Qué incluye / Qué va a pasar», superficie de progreso en el editor y vista oficial de WhatsApp. | [B · E1.3](anexo-b-front.md#e13-publicar-en-whatsapp--p1-p2-p3) |
| Mandar desde el chat | C1–C5, K7 | Chat de tres zonas (lista de conversaciones, conversación y compositor). «Enviar formulario» en el compositor abre el diálogo: lista de formularios, mensaje, opciones preparadas y bloqueos. | [B · E1.4](anexo-b-front.md#e14-mandar-un-formulario-desde-el-chat--c1-a-c5) |
| Respuesta en el chat | R1–R3, F3, G6 | Tarjeta: cabecera con el formulario, valores en filas (miniaturas con «+N»), pie con «Guardada como …» o el rechazo con «Mandar de nuevo». Los avisos automáticos se ven como «Mensaje automático». | [B · E1.5](anexo-b-front.md#e15-la-respuesta-llega-al-chat--r1-a-r3-componentsformreplycardtsx-formreplyvaluestsx), [E2B.2](anexo-b-front.md#e2b2-en-la-tarjeta-del-chat--f3) |
| Catálogos | K1, K2 | Listado con conteos por origen y estado; «Nuevo catálogo» abre el diálogo de origen. | [B · E2.1–E2.2](anexo-b-front.md#e21-catálogos-listado--k1) |
| Catálogo Manual | K3 | Metadatos, opciones paginadas con mover arriba/abajo, «Así lo ve el empleado» y dar de baja/reactivar. | [B · E2.3](anexo-b-front.md#e23-editor-de-catálogo-manual--k3-pagescatalogeditorpagetsx-componentscatalogmanualitemseditortsx-catalogmetadatafieldstsx-catalogstateactiontsx) |
| Catálogo Filters | K4 | Columnas y condiciones, y vista previa «Como si lo recibiera» con una persona de ejemplo. | [B · E2.4](anexo-b-front.md#e24-editor-de-catálogo-filters--k4-pagescatalogfilterseditorpagetsx-componentscatalogfilterseditortsx) |
| Visor de fotos | F4 | Diálogo común: foto grande, anterior/siguiente, tira de miniaturas, pie con nombre, tamaño y Descargar. | [B · E2B.3](anexo-b-front.md#e2b3-visor-de-fotos--f4-shareduiphotoviewertsx-sharedui-formrecordfilestsx) |
| Respuestas (todos) | — | Listado de formularios publicados con buscador. | [B · E3.1](anexo-b-front.md#e31-respuestas-todos-los-formularios-pagesresponseformspagetsx) |
| Planilla | T1, T2, T4 | Cabecera con el formulario y Exportar; barra con búsqueda privada, filtros rápidos, «Más filtros» (panel lateral), el total y «Columnas»; tabla con columnas fijas más una por pregunta, que se desplaza a lo ancho. | [B · E3.2–E3.3](anexo-b-front.md#e32-la-planilla--t1-t2-pagesformresponsespagetsx) |
| Detalle (actual) | T3, G4, G5 | Página completa: Respuesta, De dónde vino, PDF y Qué pasó después. **Se reemplaza por D2** (abajo). | [B · E3.4](anexo-b-front.md#e34-detalle-de-una-respuesta--t3-implementación-actual-pagesformresponsedetailpagetsx) |
| Exportar | T5 | Diálogo: qué filtros se aplican, todas las preguntas o solo las visibles, el tope de 10.000. | [B · E3.5](anexo-b-front.md#e35-exportar-a-excel--t5-componentsresponseexportdialogtsx) |
| Reglas | G1 | Sección del editor: reglas en palabras (cuándo, si, hacer en orden), una abierta para editar, «Guardar reglas» e historial de revisiones. | [B · E4.1](anexo-b-front.md#e41-cuando-llega-una-respuesta-reglas--g1-componentsformrulessectiontsx) |
| Diseño del PDF | G2 | Dos columnas: a la izquierda «El comprobante» (nombre, preguntas, «Incluir las fotos», quién aprobó, pie y «Guardar diseño»); a la derecha, la vista previa con el selector de idioma. | [B · E4.2](anexo-b-front.md#e42-diseño-del-pdf--g2-pagesformpdfdesignpagetsx-componentsformpdfpreviewtsx) |
| Por aprobar | G3 | Bandeja con «Para mí / Todas», de la más vieja a la más nueva. | [B · E4.3](anexo-b-front.md#e43-por-aprobar--g3-pagesapprovalinboxpagetsx) |

## Detalle de una respuesta: D2, panel al costado (elegido el 2026-10-17)

Reemplaza el detalle de página completa (T3, G4 y G5). Todavía no está programado en ArquitecturaBase: se hace dentro de la Etapa 5, antes del asistente.

**Dónde se abre:** tocar una fila de la planilla o de «Por aprobar» abre el panel **a la derecha, sin salir de la lista**. La planilla queda a la vista, con sus filtros y su orden, y la fila abierta queda marcada. El panel tapa las columnas de la derecha. La URL lleva la respuesta abierta, así que se puede compartir y recargar.

**Disposición del panel, de arriba hacia abajo:**

1. **Cabecera:**
   - a la izquierda, el número (`VIS-000024`) y la insignia del estado (Aprobada, Por aprobar o Rechazada);
   - a la derecha, ↑ y ↓ (respuesta anterior y siguiente de la planilla, también con las flechas del teclado) y ✕ para cerrar. Escape cierra y el foco vuelve a la fila.
2. **Renglón de contexto:** «Registrar visita · versión 3» a la izquierda; a la derecha, **«Abrir conversación»** y **«Descargar ▾»** (el PDF; las fotos juntas quedan como idea para después).
3. **Momentos del estado**, uno por renglón, cada uno con su marca (hecho, esperando o con problema):
   - **Recibida** · fecha y hora · «Se lo mandó Ana Pérez» (o «La ofreció el asistente» en la Etapa 5).
   - **Al recibirla** · «1 de 1 hecha».
   - **Por aprobar** · «Supervisor · hace 2 horas», con **Rechazar** y **Aprobar** en ese mismo renglón mientras espera. Ya decidida, queda **Aprobada** · «Laura Ríos · 9 oct, 12:30» o **Rechazada** con su motivo.
   - **Al aprobarla** (o **Al rechazarla**) · «2 de 2 hechas», o «1 sin confirmar».

   Los momentos se arman con **las reglas que tenía el formulario cuando llegó esa respuesta**: un formulario sin reglas muestra solo «Recibida», y lo que no aplica no aparece. Así funciona con cualquier cantidad de acciones.
4. **Pestañas:**
   - **Respuesta:** las preguntas de su versión como filas «pregunta · valor», con las fotos en miniatura (abren el visor F4) y los documentos con nombre, tamaño y descarga.
   - **Fotos (N):** solo si hay fotos.
   - **Actividad:** cada momento con sus líneas en orden de tiempo, con hora, qué pasó y su estado a la derecha (Hecho, Leído, Sin confirmar con «Consultar», No se pudo con el motivo y «Reintentar»); el encabezado dice «Reglas de la revisión N».
   - **PDF:** solo si hay copia fija; con Ver y Descargar.
5. **Pie:** «↑ ↓ pasan de respuesta · Esc cierra el panel».

**En el celular:** no hay costado. Tocar una fila abre la respuesta en toda la pantalla, con ↑ ↓ en la cabecera; volver deja la planilla en la misma fila. Mientras espera decisión, Aprobar y Rechazar quedan fijos abajo.

## Pantallas de la Etapa 5 (dibujadas, sin programar)

- **03 · «Cuándo lo ofrece el asistente»:** un campo de texto largo en la superficie «El formulario» del editor, a todo el ancho, con la ayuda «El asistente lee el mensaje y, si coincide con esto, ofrece el formulario. No guarda nada sin que la persona confirme.».
- **H1 · El chat en modo Asistente:**
  - en la cabecera de la conversación, la marca «Asistente»;
  - en la conversación, el mensaje de Juan, la respuesta del asistente (con el rótulo «Asistente» y la tarjeta del formulario prellenado), una caja punteada **«Por qué lo mandó»** (qué reconoció y de dónde salió cada dato), la tarjeta de la respuesta guardada y el **«Mensaje automático»** de la regla;
  - en lugar del compositor, una franja: «El asistente atiende esta conversación. Para escribirle vos, tomá la atención manual.» con el botón **«Tomar atención manual»**.
- **H2 · En atención manual, sugiere:** arriba del compositor, una caja con «El asistente sugiere mandarle Registrar visita», los datos prellenados, **«No, gracias»** y **«Revisar y enviar»**, que abre el diálogo de envío con los prellenados para destildar.
- **H3 · Cambiar de modo:**
  - una confirmación para pasar a Asistente;
  - el aviso al tomar la atención en medio de un envío;
  - el aviso «El asistente no está disponible…»;
  - los chats existentes que quedan en Atención manual;
  - la marca en la lista de chats;
  - sin permiso, el modo se ve pero el botón no.
- **H4 · Catálogo propuesto:** el pedido llega por WhatsApp y la propuesta se abre en la misma pantalla de K4, con el banner «Propuesta del asistente. La pidió … No se usa hasta que alguien la apruebe.».
- **H5 · Los límites:** seis casos en el chat:
  - pregunta con botones si duda;
  - una consulta o una charla no se contesta;
  - después de un «ahora no», no insiste;
  - sin formulario o sin permiso, avisa y anota el pedido;
  - fuera de las 24 horas, espera a que Juan escriba;
  - todo queda en el historial.
- **H6 · Audios:**
  - en el chat, una burbuja de audio con reproducir, la forma de onda, la duración y la marca «Audio»;
  - debajo, la **transcripción** en una franja aparte;
  - los estados: pasando a texto, transcripto, leído por el asistente, sugerencia en atención manual, «No pudimos pasar este audio a texto…» (queda sin leer para un operador) y borrado a los 90 días.
- **H7 · Formularios que faltan:**
  - entrada propia en el subpanel de Formularios;
  - cabecera con una bajada explicativa;
  - barra con búsqueda por tema, el segmentado «Pendientes (N) · Descartados (N)» y el total «N temas · N pedidos»;
  - tabla Tema (nombre y una frase) / Pedidos / Personas / Último pedido / Acciones (**Crear formulario**, **Descartar**);
  - sin nombres, teléfonos ni mensajes.
- **s1–s4 · Sugerencias entre formularios:**
  - en el editor, una sección «Qué sugerir después» con su propio «Guardar sugerencias». Cada sugerencia dice el formulario destino, cuándo (texto para la IA o una condición), el modo («La IA decide» o «Preguntar siempre») y qué datos pasa;
  - en el celular, después de responder, el ofrecimiento del siguiente formulario con «Abrir formulario»;
  - en el detalle, la cadena de origen.

## Cómo adaptar

- **Respetar:**
  - el orden de las zonas y de las acciones (por ejemplo, Descartar · Guardar borrador · Publicar);
  - qué se abre en diálogo y qué en panel;
  - los textos en voseo;
  - los estados de cada pantalla;
  - qué vive en la URL (página, tamaño, orden, columnas, la respuesta abierta) y qué nunca (búsquedas en textos, filtros por pregunta, personas, borradores, motivos).
- **Cambiar sin problema:** componentes, colores, tipografía y espaciados: van los de este proyecto.
- **Con la regla de idioma nueva:** el editor ya no necesita marcar «N pendientes» como bloqueo para publicar; la traducción es opcional. Ajustar el control «Textos en» y el chequeo de publicación al dibujar el tablero de acá.
- **Lo que suma el multitenant:**
  - la empresa en la planilla (columna o filtro);
  - la biblioteca de plantillas de la plataforma (F3), con su propia pantalla de plataforma y «Usar plantilla» en el alta;
  - «Por aprobar» dentro de «Para resolver».

  Cada uno necesita su tablero.
