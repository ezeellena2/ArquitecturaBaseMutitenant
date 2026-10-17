# Formularios por WhatsApp: inventario del front para replicar las pantallas

**Repositorio relevado:** `C:\Users\ezequ\source\repos\ArquitecturaBaseFront`, rama `main`, último commit
`f8212b2` (2026-10-17), más los cambios **sin commit** que estaba haciendo Codex al momento del relevamiento (ver
«Trabajo en curso» abajo). Todo lo que sigue sale del código; cuando algo figura en el diseño pero no en el código, se
dice explícitamente.

**Para quién es:** un equipo que quiere armar las mismas pantallas en otro sistema. Se describe la **disposición**
(qué hay en cada zona, en qué orden, qué se abre en diálogo o en panel lateral, qué estados existen y qué textos se
ven), no los colores ni los componentes concretos. Los nombres de componentes (`Page`, `Surface`, `DataTable`…)
aparecen solo para que se entienda qué "pieza de disposición" es cada cosa; el apartado 2 explica qué significa cada una.

**Fuentes leídas antes del código:** `AGENTS.md` raíz, `docs/architecture.md`, `docs/guides/premisas-de-desarrollo.md`,
`docs/guides/mensajes-y-estados.md`, `src/features/business-forms/AGENTS.md`, `src/features/whatsapp/AGENTS.md` y
`docs/design/formularios-whatsapp.md` (registro de tableros elegidos, con los códigos A–E, P1–P3, C1–C5, R1–R3,
K1–K7, F1–F5, T1–T5, G1–G6 y D2 que se usan acá como referencia).

---

## 0. Estado por etapa (qué existe en el código)

| Etapa | Tableros | Estado en el código del front |
| --- | --- | --- |
| 1. El formulario y el chat | A (listado), B (alta), C (prefijo), D (conflicto), E (dos idiomas), P1–P3 (publicar), C1–C5 (enviar desde el chat), R1–R3 (respuesta en el chat) | Implementado |
| 2. Catálogos | K1 listado, K2 origen, K3 Manual, K4 Filters, K6 pregunta de catálogo, K7 preparación al enviar | Implementado. K5 (catálogo "con una consulta") **postergado**: no se ofrece ni deshabilitado |
| 2B. Fotos y documentos | F1–F5 | Implementado |
| 3. Planilla | T1–T5 | Implementado (detalle a página completa, ver nota D2) |
| 4. Reglas, PDF, aprobación, avisos | G1–G6 | Implementado. Hay un cambio **en curso sin commit** en G5 (revisión por destinatario) |
| 5. Asistente | H1–H7, s1–s4 | **Nada implementado en el front.** El modo "Bot" del chat se muestra como «Automatización sin configurar» |
| Rediseño del detalle | D2 (panel lateral sobre la planilla, elegido el 2026-10-17) | **No implementado.** El detalle sigue siendo la página T3/G4/G5 descripta en la Etapa 3–4 |

**Trabajo en curso (sin commit, 2026-10-17):** `ResponseWorkflowHistory.tsx`, `workflow.ts`, `api/workflow.ts`,
`useResponseWorkflow.ts` y `forms.json` (es/en) están cambiando para que «Resolver envío» se haga **por destinatario**
de un aviso (cada persona de un rol con su estado), no por acción completa. Se describe el estado del árbol de trabajo
y se marca como "en curso".

---

## 1. Mapa de navegación

### 1.1 Menú lateral (escritorio)

De arriba hacia abajo (`src/layouts/components/Sidebar.tsx`, `src/layouts/navigation.ts`):

1. Marca/encabezado del menú.
2. Grupo General: **Inicio** (`/`).
3. Línea separadora + botón de panel **Formularios** (ícono de portapapeles). Aparece si la persona tiene al menos uno de:
   `formtemplates.read`, `formcatalogs.read`, `formrecords.read`, `formapprovals.decide`.
4. Línea separadora + botón de panel **WhatsApp** (si tiene `whatsapp.read`), que abre un panel con **Chat**.
5. Abajo de todo, separado por línea: botón de panel **Administración**.
6. Pie: avatar con inicial, nombre y correo/teléfono de la cuenta.

Formularios, WhatsApp y Administración **no se despliegan dentro del menú**: abren un **subpanel** (una segunda
columna de ~224 px pegada al menú). El subpanel tiene: encabezado con ícono + nombre del panel («Formularios»), un
botón en el borde para cerrarlo («Cerrar el panel Formularios») y la lista de enlaces.

- El subpanel **se abre solo al entrar** a una ruta de la sección (cualquier `/formularios…` o `/catalogos…`) y se
  cierra al salir de la sección; adentro se puede cerrar y abrir a mano.
- Con el menú contraído (solo íconos) el subpanel igual se ve expandido.

### 1.2 Subpanel «Formularios»: orden exacto

1. **«Por aprobar (N)»** — solo con `formapprovals.decide`. N es la cantidad que trae el servidor; mientras carga se ve
   un bloque de carga debajo; si falla, un botón «Reintentar». La cantidad se pide recién al entrar a Formularios o
   abrir el panel.
2. **Sección «Respuestas»** (rótulo en mayúsculas chicas) — solo con `formrecords.read`:
   - Un enlace por formulario publicado que la persona puede ver, **hasta 4** (página 1 de tamaño 4 del servidor),
     cada uno a `/formularios/{id}/respuestas`, con el nombre del formulario.
   - **«Ver todos (N)»** a `/formularios/respuestas` si hay más de 4; **«Ver todos»** (sin número) si no hay ninguno.
   - Cargando sin datos: bloque de carga. Error: botón «Reintentar».
   - La sección termina con una línea separadora.
3. **«Diseñar formularios»** (`/formularios`) — con `formtemplates.read`. No queda marcado como activo cuando se está
   en una ruta de respuestas o de Por aprobar, aunque sean hijas de `/formularios`.
4. **«Catálogos»** (`/catalogos`) — con `formcatalogs.read`.

Los formularios del panel **no están fijos en la configuración del menú**: los compone `FormResponsesShell` con la
lista paginada del backend (`GET /api/forms/navigation`).

### 1.3 Celular (menos de 768 px)

- El menú es un **cajón** que se abre con ☰ desde la barra superior, con fondo oscurecido; Escape lo cierra.
- Formularios, WhatsApp y Administración se vuelven **grupos desplegables dentro del cajón** (no hay subpanel al costado).
  Al desplegar Formularios aparecen, en este orden: «Por aprobar (N)», la sección «Respuestas» con sus enlaces y
  «Ver todos», «Diseñar formularios» y «Catálogos». Tocar un enlace cierra el cajón.

### 1.4 Migas (barra superior)

- Rutas normales de la sección: `Inicio › Formularios › {pantalla del menú}` y, en una ruta hija,
  `Inicio › Formularios › {enlace padre} › {hoja que pone la pantalla}`. Ejemplos:
  - `/formularios/{id}`: `Inicio › Formularios › Diseñar formularios › {nombre original del formulario o «Nuevo formulario»}`.
  - `/catalogos/{id}`: `Inicio › Formularios › Catálogos › {nombre del catálogo o «Nuevo catálogo»}`.
  - `/formularios/{id}/pdf`: `Inicio › Formularios › Diseñar formularios › Diseño del PDF`.
  - `/formularios/por-aprobar`: `Inicio › Formularios › Por aprobar`.
- Rutas de respuestas (migas propias, `ResponseBreadcrumbs`):
  - `/formularios/respuestas`: `Inicio › Formularios › Respuestas` (Formularios enlaza a `/formularios/respuestas`).
  - `/formularios/{id}/respuestas`: `Inicio › Formularios › {nombre del formulario}`.
  - `/formularios/{id}/respuestas/{recordId}`: `Inicio › Formularios › {nombre del formulario (enlace a la planilla)} › {número de la respuesta}`.

### 1.5 Rutas, permisos y archivo

Todas en `src/app/routes.tsx`, cargadas bajo demanda. Sin el permiso, la ruta redirige a `/sin-permiso`
(página «No tenés permiso»). Un fallo al leer los permisos muestra un estado de error con «Reintentar» (no es "sin permiso").

| Ruta | Pantalla | Permiso de la ruta | Archivo |
| --- | --- | --- | --- |
| `/formularios` | Diseñar formularios (listado) | `formtemplates.read` | `pages/FormsPage.tsx` |
| `/formularios/nuevo` | Nuevo formulario (editor vacío) | `formtemplates.read` + `formtemplates.manage` | `pages/FormEditorPage.tsx` |
| `/formularios/{formId}` | Editor del formulario (incluye «Cuando llega una respuesta») | `formtemplates.read` | `pages/FormEditorPage.tsx` |
| `/formularios/{formId}/pdf` | Diseño del PDF | `formrules.manage` (solo ese) | `pages/FormPdfDesignPage.tsx` |
| `/formularios/por-aprobar` | Por aprobar | `formapprovals.decide` | `pages/ApprovalInboxPage.tsx` |
| `/formularios/respuestas` | Respuestas: todos los formularios publicados | `formrecords.read` | `pages/ResponseFormsPage.tsx` |
| `/formularios/{formId}/respuestas` | Planilla de respuestas | `formrecords.read` | `pages/FormResponsesPage.tsx` |
| `/formularios/{formId}/respuestas/{recordId}` | Detalle de una respuesta | `formrecords.read` | `pages/FormResponseDetailPage.tsx` |
| `/catalogos` | Catálogos (listado) | `formcatalogs.read` | `pages/CatalogsPage.tsx` |
| `/catalogos/nuevo` (y `?kind=Filters`) | Nuevo catálogo Manual o Filters | `formcatalogs.read` + `formcatalogs.manage` | `pages/CatalogEditorPage.tsx` → `CatalogFiltersEditorPage.tsx` |
| `/catalogos/{catalogId}` | Editor del catálogo | `formcatalogs.read` | idem |
| `/whatsapp/chat` | Chat de WhatsApp (envío de formularios y tarjetas de respuesta) | `whatsapp.read` | `features/whatsapp/pages/WhatsAppChatPage.tsx` |

Los archivos de `business-forms` están en `src/features/business-forms/`. Las tres rutas de respuestas comparten un
**layout invisible** (`ResponseWorkspaceLayout`) que guarda en memoria el filtro privado del formulario abierto.

### 1.6 Qué vive en la URL y qué nunca

| Ruta | Parámetros en la URL (nombres del backend) |
| --- | --- |
| `/formularios` | `page`, `pageSize`, `sort` (`name`, `questionCount`, `lastChangedAtUtc`, con `-` para descendente), `search`, `publicationFilter` (`Published` o `Drafts`; ausente = Todos) |
| `/catalogos` | `page`, `pageSize`, `sort`, `search`, `kind` (`Manual`/`Filters`), `state` (`Active`/`Inactive`/`Unavailable`) |
| `/catalogos/nuevo` | `kind=Filters` elige el editor Filters |
| `/catalogos/{id}` (Manual) | `page`, `pageSize`, `search` del listado de opciones del catálogo |
| `/formularios/respuestas` | `page`, `pageSize`, `search` (busca por nombre de formulario) |
| `/formularios/{id}/respuestas` y su detalle | `page`, `pageSize`, `sort` (`number`, `submittedAtUtc`, `actor`, `field`, con `-`), `sortFieldId` (GUID de la pregunta cuando `sort=field`), `versionIds` (lista de GUID), `columns` (lista de FieldId visibles, o `none`; ausente = todas). El detalle conserva los mismos parámetros para volver a la planilla igual |
| `/formularios/por-aprobar` | `view=all` (ausente = «Para mí»), `definitionId`, `page`, `pageSize` |
| `/whatsapp/chat` | `conversacion={id}` (la conversación abierta) |

**Nunca en la URL ni en almacenamiento del navegador:** la búsqueda en textos largos de la planilla, los filtros por
pregunta, las personas elegidas y el rango de fechas (viven en memoria por actor/idioma/permisos y formulario, y se
borran al salir de ese formulario); borradores de formularios, catálogos, reglas y PDF; identificadores de intento
(`requestId`/UUID) y cuerpos congelados; el ticket de publicación; la URL de la vista oficial de WhatsApp; la persona
de ejemplo de un catálogo Filters; el mensaje y la selección del diálogo de envío del chat; la búsqueda de chats; el
motivo de un rechazo. Los tokens de sesión viven en memoria (`InMemoryWebStorage`).

### 1.7 Permisos que usa el front (la autorización real la hace el servidor)

| Permiso | Qué habilita en pantalla |
| --- | --- |
| `formtemplates.read` | Ver «Diseñar formularios» y abrir el editor en solo lectura |
| `formtemplates.manage` | «Nuevo formulario», «Editar», «Guardar borrador», «Descartar» |
| `formtemplates.publish` | «Publicar en WhatsApp» (no requiere manage) |
| `formcatalogs.read` | Ver Catálogos; elegir el tipo «Elegir de un catálogo» y ver nombres de catálogos en el editor |
| `formcatalogs.manage` | Crear/editar catálogos, dar de baja/reactivar, probar Filters con una persona |
| `formrecords.read` | Sección «Respuestas», planilla, detalle, valores en la tarjeta del chat, cuerpo de los avisos automáticos en el chat |
| `formrecords.export` | Botón «Exportar a Excel» |
| `formrules.manage` | Sección «Cuando llega una respuesta» en el editor, «Diseño del PDF», Consultar/Reintentar/Resolver envío en «Qué pasó después» |
| `formapprovals.decide` | «Por aprobar» y Aprobar/Rechazar |
| `whatsapp.read` | Chat; enlace «Abrir conversación» en el detalle de una respuesta |
| `whatsapp.send` | Responder y «Enviar formulario»; ver la cuenta vinculada en el encabezado |
| `whatsapp.manage` | «Tomar atención manual» |
| `users.read` | Ver el correo de la cuenta vinculada en el encabezado del chat |

Las capacidades por recurso también vienen del servidor: `canSave`, `canPublish`, `canPreview` (detalle del formulario),
`canEdit` (filas de listados), `canDecide`/`cannotDecideReason` (tarea de aprobación), `canConsult`/`canRetry`/`canReview`
(acciones del historial), `canReadFormRecords` y `formEligibility` (chat).

---

## 2. Vocabulario de disposición (para leer el resto)

- **Cabecera de pantalla (`Page`)**: banda fija arriba. A la izquierda: flecha «volver» opcional (con texto accesible
  y tooltip), **título** y, a su lado, un **estado** (texto o insignia). A la derecha: **acciones** (botones). En celular
  (<640 px) el título ocupa todo el ancho, el estado baja a otra línea y las acciones bajan alineadas a la derecha.
  El contenido va debajo con margen.
- **Superficie con banda (`Surface`)**: caja con borde; si tiene título, arriba hay una banda con el **título en
  mayúsculas chicas a la izquierda** y **acciones a la derecha de la banda**. Puede tener relleno o no (las tablas van sin relleno).
- **Barra de filtros (`FilterBar`)**: caja separada, encima de la tabla, con buscador y filtros en una fila que se parte
  en varias en celular.
- **Tabla (`DataTable` + `Pagination`)**: encabezados ordenables, estados de carga (spinner), error (mensaje +
  «Reintentar»; si ya había datos se conservan y no se reemplaza la tabla), vacío y sin coincidencias. Debajo, el
  paginado con «X–Y de Z», tamaño 10/20/50/100 y flechas; con 0 resultados muestra 0–0 de 0 sin "página 1 de 0".
  En celular la tabla se desplaza a lo ancho dentro de su caja.
- **Acciones de fila (`RowActions`)**: botones de ícono al final de la fila, con nombre accesible que incluye el dato
  de la fila; sin permiso se ocultan (no se deshabilitan).
- **Diálogo central**: título, descripción, contenido con desplazamiento propio y pie con «Cancelar» + acción concreta.
  En celular ocupa el ancho menos 2 rem.
- **Panel lateral** (diálogo con `placement="right"`): ocupa el alto completo pegado a la derecha, ancho máximo ~32 rem;
  en celular ocupa todo el ancho. Solo lo usa «Más filtros» de la planilla.
- **Confirmación (`ConfirmDialog`)**: título con la pregunta, descripción con la consecuencia, «Cancelar» (o el texto
  dado) y el botón que nombra la acción; variante destructiva cuando corresponde.
- **Aviso (toast)**: único, **abajo a la derecha**, con botón de cierre. Duraciones: éxito/info 4 s, warning 8 s,
  error 10 s; si trae una acción («Revisar», «Consultar envío», «Reintentar»), queda hasta que se cierra o se usa.
  Con un diálogo abierto, el aviso se dibuja dentro de la capa del diálogo para no romper el foco.

---

## Etapa 1: el formulario y el chat

### E1.1 Diseñar formularios (listado) — tablero A

**Ruta:** `/formularios` · **Permiso:** `formtemplates.read` · `pages/FormsPage.tsx`

**Cabecera:** título «Diseñar formularios». Acción a la derecha: **«Nuevo formulario»** (enlace a `/formularios/nuevo`),
solo con `formtemplates.manage`.

**Barra de filtros** (una fila):
1. Buscador «Buscar por nombre» (ancho fijo en escritorio, todo el ancho en celular). Escribe `search` en la URL con
   espera y vuelve a la página 1.
2. Control segmentado «Estado en WhatsApp» con tres opciones y su conteo del servidor:
   **«Todos (N)» · «Publicados (N)» · «Borradores (N)»**. Mientras no llegan los conteos se ve «…». Los conteos
   dependen solo de la búsqueda.

**Tabla** (dentro de una superficie sin banda):

| Columna | Contenido | Ordenable |
| --- | --- | --- |
| Formulario | Nombre como **enlace** al editor. Usa el nombre en el idioma de la interfaz; si está vacío, el del otro idioma | Sí |
| Preguntas | Cantidad | Sí |
| En WhatsApp | Una sola etiqueta en palabras: «Borrador», «Publicando», «Publicado · versión N», «No se pudo publicar» o «Publicación sin confirmar». Si hay una versión activa y el estado no es Publicado, segunda línea chica: «La versión N publicada sigue activa.» | No |
| Último cambio | «{fecha} · {autor}» (fecha en la zona de la cuenta, sin hora; «—» sin autor) | Sí |
| Acciones | Botón de ícono lápiz «Editar» (nombre accesible «Editar {nombre}»), solo con manage y `canEdit` | — |

Debajo, el paginado.

**Estados:**
- Cargando: spinner en la tabla.
- Vacío sin filtros: «Todavía no hay formularios» / «Creá el primero para empezar.» + botón «Nuevo formulario» (si hay manage).
- Sin coincidencias: «Sin coincidencias» / «No encontramos formularios con estos filtros.» + «Limpiar filtros» (borra
  búsqueda y filtro, conserva el orden).
- Error de carga: en la tabla «No pudimos cargar los formularios.» + «Reintentar»; la causa va en **un** toast de error.
  Si ya había una lectura del mismo actor/permisos/idioma, se conserva en pantalla.
- 403 en la lectura o sin permiso: página «No tenés permiso».

**Celular:** el buscador ocupa el ancho; el segmentado se desplaza a lo ancho si no entra; la tabla se desplaza dentro de su caja.

### E1.2 Editor del formulario (alta y edición) — tableros B, C, D, E (e1-1)

**Rutas:** `/formularios/nuevo` y `/formularios/{formId}` · **Permiso:** `formtemplates.read` (escribir requiere
`formtemplates.manage`) · `pages/FormEditorPage.tsx`, `components/QuestionEditor.tsx`, `components/FormPreview.tsx`,
`components/FileQuestionSettings.tsx`, `components/CatalogQuestionSelector.tsx`, `components/DraftReview.tsx`.

Es **una sola pantalla, sin pestañas**. En el alta **no existe nada** hasta el primer «Guardar borrador»; al guardar,
la dirección pasa a `/formularios/{id}` reemplazando la entrada del historial.

#### Cabecera

- Flecha volver «Diseñar formularios» (a `/formularios`).
- Título: nombre en el idioma original del formulario, o «Nuevo formulario».
- **Estado** (insignia junto al título), una sola etiqueta:
  - Si hay publicación: «Aceptando publicación…», «Publicación sin confirmar», «Publicando · versión N»,
    «Publicado · versión N», «No se pudo publicar · versión N».
  - Si no: «Guardado sin confirmar», «Cambios sin guardar» o «Borrador».
- **Acciones a la derecha, en este orden:**
  1. **«Descartar»** (secundario). Si hay cambios abre la confirmación; si no, vuelve al listado. Se ve con manage o
     si hay cambios; deshabilitado mientras hay un guardado/publicación en curso.
  2. **«Guardar borrador»** / «Guardando…» (solo con `formtemplates.manage`). Es principal salvo que la persona pueda
     publicar y el formulario ya exista, en cuyo caso pasa a secundario. Deshabilitado sin capacidad de guardar o con
     cantidades de archivos inválidas.
  3. **«Publicar en WhatsApp»** (principal) — con `formtemplates.publish`, capacidad `canPublish` y el formulario
     guardado al menos una vez. Deshabilitado durante un guardado o una publicación en curso.
  4. **«Ver vista oficial»** (secundario) — solo con `canPreview` y una versión activa publicada.

#### Cuerpo, de arriba hacia abajo

1. Línea de espera (si hubo un 429): «Esperá N segundos antes de consultar.»
2. **Superficie de publicación** (P2, ver E1.3), solo si hay o hubo una publicación.
3. **Superficie de guardado sin confirmar** (solo si el guardado quedó incierto): texto
   «No pudimos confirmar el guardado. Consultá antes de volver a guardar.» (variantes: «Ya no tenés permiso para
   guardar. Podés consultar tu intento pendiente.», «El identificador corresponde a otro intento. Consultá el
   guardado; tu pedido sigue bloqueado.», «La recuperación venció. El intento queda bloqueado; no vuelvas a crear el
   formulario por similitud.») + botones **«Consultar guardado»** («Consultando guardado…») y, si la consulta dio
   404, **«Repetir el mismo intento»**.
4. **Superficie «revisión pendiente»** (cuando otra persona guardó antes): «Elegí qué conservar en cada diferencia
   antes de volver a guardar.» + botón «Revisar».
5. **Superficie «Cambios de la otra revisión para revisar»** (después de elegir «Conservar mis cambios»): texto
   «Ninguna opción guarda. Después de elegir, revisás los cambios marcados y tocás «Guardar borrador».» + tabla de diferencias.
6. **Grilla de dos columnas** (en pantallas grandes: contenido flexible a la izquierda, 320 px a la derecha; en
   celular, una columna con la vista previa **debajo** de las preguntas):

   **Columna izquierda**
   - **Superficie sin banda «Textos en»**: rótulo «Textos en» + control segmentado con dos opciones. Cada opción dice
     el idioma **en su propio idioma** («Español», «English») y agrega «· Original» si es el idioma original y
     «· N pendientes» si faltan textos. Debajo: «Los tipos, el orden, las opciones y la obligatoriedad son compartidos
     por los dos idiomas.»
   - **Superficie «El formulario»** (campos en dos columnas en escritorio):
     - **Nombre** (máx. 120). En el idioma no original: ayuda «Original (Español): {texto}» y marcador
       «Falta en inglés» si está vacío.
     - **Texto del botón final**. Ayuda: «Es lo que toca el empleado al terminar.» (o el original en el otro idioma).
       Arranca en «Enviar»/«Submit» según el idioma original.
     - **Prefijo de numeración**, con ícono de candado si está fijo o si se está en el idioma no original. Se sugiere
       solo, mientras no se toque, con las primeras tres letras sin tildes de la **última palabra** del nombre
       (p. ej. «Registro de visita» → «VIS»). Ayudas según el caso:
       - normal: «Las respuestas se numeran {PREFIJO}-000001. Podés cambiar el prefijo hasta iniciar la primera publicación. El ejemplo no reserva nada.»
       - en el otro idioma: «El prefijo es compartido. Editalo desde el idioma original.»
       - fijo: «El prefijo quedó fijo cuando se aceptó la primera publicación, aunque después no se haya completado.»
       - publicación en confirmación: «El prefijo está bloqueado mientras confirmamos si la publicación fue aceptada.»
       - error junto al campo: «Ya hay un formulario con ese prefijo. Elegí otro.»
     - Pie de la superficie: «Los textos se editan por idioma y no se traducen automáticamente.»
   - **Superficie «Preguntas»**. En la banda, a la derecha: «Archivos: hasta N de 10» (si hay preguntas de archivo),
     «N preguntas» y el botón **«Agregar pregunta»** (deshabilitado al llegar a 40). Contenido:
     - Vacío: «Todavía no agregaste preguntas.»
     - Una **fila por pregunta** (ver abajo).
     - Con 40: «Podés agregar hasta 40 preguntas. Quitá una para agregar otra.»
     - Error general de la lista de preguntas (rojo, debajo).

   **Columna derecha: vista previa** (`FormPreview`)
   - Encabezado chico: «Vista previa» (o «Vista previa · pantalla X de Y» si hay preguntas de archivo).
   - «Contenido en {idioma}».
   - Un **marco de celular** con: título = nombre del formulario; cada pregunta con su control (texto, área, número
     decimal, fecha, Sí/No en radios, lista desplegable); las de archivo muestran un botón deshabilitado «Adjuntar
     documento», «Sacar foto» (solo cámara) o «Agregar fotos», y «Mínimo X · hasta Y». Abajo: «Pantalla anterior»
     (si no es la primera), «Continuar» o el **texto del botón final** en la última pantalla, y
     «Esta vista no envía ni guarda respuestas.»
   - Los textos que faltan en el idioma mostrado usan el original con la marca «· Traducción pendiente».
   - Ayudas por tipo dentro de la vista: decimal «El número se conserva exactamente como lo escribiste. El servidor lo
     valida al recibir una respuesta.»; fecha «Fecha sin hora.»; pregunta de catálogo: desplegable vacío deshabilitado
     y «Las opciones se preparan según quien lo recibe, al mandar el formulario.»; opciones sin traducir: «N opciones
     muestran el original: falta la traducción.»
   - Debajo del marco: «Cada pregunta de foto o documento abre su propia pantalla. Las preguntas siguientes pueden
     compartirla.» (si hay varias pantallas), «Los textos pendientes muestran el original marcado; esa referencia no
     se guarda como traducción.» y «Vista previa orientativa. La vista oficial de WhatsApp estará disponible al publicar.»
   - Reglas de pantallas: cada pregunta de foto/documento empieza una pantalla nueva; las siguientes se quedan en esa pantalla.

7. **Sección «Cuando llega una respuesta»** (Etapa 4, G1; ver E4.1) a todo el ancho debajo de la grilla, solo con
   `formrules.manage` y el formulario ya guardado.

#### Fila de una pregunta (`QuestionEditor`)

Plegada, en una línea:
- Número de orden.
- Etiqueta (en cursiva si falta en el idioma mostrado; si está vacía, «Pregunta»).
- Línea chica: **tipo en palabras** · (archivos: «Mínimo X · hasta Y» y, en foto, «Cámara o galería»/«Solo cámara»)
  · (catálogo: su nombre) · «Obligatoria»/«Opcional» · «N pendientes» si faltan textos.
- Insignia de advertencia «Revisá esta pregunta · {idioma}» si la comprobación de publicación la marcó.
- A la derecha, botones de ícono: **Subir**, **Bajar** (deshabilitados en los extremos, con el motivo
  «{nombre}: posición X de Y»), **Editar** (lápiz; en solo lectura es una flecha «Ver pregunta {nombre}») y **Quitar** (papelera).

Desplegada (se edita **en el lugar**, debajo de la fila, con fondo levemente distinto), grilla de dos columnas:
- **Pregunta** (texto en el idioma elegido; ayuda con el original si se está en el otro idioma).
- **Tipo de respuesta** (desplegable, en este orden): Texto corto, Texto largo, Número con decimales, Fecha, Sí o no,
  Elegir de una lista, Elegir de un catálogo, Foto, Documento. «Elegir de un catálogo» deshabilitado sin
  `formcatalogs.read`; Foto y Documento deshabilitados con el sufijo «· Falta configurar dónde se guardan los
  archivos» si la instalación no tiene almacenamiento (`form-files`). Ayuda en fecha: «Fecha sin hora.»
- **Ayuda para el empleado** (área de texto, ayuda «Opcional»).
- **Obligatoria** (casilla; no aparece en Foto/Documento, donde manda el mínimo) y **Máximo de caracteres** (solo
  texto corto/largo; 80 y 600 por defecto, de 1 a 10.000).
- Debajo, según el tipo:
  - Elegir de una lista: una fila por opción con el campo «Opción N» y botones Subir/Bajar/Quitar; botón
    «Agregar opción» (máximo 50). Una opción nueva arranca vacía.
  - Elegir de un catálogo: selector de catálogo (ver Etapa 2, K6).
  - Foto/Documento: ajustes de archivo (ver Etapa 2B, F1).
- Pie: botón **«Listo»** a la derecha (pliega y devuelve el foco a «Editar»).

**Confirmaciones que abre una pregunta:**
- Quitar una pregunta ya guardada o con texto → destructiva «¿Quitar la pregunta?» / «Dejará de pedirse en el
  borrador. Las respuestas anteriores se conservan.» / «Quitar pregunta». Sin texto ni identidad, se quita directo.
  El foco va al «Editar» de la vecina o a «Agregar pregunta».
- Cambiar el tipo de una pregunta ya guardada → «¿Cambiar el tipo de respuesta?» / «Esta será una pregunta nueva.
  Las respuestas anteriores conservan el tipo original.» / «Cambiar tipo».
- Quitar una opción guardada o con texto → «¿Quitar la opción?» / «Dejará de ofrecerse en el borrador; las
  respuestas anteriores se conservan.» / «Quitar opción».
- Cambiar el catálogo de una pregunta guardada → «¿Cambiar el catálogo?» / «La pregunta guardada se reemplaza por una
  nueva. Las versiones anteriores conservan su catálogo.» / «Cambiar catálogo».

Mover una pregunta u opción **anuncia** la nueva posición a lectores de pantalla y deja el foco en el botón de mover del mismo elemento.

#### Guardar, validar y avisos

- Éxito: toast «Guardamos el borrador de {nombre}.»
- Validación con campos visibles: el error queda **junto al campo**; el editor cambia al idioma del primer error,
  despliega esa pregunta y lleva el foco al primer campo inválido. **No** hay toast.
- Validación de un dato que el editor no puede mostrar: toast «No pudimos guardar un dato que este editor no puede corregir.»
- Fallo general: toast «No pudimos guardar el borrador.» (con «Código para reportar: …» si el servidor dio un traceId).
  El borrador queda intacto.
- Resultado incierto (red/5xx): toast con «No pudimos confirmar el resultado. Revisá el estado actual antes de repetir
  la operación. Referencia: …» y aparece la superficie de guardado sin confirmar. No hay reintento automático.
- 429: toast warning «Esperá N segundos antes de consultar.» y la línea de espera arriba; los botones de consulta esperan.
- **409 (otra persona guardó antes, tablero D):** toast de error «El formulario cambió mientras lo editabas. Revisá la
  versión guardada.» con acción **«Revisar»**, que relee y abre el diálogo de revisión.
- Ninguna acción guarda sola tras resolver; el siguiente guardado es otra acción del usuario.

#### Diálogo «Revisión del borrador» (D) — `DraftReview`

Diálogo central ancho:
- Título «Revisión del borrador»; descripción «Compará lo guardado actualmente con tus cambios. Ninguna opción guarda
  ni combina cambios automáticamente.»
- Línea «Último cambio: {fecha} · {autor}».
- Tabla con tres columnas: **«Qué cambió»** (Nombre · Español, una pregunta, «Pregunta · Opción: X», «Orden de
  preguntas», «Orden de opciones de X», «Pregunta · Ayuda para el empleado»…), **«Guardado actualmente»** y
  **«Mis cambios»**. Celdas vacías se leen «Quitada» o «Traducción pendiente». Si algo no se puede conservar, debajo
  de «Mis cambios»: «No se puede conservar esta identidad: la pregunta o una opción ya fue retirada. Lo escrito sigue
  en esta revisión.» (o el texto del prefijo fijo).
- Pie: «Ninguna opción guarda. Después de elegir, revisás los cambios marcados y tocás «Guardar borrador».» y los
  botones **«Usar borrador actual»** (secundario) y **«Conservar mis cambios»** (principal).

#### Salir con cambios

- Volver, migas, menú o Atrás con cambios → destructiva «¿Descartar los cambios?» / «Vas a perder lo que cambiaste sin
  guardar.» / «Seguir editando» / «Descartar». Recargar la pestaña lo pregunta el navegador.
- Mientras guarda: diálogo «Guardando…» / «Estamos guardando el borrador. Esperá el resultado antes de salir.» con solo «Seguir editando».
- Guardado sin confirmar: «Guardado sin confirmar» / «Consultá el guardado antes de salir. Lo escrito y el intento
  siguen en memoria.» / «Consultar guardado».
- Revisión pendiente: «Revisión del borrador» / «Elegí qué conservar…» / «Revisar».
- Publicación en confirmación: «Publicación sin confirmar» / «Todavía no sabemos si la publicación fue aceptada.
  Consultá el intento antes de volver a enviarlo.» / «Consultar publicación».

#### Estados de carga del editor

- Cargando: cabecera con el título y un spinner.
- 404: cabecera con volver + estado vacío «No encontramos este formulario.» + «Reintentar» (sin toast).
- Error de lectura: «No pudimos cargar el formulario.» + «Reintentar», y la causa en un toast.
- Sin permiso o 403: «No tenés permiso».
- **Solo lectura** (sin manage o perdido): campos de solo lectura, sin Subir/Bajar/Quitar/Agregar; «Ver pregunta» en lugar de «Editar».
- Un refresco de la lectura **nunca** pisa lo escrito: el editor se monta una vez con la lectura y no incorpora refetch.

### E1.3 Publicar en WhatsApp — P1, P2, P3

#### P1: diálogo de comprobación y aceptación — `components/PublicationCheckDialog.tsx`

Se abre con «Publicar en WhatsApp». Diálogo central ancho. **Se publica solo lo guardado.**

- **Título:** «Primero guardá los cambios» (si hay cambios sin guardar), «Publicar en WhatsApp» (mientras comprueba) o
  «Publicar la versión N en WhatsApp» (N lo da el servidor).
- **Descripción:** «Se publica sólo lo guardado. Guardá el borrador antes de comprobarlo.» o «Revisamos el borrador
  guardado antes de publicar. Esta comprobación no publica ni guarda cambios.»
- **Cuerpo**, según el caso:
  - Con cambios sin guardar: «Tus cambios siguen en el editor.» (no se comprueba nada).
  - Comprobando: spinner + «Comprobando el formulario…»
  - Error: estado vacío «Comprobación pendiente» + «Reintentar», o «Ya no tenés permiso para publicar.» (403, sin reintentar).
    La causa va en toast «No pudimos comprobar el formulario.» Un 409 cierra el diálogo y lleva a la revisión D.
  - Resultado, en este orden:
    1. Superficie **«Qué incluye»** (primera publicación) o **«Qué cambia respecto de la versión N»** (o «…de la
       versión publicada» si la base no coincide): «Agregadas: …», «Cambiadas: …», «N preguntas quitadas»; luego
       «N preguntas», la lista de nombres y «En español y en inglés. Cada empleado lo recibe en el idioma de su cuenta.»
    2. Avisos (banda amarilla) por idioma: «Faltan N textos en inglés.»
    3. Si hay impedimentos: una lista de tarjetas, cada una con el nombre («{pregunta} · Inglés», «Nombre»,
       «Texto del botón final», «El canal de publicación», «El formulario»), el motivo traducido del servidor y un
       botón **«Ir a la pregunta»** / **«Ir al texto»** que cierra el diálogo, cambia al idioma, despliega la
       pregunta y enfoca el campo exacto (sin devolver el foco al botón que abrió el diálogo).
       Si no hay impedimentos: superficie **«Qué queda fijo»**: «Las preguntas de esta versión conservan lo publicado.
       Los cambios posteriores van en otra versión.», «El prefijo {X} queda fijo al aceptar la primera publicación,
       aunque después no se complete.» (si todavía no está fijo) y «Aceptar el pedido todavía no significa que esté publicado.»
    4. Al final: «La comprobación venció. Volvé a comprobar antes de publicar.» o «Ya hay una publicación en curso
       para este formulario.» / «La publicación todavía no está disponible. Podés seguir guardando el borrador.», o
       superficie **«Qué va a pasar»** con tres pasos numerados: «Se guardará una versión fija con estas preguntas.»,
       «Se enviará a WhatsApp para comprobarla y publicarla.», «Quedará lista para mandar cuando WhatsApp confirme la publicación.»
- **Pie:** a la izquierda «Se publica el borrador guardado el {fecha}.»; a la derecha «Cancelar» y:
  - con cambios sin guardar: «Guardar borrador» (guarda y reabre el diálogo);
  - si no: **«Publicar versión N»** (deshabilitado si no se puede aceptar o venció) y, si venció, «Volver a comprobar».
- Al aceptar: el diálogo se cierra, toast de éxito «Publicación aceptada · versión N» y aparece la superficie P2.

#### P2: superficie de publicación en el editor — `components/PublicationProgress.tsx`

Superficie con banda, arriba del formulario. **Título** (con N = versión):
«Aceptando publicación…», «Publicación sin confirmar», «La publicación no fue aceptada», «Publicando · versión N»,
«Publicado · versión N», «No se pudo publicar · versión N» o «Ya no tenés permiso para publicar.»

Contenido:
- Frase de estado: «Estamos confirmando la versión. Esperá el resultado antes de iniciar otra publicación.»,
  «La versión N fue aceptada. Podés seguir editando el borrador mientras se confirma su publicación.», «La versión N
  está publicada y lista para mandar.», «La versión no se activó. La versión publicada anterior sigue en uso, si la
  había.», «Revisá el motivo y volvé a comprobar el borrador antes de publicar.», «Todavía no sabemos si la
  publicación fue aceptada. Consultá el intento antes de volver a enviarlo.» o «Esta publicación necesita revisión.
  No inicies otra para intentar resolverla.»
- Pasos en palabras (si fue aceptada y no falló): «Aceptada» y debajo el paso actual: «Preparando el envío a
  WhatsApp», «Enviada a WhatsApp; esperando confirmación», «WhatsApp la publicó; confirmando que quede lista para
  mandar», «Lista para mandar» o «Pendiente de confirmación de WhatsApp».
- Diagnósticos del servidor (sin repetir el mismo código/campo/idioma), con el idioma debajo y «Ir a la pregunta»/«Ir al texto».
- «La versión N publicada sigue activa.» cuando hay una activa y la nueva no terminó.
- «No pudimos actualizar el estado. El último estado confirmado se conserva.» si falló una consulta.
- Botones «Consultar publicación» y, tras un 404 incierto, «Repetir el mismo intento».
- Línea de espera para 429.
- El estado se consulta solo mientras está en curso (cada 30 s, o cuando el servidor indique la próxima consulta,
  entre 1 s y 5 min). Al llegar a Publicado o Fallido se relee el detalle. **El borrador sigue editable** todo el tiempo.

#### P3: vista oficial de WhatsApp — `components/OfficialFormPreview.tsx`

Diálogo central (~600 px):
- Título «Vista oficial · versión N»; descripción «La vista de WhatsApp corresponde a la versión publicada. Elegí el
  idioma del contenido.»
- Control segmentado Español / Inglés (cambiar idioma limpia la vista y espera otra acción).
- Cuerpo: spinner «Cargando la vista de WhatsApp…», la vista embebida (iframe) o un texto: «Pedí la vista en el idioma
  elegido.», «La vista venció. Pedila de nuevo para continuar.», «La vista no está disponible.» o «Ya no tenés permiso
  para ver esta vista previa.»
- Con vista: «Si no ves la vista, abrila en otra pestaña.»
- Pie: «Cerrar», «Abrir en otra pestaña» (con vista vigente) y «Cargar vista» / «Volver a pedir la vista».
- La URL vence: al vencer se quita la vista y el enlace, sin renovar sola. Se pide al abrir con el idioma del editor.

### E1.4 Mandar un formulario desde el chat — C1 a C5

**Ruta:** `/whatsapp/chat` · **Permiso:** `whatsapp.read` (enviar: `whatsapp.send`) ·
`features/whatsapp/pages/WhatsAppChatPage.tsx`, `components/ChatThread.tsx`, `components/ChatComposer.tsx`,
`components/FormComposer.tsx`, `components/FormCatalogPreparation.tsx`, `hooks/useFormComposer.ts`.

#### Disposición general del chat

- **Cabecera:** título «Chat de WhatsApp»; estado «No se pudo actualizar. Se conserva el historial visible.» si falla
  la lista; acción «Actualizar».
- **Una superficie** con dos columnas (en ≥1280 px): **lista de chats** (280 px) a la izquierda y **conversación** a la derecha.
  - Lista: buscador «Buscar por nombre o número» (no va a la URL), dos botones «Todos (N)» / «Sin leer (N)», la
    lista (nombre o teléfono, cantidad sin leer, y una línea de vista previa: «Borrador sin enviar» si hay borrador
    o el texto que da el servidor; para una respuesta de formulario el servidor manda un texto fijo traducido, sin
    valores) y un paginado «Anterior · n / total · Siguiente».
  - Sin conversación elegida: «Elegí una conversación para ver sus mensajes y responder.»
  - Estados de la lista: «Cargando conversaciones…», «No se pudo cargar el chat.», «No hay conversaciones con esos
    filtros.», «Todavía no llegaron mensajes a este WhatsApp.»
- **Celular y anchos menores a 1280 px:** se ve la lista **o** la conversación; la conversación tiene un botón «Chats» para volver.
- Se actualiza cada 5 s con la pestaña visible.
- Salir con borradores: «¿Salir con respuestas sin enviar?» / «Los borradores se conservan al cambiar de conversación,
  pero se pierden al salir de esta pantalla.» / «Seguir en el chat» / «Salir y descartar». Los borradores (texto y
  diálogo de formulario) son por conversación y sobreviven al cambio de conversación.

#### C1: encabezado de la conversación

Fila superior de la conversación:
- Izquierda: (celular) «Chats»; nombre del contacto; teléfono (o «WhatsApp no informó un número al que podamos responder.»);
  y, **solo con `whatsapp.send`**, una línea de cuenta:
  - «Número sin cuenta vinculada», o
  - «Cuenta vinculada: {correo}» (el correo solo con `users.read`; si no, el nombre de la cuenta), con sufijos
    «· sin correo» (solo si se puede leer el correo y falta) y «· inactiva».
  - Sin `whatsapp.send` el servidor no manda la cuenta y el encabezado **no infiere** que falte el vínculo.
- Derecha: el modo en palabras («Atención manual» o «Automatización sin configurar») y, con `whatsapp.manage` en modo
  Bot, «Tomar atención manual» → confirmación «Tomar atención manual» / «Vas a habilitar las respuestas manuales en
  esta conversación. Los códigos de acceso solicitados desde la web siguen funcionando.» / «Confirmar».

#### El compositor (zona inferior)

De arriba hacia abajo:
1. Motivo común si no se puede responder (arriba del área de texto): «Tenés permiso para ver el chat. Para contestar
   necesitás el permiso de responder.», «Tomá la atención manual para contestar.», «Pasaron 24 horas desde el último
   mensaje del cliente. El envío de plantillas se agregará después.», «WhatsApp no informó un número al que podamos
   responder.» o «Hay un envío pendiente o sin confirmar en esta conversación.»
2. Área de texto «Escribí tu respuesta…» (Ctrl+Enter envía, Enter agrega línea; máx. 4096; error junto al campo
   «Ingresá un mensaje de hasta 4096 caracteres.»).
3. Fila de botones:
   - **A la izquierda:** botón chico contorneado **«Enviar formulario»** (con ícono de portapapeles) y, si hay un
     envío de formulario incierto y el diálogo está cerrado, «Consultar envío». **Debajo**, el motivo cuando el botón
     está deshabilitado por algo propio de formularios: «No pudimos consultar si puede recibir formularios.», «Para
     mandar un formulario, el número tiene que estar vinculado a una cuenta.», «La cuenta vinculada está inactiva: no
     puede completar formularios.», «No tiene formularios publicados que pueda completar.» o «Cargando formularios…»
     (mientras carga el idioma del destinatario).
   - **A la derecha:** «Enviar» («Guardando…»), o «Consultar envío» / «Repetir el mismo pedido» para un texto incierto.
   - Sin `whatsapp.send` no aparece ninguno de los dos botones.
4. «Enviar formulario» se habilita solo si: hay permiso de responder, modo manual, ventana de 24 h abierta, número
   disponible, sin envío pendiente, cuenta vinculada activa con al menos un formulario elegible, y el idioma del
   destinatario ya cargado. Mientras un envío de formulario está pendiente se bloquea también el texto.

#### C2 y C3: diálogo «Enviar un formulario a {nombre}»

Diálogo central (~600 px). Mientras se envía **no se puede cerrar** (sin X, sin Escape, sin clic afuera).

De arriba hacia abajo:
1. Título «Enviar un formulario a {nombre}». Descripción «Le llega por WhatsApp un mensaje con el botón «Abrir
   formulario». Lo que complete queda guardado como una respuesta del formulario.»
2. Buscador «Buscar por nombre» (vuelve a la página 1).
3. **Lista de formularios** como tarjetas de opción única (radios): título = nombre del formulario **en el idioma del
   destinatario**; descripción «Versión N · K preguntas». Estados: «Cargando formularios…», «La lista no está
   disponible.» + «Reintentar», «No hay formularios con ese nombre.» Error de selección junto al grupo:
   «Elegí el formulario que querés enviar.»
4. Si el elegido quedó fuera de la página visible: «Elegido: {nombre} · versión N».
5. Paginado de la lista.
6. «Le llega en {inglés|español}, el idioma de su cuenta.»
7. (K7) Preparación de catálogos, si el formulario elegido tiene preguntas de catálogo (ver Etapa 2).
8. Campo **«Mensaje que lo acompaña»**, prellenado con «Completá este formulario, por favor.» (en el idioma del
   destinatario; «Please complete this form.» en inglés). Ayuda: «Es lo que lee arriba del botón «Abrir formulario».
   El botón final del formulario es otro: «{texto del botón final}».» (o «El mensaje es obligatorio: WhatsApp no manda
   el botón solo.» si no se ven opciones). Errores junto al campo: «Escribí el mensaje que acompaña al formulario.»,
   «Puede tener hasta 1024 caracteres.» (no se recorta).
9. Contador a la derecha «N / 1024».
10. «Se puede mandar hasta {fecha y hora}, 24 horas después de su último mensaje.»
11. Motivo de bloqueo (C4), espera («Esperá N segundos antes de volver a consultar o enviar.») y
    «El formulario elegido ya no está disponible. Elegí otro.» si cambió la versión.
12. Pie: «Cancelar» y **«Enviar formulario»** («Guardando…»); si hay un intento congelado, «Consultar envío»; tras un
    404 confirmado, «Repetir el mismo pedido».

El botón de WhatsApp siempre dice «Abrir formulario» («Open form» en inglés) y es distinto del botón final del formulario.

#### C4: si algo cambia con el diálogo abierto

Se conserva lo escrito y se bloquea solo el envío, con un párrafo: «Ya no tenés permiso para esta acción.», «Cambió la
cuenta vinculada o su idioma. Cerrá el diálogo, actualizá el chat y revisá el destinatario antes de enviar.», el motivo
común (ventana cerrada, fuera de atención manual, otro envío pendiente) o «El formulario elegido ya no está disponible.
Elegí otro.» Un 403 retira la lista y su metadata.

#### C5: después de enviar

- Confirmado (202 = en cola): el diálogo se cierra, el borrador del formulario se limpia y aparece una burbuja saliente
  con la tarjeta del formulario (título = nombre en el idioma del destinatario; descripción «Formulario · versión N ·
  botón «Abrir formulario»») y el estado de entrega abajo: «En cola», «Enviando», «Aceptado por WhatsApp», «Entregado»,
  «Leído», «No se pudo enviar», «Envío sin confirmar», «Revisado por el operador».
- Incierto: toast warning «No se pudo confirmar si el pedido quedó guardado. Revisá su estado antes de repetirlo.» con
  acción «Consultar envío». Consultar con 404 **reabre el mismo diálogo** con el intento congelado, toast info «El
  pedido todavía no figura guardado. Podés repetir el mismo pedido; el sistema evita duplicarlo.» y el botón «Repetir
  el mismo pedido». Nunca se reenvía solo.
- Falló (burbuja): «El formulario no le llegó. Podés mandarlo de nuevo con «Enviar formulario».» (no se reutiliza como texto).
- Envío sin confirmar en la burbuja: «Revisá el WhatsApp del teléfono: el mensaje pudo haberse enviado. No se repetirá
  automáticamente.» + «Ya revisé el WhatsApp» (solo para el operador que lo envió) → confirmación «Confirmar revisión
  del envío» / «Esta acción habilita nuevas respuestas. No vuelve a enviar este mensaje ni lo marca como entregado.
  Confirmá solo después de revisar qué llegó al teléfono.» / «Confirmar que revisé».

### E1.5 La respuesta llega al chat — R1 a R3 (`components/FormReplyCard.tsx`, `FormReplyValues.tsx`)

Burbuja **entrante** sin texto propio, con una **tarjeta**: ícono de portapapeles, título = nombre del formulario
(o «Recibiendo formulario» mientras bajan archivos sin nombre todavía), subtítulo «Respuesta del formulario».
Nombre accesible del grupo: «Respuesta del formulario {nombre}, guardada como {número}».

Según el estado (R1):
- Guardando: «Llegó una respuesta de formulario. La estamos guardando…» (sin valores).
- Guardada (Completed):
  1. Lista «pregunta → valor» (dos columnas en escritorio, una en celular). Se ven **las primeras 3**; con más o con un
     texto largo de más de 240 caracteres o 3 líneas aparece «Ver las N respuestas» (o «Ver la respuesta completa»)
     / «Mostrar menos». Plegar/desplegar conserva la posición del historial.
  2. Franja de éxito: «Guardada como {PREFIJO-000024}».
  3. Sin permiso de lectura: «No tenés permiso para ver las respuestas de este formulario.»; si se revocó con el chat
     abierto: «Ya no tenés permiso para ver estas respuestas.» (y los valores desaparecen de pantalla y caché).
  4. Mensaje de WhatsApp borrado: «El mensaje de WhatsApp ya no está disponible. La respuesta sigue guardada.»
- No se pudo guardar: «No pudimos guardar la respuesta del formulario. Podés escribirle o mandárselo de nuevo.»
- No reconocida: «Llegó una respuesta de formulario que no pudimos relacionar con un envío. No se guardó.»
- Proyección inválida: «No se pudieron mostrar las respuestas.»
- (Etapas 2 y 2B agregan: opción dada de baja, bajando/reintentando archivos y archivos rechazados, con «Mandar de nuevo».)

Cómo se muestran los valores (R3): Sí/No, «Sin respuesta» para vacío, decimales con la escala exacta recibida
(formato del idioma, sin redondear ni completar ceros), fecha sin hora (sin corrimiento de zona), opciones con la
**etiqueta histórica** de su versión (más nombre del catálogo y detalle si los hay), texto largo plegado a 3 líneas.

Lista de chats: la vista previa de una respuesta de formulario es el texto fijo que manda el servidor (el diseño dice
«Respondió {nombre del formulario}»), nunca valores.

---

## Etapa 2: catálogos (K1–K7)

### E2.1 Catálogos (listado) — K1

**Ruta:** `/catalogos` · **Permiso:** `formcatalogs.read` · `pages/CatalogsPage.tsx`, `components/CatalogListTable.tsx`

**Cabecera:** «Catálogos»; acción **«Nuevo catálogo»** (con `formcatalogs.manage`) que abre el diálogo de origen.

**Barra de filtros:**
1. Buscador «Buscar catálogo».
2. Segmentado de origen con conteos del servidor (búsqueda + estado, ignorando el propio): «Todos (N)» ·
   «Escritas a mano (N)» · «De la base (N)».
3. Menú desplegable «Estado: {Todos|Activo|Dado de baja|No se puede usar}».

**Tabla:**

| Columna | Contenido |
| --- | --- |
| Catálogo (ordenable) | Nombre como enlace al editor + descripción chica debajo |
| De dónde salen | «Escritas a mano» o «De la base» (y debajo «Usuarios, con filtros») |
| Opciones | Cantidad o «—» |
| Lo usan | Enlace «N formularios» (nombre accesible «Ver formularios que usan {nombre}») o «—»; `null` = desconocido, nunca 0. **El enlace solo abre el editor del catálogo; el editor no lista los formularios que lo usan** (el tablero K3 lo dibuja, el código no) |
| Estado | Punto + «Activo» / círculo vacío + «Dado de baja» / ícono de alerta + «No se puede usar» con el motivo localizado «El catálogo tiene una configuración no válida.» |
| Acciones | Lápiz «Editar {nombre}» (oculto sin manage o sin `canEdit`) |

**Estados:** vacío «Todavía no hay catálogos.» / «Creá el primero para compartir sus opciones entre formularios.» +
«Nuevo catálogo»; sin coincidencias «No encontramos catálogos con estos filtros.» / «Limpiá los filtros para ver los
demás catálogos.» + «Limpiar filtros»; error «No pudimos cargar los catálogos.» + «Reintentar» (causa en toast); 403 → sin permiso.

### E2.2 Diálogo «Nuevo catálogo» (origen) — K2 (`components/CatalogOriginDialog.tsx`)

Diálogo central ancho:
- Título «Nuevo catálogo»; descripción «¿De dónde salen las opciones? No se puede cambiar después: para otro origen,
  se arma otro catálogo.»
- Tarjetas de opción única «De dónde salen las opciones»:
  - **«Escribirlas a mano»** — «Para listas que mantiene una persona: clientes, tipos de trabajo, depósitos. Se agregan,
    ordenan y dan de baja acá.» (elegida por defecto)
  - **«Tomarlas de la base, con filtros»** — «Elegís una vista de catálogo (por ahora, Usuarios) y qué filas entran.
    Se actualiza sola: un usuario nuevo aparece en el próximo envío.»
  - No hay tercera opción («con una consulta» está postergada).
- Pie: «Cancelar» / «Continuar» → `/catalogos/nuevo` o `/catalogos/nuevo?kind=Filters`.

### E2.3 Editor de catálogo Manual — K3 (`pages/CatalogEditorPage.tsx`, `components/CatalogManualItemsEditor.tsx`, `CatalogMetadataFields.tsx`, `CatalogStateAction.tsx`)

**Cabecera:** volver «Catálogos»; título = nombre (en el idioma de la interfaz) o «Nuevo catálogo»; estado en texto
«Activo» / «Dado de baja» y «· Cambios sin guardar». **Acciones** (solo con manage): «Dar de baja» o «Reactivar»
(solo si ya existe), «Descartar», «Guardar».

**Cuerpo:**
1. (Si hay conflicto) Superficie «Revisión del catálogo»: «El catálogo puede haber cambiado. Revisá la versión actual
   antes de guardar de nuevo.» + «Revisar».
2. Superficie sin banda «Textos en» con segmentado Español / Inglés y «Completá el nombre y la descripción en español e
   inglés. En las opciones, el inglés es opcional.»
3. Superficie **«El catálogo»**: «Nombre» (obligatorio, 120) y «De dónde salen las opciones» (solo lectura:
   «Escritas a mano»; ayuda con el texto de que no cambia) en dos columnas; «Descripción» (obligatoria, 1000, a todo
   el ancho; ayuda «La leen las personas para saber cuándo usarlo.»).
4. (Si hay movimientos pendientes) Superficie con «N movimientos pendientes. Se aplicarán en este orden al guardar.» y
   «Descartar movimientos».
5. Superficie **«Opciones»** con «+ Agregar opción» en la banda:
   - Buscador «Buscar opción» (solo si el catálogo ya existe; va a la URL).
   - Tabla: **#** (posición confirmada; «—» para nuevas) · **Texto y código** (texto y debajo el código chico) ·
     **Detalle** · **Estado** («Activa»/«Dado de baja») · **Acciones**: Subir, Bajar (las nuevas no se mueven hasta
     guardarse: «… Guardá la opción para poder moverla.»), lápiz «Editar {nombre}» o, si está dada de baja, botón
     «Reactivar».
   - Edición **en el lugar**: en ≥640 px se abre una fila debajo de la opción; en celular se abre **debajo de la
     tabla** con el título «Editando {nombre}». Campos: «Texto» (obligatorio en español, máx. 30; en inglés con la
     ayuda «Si no completás el texto en inglés, se usa el de español.»), «Código» (obligatorio, 80, ayuda «El código es
     para la empresa: no se muestra en WhatsApp.»), «Detalle» (opcional, 300), casilla «Activa» con la ayuda «Una
     opción dada de baja deja de ofrecerse en los envíos nuevos. Las respuestas que ya la eligieron siguen
     mostrándola.» y botón «Listo».
   - Vacío: «Todavía no hay opciones.» Paginado al pie. Error de opciones: «No pudimos cargar las opciones.» + «Reintentar».
   - Las opciones nuevas se agregan al final al guardar; las flechas encolan movimientos que se aplican con el mismo «Guardar».

**Diálogos:**
- «Dar de baja» → confirmación destructiva con el mismo título y «Al guardar, este catálogo no se podrá elegir en
  preguntas nuevas y tampoco se podrán mandar los formularios publicados que lo usan. Las respuestas anteriores se
  conservan.»; «Reactivar» → «Al guardar, este catálogo volverá a estar disponible para los formularios.» El cambio
  **se aplica con Guardar**, no al confirmar.
- Descartar / salir con cambios → «¿Descartar los cambios?» / «Vas a perder lo que cambiaste sin guardar.» (ver
  observación sobre el botón de cancelar en el apartado 5).
- «Revisión del catálogo» (409): diálogo con descripción «Tu borrador se conserva al releer. Usar la revisión actual
  descarta los cambios sin guardar, incluidas las opciones pendientes.», dos superficies lado a lado «Tu borrador» y
  «La revisión actual» (nombre y descripción en ambos idiomas, o «Releé para comparar con la revisión actual.») y el
  pie «Cerrar», «Releer», **«Usar la revisión actual»**.

**Avisos:** éxito «Catálogo guardado.»; errores de campo junto al campo (incluidos los de cada opción: se despliega la
opción con error y se cambia al idioma); red/5xx: toast con el texto de reintentar guardado y el borrador intacto
(se puede volver a guardar con la misma revisión); solo el 409 pide revisión.

### E2.4 Editor de catálogo Filters — K4 (`pages/CatalogFiltersEditorPage.tsx`, `components/CatalogFiltersEditor.tsx`)

**Cabecera:** igual que Manual. «Guardar» se deshabilita si el catálogo está activo y no se eligió una persona de ejemplo.

**Cuerpo:** (superficie de revisión si hay conflicto) y una grilla de dos columnas en pantallas muy anchas (1,6 : 1),
una columna en el resto:

- **Izquierda:**
  1. «Textos en» y «El catálogo» (como en Manual; origen «De la base»).
  2. Superficie **«De dónde salen: la base, con filtros»**:
     - Fila de tres: «Vista» (desplegable deshabilitado con «Usuarios»; ayuda «Columnas: Nombre, Rol, Activo.»),
       «Texto de cada opción» (solo Nombre), «Detalle» («Sin detalle» o una columna).
     - «Qué filas entran»: una fila por condición con «Si» / «Y» + columna, «Comparación», «Valor» (lista si la columna
       tiene opciones —p. ej. roles o Sí/No, con etiquetas que da el servidor— o texto) y papelera «Quitar condición N»;
       botón «+ Agregar condición». Valores guardados que ya no existen se muestran como «Columna no disponible»,
       «Comparación no disponible» o «Valor no disponible».
     - Fila final: casilla «Sin incluir a quien lo recibe» («La persona que recibe el formulario no aparece entre estas
       opciones.») y, al lado, «Orden» + «Dirección» («A → Z» / «Z → A»).
     - Si fallan las columnas: «No pudimos cargar las columnas y las personas de ejemplo.» + «Reintentar».
- **Derecha:** superficie **«Vista previa»**:
  - Campo con autocompletado **«Como si lo recibiera»** (busca personas con 300 ms de espera; ayuda «Elegí una persona
    de ejemplo. Esta prueba no cambia su identidad ni sus permisos.»), «Reintentar» si falla, paginado de personas.
  - Botón **«Probar»**.
  - Resultado: lista de opciones (texto, detalle y, si no entra, «No entra en WhatsApp: máximo 30 caracteres de texto y
    300 de detalle.»), «N opciones.», o «No hay opciones con estos filtros.»; sin probar: «Elegí una persona para
    probar antes de guardar.»
  - Más de 200: toast warning «Hay más de 200 opciones. Ajustá los filtros antes de guardar.» y no se guarda.
  - Sin manage: «Necesitás permiso para administrar catálogos para probar esta vista.» (no se ven personas).
- Cambiar condiciones o persona borra el resultado anterior. Guardar vuelve a probar en el servidor.

**Diálogo de revisión (409):** ancho, con «Tu borrador» y «La revisión actual» lado a lado, cada uno con nombre,
descripción y la configuración de filtros en solo lectura; pie «Releer» / «Usar la revisión actual».

### E2.5 Pregunta «Elegir de un catálogo» en el editor — K6 (`components/CatalogQuestionSelector.tsx`)

Dentro de la pregunta desplegada:
- Campo **«Catálogo»** (obligatorio) con un desplegable. Cada opción: «{nombre} · Escritas a mano|De la base · N
  opciones · {estado si no está activo}». Los no activos se ven pero **no se pueden elegir**. El seleccionado se
  conserva aunque no esté en la página visible. Ayuda: la descripción del catálogo, «El empleado ve el texto y el
  detalle de cada opción.» o, sin lectura, «Necesitás permiso para leer catálogos y elegir uno.»
- Buscador «Buscar catálogo» (vuelve a página 1), spinner, «Reintentar», paginado.
- Vacío: «Todavía no hay catálogos disponibles.» / búsqueda sin resultados: «No hay catálogos que coincidan con la búsqueda.»
- Enlace «Ver el catálogo» al editor del catálogo.
- En la fila plegada se agrega el nombre del catálogo. En la vista previa, desplegable vacío deshabilitado.

### E2.6 Al mandar y al responder — K7

En el diálogo de envío del chat, si el formulario elegido tiene preguntas de catálogo, entre «Le llega en…» y el
mensaje aparece la **preparación** (`FormCatalogPreparation`):
- «Preparando las opciones para esta persona…»
- Error: «No pudimos cargar las opciones del catálogo.» + «Reintentar» (causa en toast).
- Un 409: banda roja «Cambió la cuenta vinculada o su idioma…» o «El formulario elegido ya no está disponible. Elegí otro.»
- Por pregunta, bandas: «{pregunta}: N opciones» (informativa) o, en rojo, «{pregunta}: el catálogo no se puede usar.
  Avisale a quien lo administra.», «{pregunta}: supera las 200 opciones que admite WhatsApp. Ajustá el catálogo antes
  de mandar.», «{pregunta}: no tiene opciones y la pregunta es obligatoria.», «{pregunta}: N opciones de {catálogo} no
  entran en WhatsApp. Revisá sus textos y detalles.»
- Mientras carga o si hay un problema, **«Enviar formulario» queda deshabilitado**; el mensaje escrito se conserva.
  El operador ve solo cantidades y problemas, nunca las opciones.

En la tarjeta de respuesta: la opción elegida muestra su texto, el nombre del catálogo y el detalle **históricos**.
Si la opción se dio de baja antes de responder: «{pregunta}: una opción se dio de baja. No se guardó la respuesta.» +
botón **«Mandar de nuevo»** (abajo a la derecha de la tarjeta), que abre el diálogo de envío y exige confirmar otro envío.

---

## Etapa 2B: fotos y documentos (F1–F5)

### E2B.1 En el editor — F1 (`components/FileQuestionSettings.tsx`)

En una pregunta Foto o Documento desplegada (no tiene casilla «Obligatoria»):
- Foto: «De dónde sale la foto» con dos tarjetas: «Cámara o galería» («Puede sacarla en el momento o elegir una que
  ya tiene.») y «Solo cámara» («La saca en el momento: sirve como evidencia de la visita.»).
- Documento: «PDF, Excel, Word o imágenes JPEG y PNG. Sin CSV.»
- «Cuántas fotos» / «Cuántos documentos» con dos campos lado a lado «Mínimo» (0–10) y «Máximo» (1–10). Si son
  inválidos se marcan **ambos** con «Usá cantidades enteras: mínimo de 0 a 10, máximo de 1 a 10 y mínimo no mayor al
  máximo.» y no se puede guardar. Debajo: «Con mínimo 1, la pregunta es obligatoria. Con 0, es opcional.»
- La banda «Preguntas» muestra «Archivos: hasta N de 10» (suma de máximos; no impide guardar; el límite lo controla la publicación).
- Vista previa: una pantalla por pregunta de archivo («Vista previa · pantalla X de Y»).

### E2B.2 En la tarjeta del chat — F3

Estados adicionales de la tarjeta de respuesta:
- «Recibimos el formulario. Estamos bajando los archivos de WhatsApp y guardando la respuesta.» (o «Recibimos N
  archivos. Los estamos bajando…»).
- «Estamos intentando bajar los archivos de nuevo. No hace falta hacer nada.»
- Rechazos: «No pudimos guardar el archivo de «{pregunta}». No se guardó la respuesta.», «No pudimos bajar los
  archivos de WhatsApp. No se guardó la respuesta.» o «Uno de los archivos no cumple el tipo, tamaño o contenido
  admitido. No se guardó la respuesta.», más «Podés mandar el formulario de nuevo.» y «Mandar de nuevo» si se puede.
- Guardada: en el valor de una foto, **hasta 4 miniaturas** y, si hay más, una quinta casilla «+N» («Ver N fotos más»);
  en un documento, una tarjeta por archivo con nombre, tamaño (KB/MB, mínimo 1 KB) y botón «Descargar».
- Las miniaturas se piden solo cuando están visibles; mientras cargan muestran un reloj y, si fallan, una alerta.
  Revocar la lectura cierra el visor y quita nombres y miniaturas.

### E2B.3 Visor de fotos — F4 (`shared/ui/PhotoViewer.tsx`, `shared/ui/FormRecordFiles.tsx`)

Diálogo central grande (compartido entre chat, planilla y detalle):
- Cabecera: título = nombre de la pregunta (o «Fotos de la respuesta» en la planilla/detalle); debajo «Foto X de Y · {número de respuesta}».
- Centro: la foto en grande (original, pedido al abrir), flechas «Foto anterior» / «Foto siguiente» a los costados.
  Mientras carga «Cargando…»; si falla «No se pudo cargar el archivo.»
- Tira de miniaturas para saltar.
- Pie: nombre del archivo, tamaño, fecha de recepción (en la zona de la cuenta) y «Descargar».
- Teclado: ← → cambian de foto, Escape cierra. Al cerrar, el foco vuelve a la miniatura (o a la tarjeta si la miniatura desapareció).
- Los documentos **siempre se descargan**, nunca se abren en el visor.

---

## Etapa 3: planilla de respuestas (T1–T5)

### E3.1 Respuestas: todos los formularios (`pages/ResponseFormsPage.tsx`)

**Ruta:** `/formularios/respuestas` · **Permiso:** `formrecords.read`. Se llega desde «Ver todos (N)».
- Cabecera: «Respuestas».
- Barra de filtros: buscador «Buscar formularios» (máx. 100).
- Tabla: «Formulario» (enlace a su planilla) · «Versión del formulario».
- Vacío: «Todavía no hay formularios publicados» / «Los formularios publicados van a aparecer acá.»; con búsqueda:
  «Sin coincidencias». Error: «No pudimos cargar los formularios.» + «Reintentar».

### E3.2 La planilla — T1, T2 (`pages/FormResponsesPage.tsx`)

**Ruta:** `/formularios/{formId}/respuestas` · **Permiso:** `formrecords.read` (exportar: `formrecords.export`).

**Cabecera:** título = nombre del formulario; estado «Respuestas · versión publicada N»; acción **«Exportar a Excel»**
(secundaria, solo con export; deshabilitada mientras se carga o refresca la planilla).

**Barra de filtros, primera fila (en orden):**
1. Buscador **«Buscar en textos largos»** (máx. 100) — solo si el formulario tiene alguna pregunta de texto largo.
   **No va a la URL.**
2. Menú desplegable de la **primera pregunta de tipo lista** (rótulo = nombre de la pregunta, con «(n)» elegidas):
   casillas con cada opción y su conteo del servidor (deshabilitadas con 0), «Ver más opciones» (abre el panel
   lateral desplazado a esa pregunta) y «Todas» (quita el filtro).
3. Botón «Recibida» (abre el panel de filtros).
4. Botón «Más filtros» (abre el panel de filtros).
5. Conteo «N respuestas» («…» mientras carga).
6. Menú **«Columnas»**: «Siempre visibles» con N°, Recibida y Cargó marcadas y deshabilitadas; separador; una casilla
   por pregunta (con «(desde vN)» si apareció en una versión posterior). Va a la URL (`columns`).
7. Enlace «Limpiar todo» (si hay filtros).

**Segunda fila (si hay filtros):** chips de los filtros aplicados, cada uno con su «Quitar filtro: …». Textos de chip:
«{pregunta}: {valores}», «{pregunta}: Desde X · Hasta Y», «Personas · N», «Versión del formulario · 1, 2»,
«Recibida · {desde} – {hasta}», «Buscar en textos largos: {texto}».

**Tabla:**

| Columna | Contenido |
| --- | --- |
| N° (ordenable) | Número como enlace al detalle (con los mismos parámetros de la URL) |
| Recibida (ordenable) | Fecha y hora en la zona de la cuenta |
| Cargó (ordenable) | Ícono de WhatsApp + nombre (texto accesible «WhatsApp» o «Canal sin información»; «Persona sin información» sin nombre) |
| Estado | **Solo si el formulario pide aprobación**: «Recibida», «Pendiente», «Aprobada», «Rechazada» |
| Una por pregunta (ordenables) | Encabezado = etiqueta (+ «(desde vN)»). Orden actual y **retiradas al final**. Valor: «—» si la pregunta no existía en esa versión, «Sin respuesta», «Sí/No», decimal exacto, fecha sin hora, opción con su detalle; texto truncado. Foto: **primera miniatura + «N fotos»** (abre el visor con todas las fotos de la fila). Documento: enlace «N documentos · PDF, XLSX» al detalle |

Con muchas preguntas, la tabla se desplaza a lo ancho. Debajo, el paginado.

**Estados:**
- Cargando: spinner.
- Vacío: «Todavía no hay respuestas» / «Mandá este formulario desde el chat de WhatsApp; las respuestas confirmadas aparecen acá.»
- Sin coincidencias: «Ninguna respuesta coincide» / «Cambiá o limpiá los filtros para ver las respuestas.» + «Limpiar todo».
- Error: «No pudimos cargar las respuestas.» + «Reintentar» (los datos anteriores se conservan; la causa en toast, con
  «Reintentar» en el toast si fue de red).
- Formulario inexistente (404): «No encontramos ese formulario o no está disponible», sin reintentar.
- 403: se retiran filas, valores, conteos y visor y se muestra «No tenés permiso».
- Error al cargar un archivo: un toast.

### E3.3 Panel lateral «Más filtros» — T2 (`components/ResponseFiltersPanel.tsx`, `ResponseFacetControl.tsx`)

**Panel a la derecha** (a pantalla completa en celular). Trabaja sobre un borrador; nada se aplica hasta el botón final.
- Título «Más filtros»; descripción «Las opciones se cuentan con los otros filtros aplicados.»
- Contenido desplazable, de arriba hacia abajo:
  1. «Recibida desde» / «Recibida hasta» (fechas, dos columnas; error junto al campo «La fecha final debe ser igual o
     posterior a la inicial.»). Las fechas se interpretan como días de la zona de la cuenta.
  2. **Una sección por pregunta** (título = etiqueta):
     - «Respuesta»: desplegable «Todas / Con respuesta / Sin respuesta / La pregunta no existía», cada una con su
       conteo entre paréntesis (deshabilitada con 0).
     - Según el tipo: lista o Sí/No → casillas con conteo («Buscar opciones» y paginado en listas); decimal/fecha →
       «Desde» / «Hasta» (decimal con error «Ingresá un número con coma o punto decimal.»); texto → «Contiene».
  3. **«Cargó»**: casillas de personas con conteo (buscador y paginado; sin nombre: «Persona sin información»).
  4. **«Versión del formulario»**: casillas «Versión N» con conteo.
  - «No hay opciones que coincidan.» cuando una faceta está vacía.
  - Los conteos se piden solo para las secciones visibles y se recalculan 300 ms después de cambiar el borrador.
- Debajo: línea de espera (429) y «Reintentar» si falló el conteo.
- Pie: **«Limpiar todo»** (terciario, limpia el borrador), «Cancelar» y el botón principal **«Ver N respuestas»**
  (o «Aplicar filtros» mientras no hay conteo), deshabilitado con datos inválidos o conteo pendiente/fallido.

### E3.4 Detalle de una respuesta — T3 (implementación actual) (`pages/FormResponseDetailPage.tsx`)

**Ruta:** `/formularios/{formId}/respuestas/{recordId}` · **Permiso:** `formrecords.read`. **Página completa**, no panel
(el panel D2 no existe en el código).

**Cabecera:**
- Volver: a la planilla con los mismos filtros/orden, o a «Por aprobar» si se llegó desde ahí (el rótulo de la flecha
  dice igual «Volver a la planilla»; ver apartado 5).
- Título: número de la respuesta (p. ej. «VIS-000024»).
- Estado: «{nombre del formulario} · Versión N».
- Acciones a la derecha: «X de Y» (posición dentro de la planilla filtrada), **«Anterior»**, **«Siguiente»**. Al
  llegar a un borde de página, pide la página vecina y salta a su primera/última respuesta; conserva filtros y orden.

**Cuerpo, de arriba hacia abajo:**
1. (Etapa 4) Superficie **«Por aprobar»** con el panel de decisión, solo si la tarea de aprobación está abierta.
2. Superficie **«Respuesta»**: lista «pregunta / valor» en dos columnas (una en celular), con **las preguntas de la
   versión de esa respuesta**. Fotos y documentos ocupan el ancho completo: galería con **todas** las miniaturas (abre
   el visor con todas las fotos de la respuesta) y documentos con nombre, tamaño y «Descargar». Opciones con detalle y
   nombre del catálogo. Vacío → «Sin respuesta».
3. Superficie **«De dónde vino»** (dos columnas): «Cargó» (persona), «Canal» («WhatsApp · {número enmascarado}»),
   «Recibida» (fecha y hora), «Envió el formulario» («{operador} · {fecha}»). Enlace **«Abrir conversación»** a
   `/whatsapp/chat?conversacion={id}`, solo con `whatsapp.read`.
4. (Etapa 4) Superficie **«PDF de la respuesta»** si hay copias emitidas.
5. (Etapa 4) Superficie **«Qué pasó después»** (historial de la respuesta y de sus acciones). Incluye siempre los
   eventos «Se mandó el formulario», «Se respondió el formulario», «Se guardó la respuesta» con fecha y actor.

**Estados:** cargando (cabecera «Detalle de la respuesta» + spinner); 404 → «No encontramos esa respuesta o no está
disponible» + «Volver a la planilla»; error → «No pudimos cargar las respuestas.» + «Reintentar»; respuesta de otro
formulario que la URL → «No encontramos esa respuesta o no está disponible»; 403 → sin permiso. Un único dueño de avisos
para valores, planilla, historial y navegación.

### E3.5 Exportar a Excel — T5 (`components/ResponseExportDialog.tsx`)

Diálogo central:
- Título «Exportar respuestas a Excel»; descripción «Se exportan las respuestas que coinciden con los filtros aplicados.»
- «N respuestas», «N filtros aplicados» y la lista de esos filtros (mismos textos que los chips).
- Radios **«Columnas a exportar»**: «Todas las preguntas (N columnas)» / «Solo las visibles (N columnas)».
- «Número, recibida, cargó y canal siempre se incluyen. De los archivos se exportan la cantidad y los nombres.»
- Nombre del archivo: «{PREFIJO}-{AAAAMMDD}.xlsx».
- Con más de 10.000: «Podés exportar hasta 10.000 respuestas. Acotá los filtros para continuar.» (botón deshabilitado).
- Espera 429: «Esperá N segundos para volver a intentar.»
- Pie: «Cancelar» / **«Exportar a Excel»** («Exportando…»).
- Éxito: descarga y toast «Descargamos la planilla de Excel.»; 403: cierra y toast «Ya no tenés permiso para exportar
  respuestas.»; otro error: toast. El archivo lo genera el servidor; cerrar cancela la descarga.

---

## Etapa 4: reglas, PDF, aprobación, avisos (G1–G6)

### E4.1 «Cuando llega una respuesta» (reglas) — G1 (`components/FormRulesSection.tsx`)

**Dónde:** dentro del editor del formulario, debajo de preguntas y vista previa. **Permiso:** `formrules.manage` y el
formulario guardado. Tiene **guardado propio** (no guarda el borrador ni publica). Grilla de dos columnas en pantallas
grandes (contenido / 320 px), una en celular:

**Izquierda: superficie «Cuando llega una respuesta».** En la banda, a la derecha: insignia «Cambios sin guardar» o
«Revisión N», y botón **«Guardar reglas»** («Guardando reglas…»), deshabilitado sin cambios, con errores locales o con conflicto.
1. Texto: «Se guardan aparte: cambiarlas no crea otra versión ni toca WhatsApp. Lo que ya pasó con cada respuesta no cambia.»
2. Bloque de error/conflicto (párrafo dentro de la superficie): conflicto → el detalle del servidor o «Las reglas
   cambiaron. Releé la revisión actual para revisarla.» + «Releer reglas» y, después de releer, «Usar la revisión actual»;
   otro error → texto de fallo operativo.
3. Línea de espera (429).
4. Vacío: «Todavía no hay reglas.»
5. **Una sección por regla**: número a la izquierda; en el medio, el **resumen en palabras** (evento —«Se recibe una
   respuesta», «Se aprueba una respuesta», «Se rechaza una respuesta»—; «Si: {preguntas}»; lista numerada de acciones
   «Pedir aprobación a una persona con un rol · {rol}», «Armar el PDF», «Mandarle un mensaje a quien la cargó: {texto}»,
   «Avisar a las personas con un rol · {rol}: {texto}»); a la derecha, lápiz «Editar regla N» y papelera «Quitar regla N».
   Desplegada (en el lugar):
   - «Cuándo» (desplegable de evento).
   - «Si (opcional)»: filas de condición con «Pregunta», «Condición» (según tipo: Tiene respuesta / Sin respuesta / No
     se preguntó / Es igual a / Es mayor o igual a / Es menor o igual a / Contiene / Es una de) y «Valor» (Sí/No,
     selección múltiple de opciones, fecha o texto), papelera «Quitar condición N»; texto «Sin condición, la regla corre
     siempre. Las condiciones usan las preguntas del formulario.»; botón «Agregar condición» (máx. 10).
   - «Hacer, en este orden»: una tarjeta por acción con el desplegable «Acción N» (en celular el rótulo visible dice
     «Acción N: {tipo}»), Subir/Bajar/Quitar acción; según el tipo: «Rol» («Elegí un rol»; uno borrado se muestra
     «Rol no disponible»), «Armar el PDF con el diseño vigente.» + enlace **«Ver el diseño»** a `/formularios/{id}/pdf`,
     «Mensaje» (ayuda «El idioma original ({idioma}) es obligatorio; el otro es opcional. Usá {número} y el nombre de
     una pregunta entre llaves para completar con la respuesta.»; máx. 4096, o 1024 si adjunta PDF) y casilla
     «Adjuntar el PDF» (solo en «Se aprueba»). Botón «Agregar acción» (máx. 10; deshabilitado si la última es pedir aprobación).
   - Botón «Listo».
6. Botón «Agregar regla» (máx. 20).

Errores junto a cada dato: «Completá este dato.», «Revisá los valores de las condiciones.», «Pedir aprobación tiene que
ser la última acción. Lo que sigue va en «Se aprueba» o «Se rechaza».», «La aprobación se pide cuando se recibe una
respuesta.», «El PDF se arma sólo cuando se aprueba una respuesta.», «Agregá Armar el PDF antes de adjuntarlo.»,
«Se alcanzó el límite.», «El mensaje supera el límite permitido.» Éxito: toast «Reglas guardadas.»

**Derecha: superficie «Historial de las reglas».** Lista de revisiones: enlace «Revisión N» (abre un diálogo con el
resumen de esa revisión, solo lectura), insignia «Vigente» en la actual, «{fecha} · {autor}». Paginado (10 por página)
y «Cada respuesta recuerda con qué revisión se procesó. Los cambios se aplican a las respuestas nuevas.» Carga/error con «Reintentar».

Estados: cargando (superficie con spinner), error de lectura (estado vacío + «Reintentar»), 403 → la sección se
reemplaza por «Ya no tenés permiso para administrar las reglas.» y se descartan sus consultas. Las reglas cuentan como
"cambios sin guardar" para la guarda de salida del editor.

### E4.2 Diseño del PDF — G2 (`pages/FormPdfDesignPage.tsx`, `components/FormPdfPreview.tsx`)

**Ruta:** `/formularios/{formId}/pdf` · **Permiso:** solo `formrules.manage` (no exige leer formularios ni respuestas).
Se llega desde «Ver el diseño» en una regla.

**Cabecera:** volver «Volver» (al editor si tiene `formtemplates.read`; si no, a Inicio); título = nombre del PDF en el
idioma original o «Diseño del PDF»; estado «{formulario} · diseño del PDF» (+ «· Cambios sin guardar»); acción «Descartar».

**Cuerpo:**
1. (Conflicto 409) Superficie «El diseño cambió» / «Conservamos tus cambios. Releé y elegí si querés usar la revisión
   actual.» + «Releer» y, después, «Usar la revisión actual».
2. Grilla 3 : 2 en pantallas muy anchas, una columna en el resto:
   - **Superficie «El comprobante»** con **«Guardar diseño»** en la banda (habilitado con cambios o si nunca se guardó):
     - «Nombre del PDF» (obligatorio en el original, 160) | «En {otro idioma}» (opcional: «Opcional. Si falta una
       traducción, el PDF sale entero en el idioma original.»).
     - «Qué preguntas incluye»: una casilla por pregunta; las retiradas que estaban elegidas aparecen como «Pregunta retirada».
     - Casilla «Incluir las fotos» («Las de las preguntas elegidas, chicas y debajo de su pregunta. Sin marcar, solo
       dice cuántas hay. Los documentos se nombran, no se adjuntan.»).
     - Casilla «Quién la aprobó y cuándo» («Se completa al aprobarla.»).
     - «Texto al pie» (ayuda «Opcional. Si lo agregás, completá el idioma original.») | «Texto al pie en {otro idioma}».
     - Errores junto al campo: «Completá el idioma original.», «El texto supera el máximo permitido.»
     - Pie: «Revisión N · {fecha}» o «Todavía no se guardó una revisión de este diseño.» y «Los PDF ya armados no
       cambian: son una copia fija del momento.»
   - **Superficie «Vista previa»** con un segmentado «Español / English» en la banda:
     - Botón «Vista previa» (pide un PDF de ejemplo con el diseño actual; no guarda ni emite).
     - Sin pedirla: «Con una respuesta de ejemplo. La vista previa no guarda ni emite un comprobante.»
     - Con PDF: «Contenido en {idioma}» o, si faltaba la traducción, «No está completo {pedido}. La vista previa usa
       todo el contenido en {real}.»; el PDF embebido; «El PDF real se arma con los datos y el idioma de cada respuesta.»
     - Editar o cambiar idioma **retira** la vista anterior. Error: mensaje + «Reintentar»; espera 429.
3. Salir con cambios: «¿Descartar los cambios?» con «Seguir editando» / «Descartar».

Éxito: toast «Diseño guardado.» 403 → sin permiso.

### E4.3 Por aprobar — G3 (`pages/ApprovalInboxPage.tsx`)

**Ruta:** `/formularios/por-aprobar` · **Permiso:** `formapprovals.decide` (y el rol vigente). En el panel con su cantidad.

- **Cabecera:** «Por aprobar» (sin acciones).
- **Barra de filtros:** segmentado «Alcance» con **«Para mí (N)»** (excluye las propias) y **«Todas abiertas (N)»**
  (las incluye, sin poder decidirlas); desplegable «Todos los formularios» / cada formulario. Van a la URL.
- Línea de error de opciones/conteos con «Reintentar».
- **Tabla**, ordenada de la más vieja a la más nueva (orden del servidor):

| Columna | Contenido |
| --- | --- |
| N° | Con `formrecords.read`: enlace al detalle (recordando el regreso a la bandeja). Sin ese permiso: botón-enlace que abre el diálogo de decisión |
| Formulario | Nombre |
| Cargó | Persona |
| Recibida | Fecha y hora |
| En espera | Tiempo relativo («hace 3 horas») |
| Rol | Rol que debe decidir; si es propia: «No podés aprobar ni rechazar tu propia respuesta.» |

- Vacío: «No hay respuestas por aprobar» / «Las respuestas que esperan aprobación aparecen acá, de la más vieja a la más nueva.»
- Error: «No pudimos cargar los formularios.» + «Reintentar» (reusa el texto del listado de formularios).
- Perder el permiso o el rol → sin permiso.

**Diálogo de decisión sin `formrecords.read`:** título = número; descripción «{formulario} · {autor}»; el panel de
decisión (abajo) y «Cerrar». **Nunca** muestra valores, archivos ni PDF.

### E4.4 Aprobar o rechazar — G4 (`components/ApprovalDecisionPanel.tsx`)

Se usa en la superficie «Por aprobar» del detalle y en el diálogo de la bandeja:
1. Insignia de estado («Pendiente de aprobación», «Aprobada», «Rechazada») + nombre del rol.
2. Si se decidió: «{nombre} · {fecha}» y el motivo.
3. Si está abierta: botones **«Aprobar»** (principal) y **«Rechazar»** (secundario), deshabilitados si no puede decidir.
4. «No podés aprobar ni rechazar tu propia respuesta.» cuando corresponde.
5. Párrafo de error (dentro del panel): «La aprobación cambió. Se actualizó con la decisión vigente.» (409, relee sin
   escribir de nuevo) u otro error; línea de espera 429.
- **Diálogo «Rechazar {número}»:** «El motivo queda en el historial de la respuesta.»; campo «Motivo» obligatorio
  (máx. 500, recortado al enviar) con contador «N / 500 caracteres»; pie «Cancelar» / **«Rechazar»** (destructivo).
  Mientras envía no se puede cerrar.
- Éxito: toast «Respuesta aprobada.» / «Respuesta rechazada.»; el foco pasa al resumen de la decisión y se refrescan
  bandeja, conteo, historial y planilla.
- Perder el permiso: «Ya no tenés permiso para decidir esta aprobación.»

### E4.5 Qué pasó después — G5 (`components/ResponseWorkflowHistory.tsx`) — **parcialmente en curso**

Superficie «Qué pasó después» en el detalle; en la banda, «Reglas de la revisión N».
- Lista cronológica:
  1. Eventos de la respuesta: «Se mandó el formulario», «Se respondió el formulario», «Se guardó la respuesta» (fecha · actor).
  2. La decisión, si la hubo: insignia del estado · rol; «{nombre} · {fecha}»; motivo.
  3. **Una fila por acción**: fecha a la izquierda; en el medio el tipo de acción y el evento («Se recibe una
     respuesta»…), el motivo si lo hay, y «No sabemos si salió. Consultá el estado y, si sigue sin confirmar, revisá el
     envío.» (sin confirmar) o «La condición de la regla no se cumplió.» (no aplica); a la derecha la insignia de estado:
     «Esperando», «En curso», «Hecho», «No se pudo», «Sin confirmar», «No aplica».
     - **(En curso, sin commit)** debajo, una subfila por **destinatario**: nombre + insignia «Sin envío registrado»,
       «Por enviar», «Enviando», «Enviado», «No se pudo enviar» o «Envío sin confirmar»; el motivo; si se revisó a mano:
       «{nombre} indicó que salió/no salió.», fecha y «Revisión humana: no confirma la entrega de WhatsApp.»; y el botón
       **«Resolver envío»** por destinatario (con `formrules.manage` y capacidad del servidor). En la versión
       comprometida (`f8212b2`) la revisión y el botón eran por acción.
     - Botón **«Consultar»** (para «Sin confirmar») o **«Reintentar»** (para «No se pudo» con `canRetry`), con
       «Consultando…». Nunca se ofrece repetir lo Hecho o No aplica.
- Carga: spinner. Error de lectura: mensaje + «Reintentar» (sin reintentar si es 404). Sin permiso de recuperar:
  «Ya no tenés permiso para recuperar acciones.» Espera 429.
- Pie: «Reintentar retoma sólo lo que no salió: no repite lo confirmado ni arma otro PDF.»
- Se vuelve a leer sola cada 2 s mientras haya acciones Esperando o En curso.

**Diálogo «Resolver el envío»:** descripción «Consultá el estado y comprobá el envío antes de elegir. Tu revisión
permite continuar la regla; no confirma que WhatsApp lo entregó.»; (en curso) «Destinatario: {nombre}»; radios
«Salió» («La regla continúa sin volver a mandar este aviso.») / «No salió» («El aviso queda para reintentar de forma
explícita.»; en avisos a un rol: «Se registra la falla de esta persona. No se reintenta el aviso al rol.»); si un envío
anterior quedó incierto: «Consultá el estado para saber si la revisión quedó guardada antes de volver a confirmarla.» y
el botón principal pasa a «Consultar»; pie «Cancelar» / «Confirmar revisión» → «Confirmar que salió» / «Confirmar que no
salió». Se congela la revisión de la acción al abrir. Mientras envía no se puede cerrar; al confirmar el foco vuelve a la fila.

### E4.6 PDF de la respuesta (copia fija) (`components/ResponseDocuments.tsx`)

Superficie «PDF de la respuesta» en el detalle, solo si hay copias. Por copia: nombre, fecha, insignia del idioma,
botones **«Ver PDF»** y **«Descargar»**, y «Los PDF ya armados no cambian: son una copia fija del momento.» «Ver PDF»
abre un diálogo «Copia fija» (descripción = nombre del archivo) con el PDF embebido y «Cerrar». Error y espera como
párrafos debajo. 403 retira el alcance privado.

### E4.7 Avisos automáticos en el chat — G6

Los mensajes que mandan las reglas aparecen en el historial como burbuja con autor **«Automático»**, el texto del
aviso y una tarjeta «Acción del formulario» con descripción «Documento» (si adjunta el PDF) o «Aviso automático».
No ofrecen «Usar este texto en el borrador» ni la revisión de envío incierto del chat (eso vive en «Qué pasó
después»). Sin `formrecords.read` o al revocarse, el cuerpo y el pie se retiran de pantalla y caché; la lista de chats
muestra el resumen genérico del servidor. El PDF solo se abre desde el detalle autorizado.

---

## Etapa 5 y lo que no existe

- **Asistente (H1–H7):** nada en el front. No hay modo Asistente, sugerencias, «Por qué lo mandó», audios ni
  «Formularios que faltan». «Cuándo lo ofrece el asistente» no aparece en el editor.
- **Sugerencias entre formularios (s1–s4):** propuesta en revisión; nada en el código.
- **D2 (detalle en panel lateral sobre la planilla, ↑ ↓, pestañas Respuesta/Fotos/Actividad/PDF, decisión fija abajo en
  celular):** elegido el 2026-10-17, **no implementado**; el detalle actual es la página E3.4.
- **Cargar respuestas desde la web, correcciones, envío fuera de 24 h con plantillas de Meta:** no implementados (el
  compositor dice «El envío de plantillas se agregará después.»).
- **Arrastrar para ordenar preguntas:** no existe; se ordena con Subir/Bajar.
- **«Formularios que lo usan» y «cómo lo ve el empleado» dentro del editor de catálogo (K3):** no existen en el código.
- **K5 (catálogo con una consulta):** postergado.

---

## 3. Reglas transversales del front que se aplicaron

### 3.1 Contrato de mensajes (`docs/guides/mensajes-y-estados.md`, `shared/ui/notifications.ts`, `shared/api/errorFeedback.ts`)

- **Un solo canal operativo:** un único Toaster, abajo a la derecha, montado en `src/app/providers.tsx`. Ninguna feature
  monta otro ni cambia posición/duración. Prueba de arquitectura: solo el emisor y el adaptador visual importan Sonner.
- **Dónde va cada cosa:** dato inválido → junto al campo (con `aria-invalid` y `aria-describedby`), sin toast; operación
  completada → toast de éxito **después** de la respuesta; fallo general → toast de error conservando el borrador;
  decisión previa → `ConfirmDialog` con consecuencia y verbo concreto; condición duradera (sin permiso, prefijo fijo,
  ventana cerrada) → texto junto al control o estado del flujo; carga inicial fallida → estado con «Reintentar» y la
  causa en **un** toast; refetch fallido con datos → conservar datos y avisar.
- **Un dueño por hecho:** las consultas comunes las avisa el manejador global de QueryCache; las que interpretan su
  error localmente declaran `meta: { errorOwner: "local" }` y su pantalla avisa una vez, con un id estable que incluye
  actor/cultura/recurso (p. ej. `forms:editor:{editorId}`), que se descarta al desmontar.
- **Reintentar no es universal:** una escritura incierta (red/5xx) se **consulta** antes de repetir; un 404 al consultar
  no prueba que no se guardó; solo una acción explícita repite **el mismo UUID y el mismo cuerpo**. Lecturas: hasta dos
  reintentos automáticos solo en errores de red.
- **429:** se respeta `Retry-After` (entre 1 s y 24 h), la espera queda visible junto al control («Esperá N segundos…»)
  y no se reintenta solo al vencer.
- **409:** el borrador se conserva; se relee y se adopta solo con una elección explícita («Usar borrador actual»,
  «Usar la revisión actual»). En aprobaciones, el 409 relee y muestra la decisión vigente sin escribir de nuevo.
- **401** sigue el flujo de sesión; **403** lleva a «No tenés permiso» o retira el bloque afectado, sin toast duplicado.
- Duraciones: éxito/info 4 s, warning 8 s, error 10 s, con acción hasta cerrar. Todos tienen botón «Cerrar aviso».

### 3.2 Datos con TanStack Query, claves por actor/cultura/permisos y limpieza privada

- Todo dato del servidor pasa por TanStack Query y el cliente común `shared/api/httpClient` (token, idioma en
  `Accept-Language`, `ApiError` desde ProblemDetails). Nada de `fetch` en efectos.
- Claves privadas: formularios `["business-forms", actorId, permisosOrdenados, cultura, …]`; respuestas
  `["form-responses", "{actor}:{idioma}:{permisos}:{read|denied}", …]`; aprobaciones `["form-approvals", owner, …]`;
  archivos `["form-files", scope, actor, idioma, recordId, fileId, miniatura]`; chat por actor/cultura/permisos.
- Al desmontar o cambiar de dueño se **cancelan y eliminan** esas consultas (`usePrivateQueryCleanup`, `useCleanup`,
  `PrivateFormFilesScopeProvider`, `useChatReadCleanup`). Las lecturas privadas usan en general `gcTime: 0` y `cache: "no-store"`.
- Se conserva la última lectura del **mismo** dueño mientras se recarga o falla (placeholder), nunca la de otro.
- Borradores, cuerpos congelados, UUID, tickets, URLs firmadas, blobs de PDF y object URLs viven en memoria del
  componente dueño, **fuera** de MutationCache, URL, storage y logs.

### 3.3 Tokens en memoria

`src/auth/authConfig.ts`: `userStore` con `InMemoryWebStorage` (tokens solo en memoria); `stateStore` en
`sessionStorage` solo para el ida y vuelta del ingreso. `localStorage` solo guarda claves `arquitecturabase.*` mínimas
(idioma), nunca tokens ni datos del usuario.

### 3.4 Idiomas

- i18next con namespaces por módulo (`forms`, `whatsapp`, `common`) en `src/locales/{es,en}/`. Español rioplatense con
  voseo e inglés; `src/locales/parity.test.ts` exige las mismas claves.
- **Idioma de la interfaz ≠ idioma del contenido:** el editor edita cada texto del formulario en es/en por separado; el
  diálogo del chat muestra nombre y botón **en el idioma del destinatario** (carga ambos recursos sin congelar el chat);
  la vista oficial y el PDF piden la cultura explícita.
- Fechas en la zona de la cuenta con los formateadores compartidos (`formatDateInZone`, `formatDateTimeInZone`,
  `formatTimeInZone`, `formatDateOnly`, `formatRelativeTime`); decimales con `formatExactDecimal`.

### 3.5 Accesibilidad y foco

- Tras mover una pregunta/opción el foco queda en el control de mover del mismo elemento y se anuncia la posición.
- «Listo» devuelve el foco a «Editar»; quitar lleva el foco a la vecina o a «Agregar…» (`ConfirmDialog.focusFallback`).
- Cerrar un diálogo abierto por un botón ajeno devuelve el foco con `useRestoreFocusOnClose`; «Ir a la pregunta» no lo
  devuelve (enfoca el campo marcado).
- Tras guardar con errores, el foco va al primer campo inválido.
- Tras decidir una aprobación, el foco va al resumen; tras resolver un envío, a la fila de la acción.
- Botones de ícono con nombre accesible que incluye el dato y tooltip; sin permiso se ocultan.
- Estados que no dependen solo del color (texto o nombre accesible). Toasts accesibles con teclado también dentro de diálogos.

### 3.6 Contratos validados con zod contra cuerpos HTTP exportados

- Cada respuesta del backend se valida con esquemas **estrictos** (`z.strictObject`) en `api/response.ts`,
  `catalogResponse.ts`, `responses.ts`, `rules.ts`, `workflow.ts`, `approvals.ts`, `pdf.ts` y en `whatsapp/api/forms.ts`
  y `formReplies.ts`. Un campo de más o con otro tipo es un error de contrato (no se muestra el cuerpo en el error).
- Los tests `*HttpContract.test.ts` consumen **cuerpos HTTP reales sin modificar** que exporta la suite del backend
  (ApiFactory + PostgreSQL + Redis, Meta simulado) a `src/test/fixtures/{forms-http, catalogs-http, form-files-http,
  forms-responses-http, forms-rules-http, forms-workflow-http, forms-approvals-http, forms-pdf-http,
  whatsapp-forms-http, whatsapp-catalogs-http, whatsapp-form-actions-http}` con procedencia y SHA256 automáticos.

### 3.7 Tests visuales y de comportamiento

- Vitest + Testing Library por rol y texto, MSW para HTTP (`npm run test`).
- Playwright (`npm run test:visual`, specs en `src/test/visual/`: `forms`, `formReview`, `formPublicationReads`,
  `formSharedControls`, `formCatalogQuestion`, `formFiles`, `chatForms`, `chatCatalogs`, `formReplies`, `catalogManual`,
  `catalogFilters`, `catalogReview`, `responses`, `formRules`, `formPdf`, `formApprovals`, `formWorkflow`,
  `formActionMessages`…) con capturas en es/en a **1440, 390 y 320 px** y transporte simulado.
- Prueba de arquitectura `featureDependencies.test.ts`: `whatsapp` nunca importa `business-forms` (y viceversa); lo
  compartido vive en `shared` (p. ej. `FormRecordFiles`, `PhotoViewer`, `PhonePreview`, `EmbeddedPreview`, `MessageCard`).

---

## 4. Trampas que el front resuelve

| Caso borde | Cómo se resuelve | Referencia |
| --- | --- | --- |
| Refetch que pisa lo que se está escribiendo | El editor se monta una vez con la lectura del montaje actual; los refetch no tocan el borrador | `pages/FormEditorPage.tsx`, `hooks/useFormDraftEditor.ts`, `pages/CatalogEditorPage.tsx` (`mountedDetail`) |
| Guardado incierto (red/5xx) | Cuerpo y UUID congelados; «Consultar guardado»; 404 no prueba rollback y solo habilita «Repetir el mismo intento»; 410/expirado bloquea | `draftSession.ts`, `hooks/useFormDraftEditor.ts` |
| Callbacks tardíos de otra cuenta, idioma o editor | Identidades separadas actor / instancia de editor (`editorId`) / intento; se descartan resultados que no corresponden | `draftSession.ts`, `hooks/useFormPublication.ts` |
| Opciones correlacionadas mal tras guardar | Se correlacionan por `fieldLocalId + localId`, nunca por etiqueta ni índice; el mapa de identidades y el snapshot se aplican juntos | `draftSession.ts`, `validationPaths.ts` |
| Borrar una ayuda en un idioma | Se envía la clave con `null` y se conserva el mapa completo `{es,en}`; nunca se transforma en `help: null` | `editorModel.ts` |
| Cambiar el tipo o el catálogo de una pregunta publicada | Pregunta nueva (LocalId nuevo, `fieldId: null`, `replacesFieldId` anterior) con confirmación; se conservan textos y obligatoriedad | `editorModel.ts` (`replaceQuestionType`, `changeQuestionCatalog`) |
| Error de validación en una pregunta plegada o en el otro idioma | Cambia el idioma, despliega la pregunta y enfoca el primer `aria-invalid` | `pages/FormEditorPage.tsx` |
| Prefijo que se fija a mitad de la edición | Se bloquea al aceptar la publicación aunque después falle; candado + ayuda | `pages/FormEditorPage.tsx` |
| Publicar algo distinto de lo guardado | El diálogo comprueba solo la revisión guardada; con cambios pide guardar primero | `components/PublicationCheckDialog.tsx` |
| Comprobación vencida al hacer clic | Se revisa el vencimiento en el momento del clic y se pide «Volver a comprobar» | idem |
| Número de versión inventado | El número sale del chequeo (`nextVersionNumber`); el diff nombra la versión base solo si su identidad coincide con la activa | idem |
| Diagnósticos repetidos | Se agrupan por canal/código/campo/pregunta/opción/idioma | `publicationPresentation.ts` |
| Diagnóstico de una versión fija sobre un borrador que cambió | Se liga por `FieldId/ChoiceId`; las preguntas quitadas quedan en el resumen sin marcar otra | `validationPaths.ts` |
| URL de la vista oficial que vence | Vive solo en el diálogo; al vencer se quita el iframe y el enlace; cambiar idioma limpia la vista; cultura explícita y `no-store` | `components/OfficialFormPreview.tsx` |
| Doble montaje de StrictMode reanudando la publicación | El efecto reanuda el mismo GET de estado; no hay POST automático | `hooks/useFormPublication.ts` |
| Mensaje de envío recortado | Se valida obligatorio y ≤1024 sin recortar; el recibo reconoce el texto normalizado sin modificar el intento | `whatsapp/lib/formMessage.ts`, `hooks/useFormComposer.ts` |
| Cambia la cuenta vinculada o su idioma con el diálogo abierto | Se compara la revisión opaca y la cultura del destinatario; se bloquea el envío sin perder el texto | `whatsapp/lib/formSendSession.ts`, `hooks/useFormComposer.ts` |
| Envío incierto con el diálogo cerrado | «Consultar envío» en el compositor; un 404 reabre el mismo diálogo con el intento congelado y «Repetir el mismo pedido» | `hooks/useFormComposer.ts`, `components/FormComposer.tsx` |
| Acción de un toast con un cierre viejo | La acción lee el estado actual por ref antes de consultar | `components/ChatComposer.tsx`, `hooks/useFormComposer.ts` |
| Sin `whatsapp.send` | No se infiere "sin vínculo"; se ocultan ambos botones de envío | `components/ChatThread.tsx` |
| Correo oculto | Un correo no visible no deshabilita un formulario elegible; «sin correo» solo si se puede leer | idem |
| Permiso de leer respuestas revocado con el chat abierto | Una "compuerta" síncrona retira valores de mensajes, páginas viejas y pendientes **antes** de publicar la proyección; se cancelan páginas en vuelo sin rollback | `whatsapp/hooks/useChatThread.ts`, `hooks/formReplyReadGate.ts`, `lib/threadPrivacy.ts` |
| Respuesta de formulario inferida por el texto | Solo es Flow/FlowReply si el servidor lo dice; nunca por el cuerpo | `whatsapp/lib/timeline.ts` |
| Decimales y vacíos que se confunden | Escala exacta del string; «Sin respuesta» vs «—» (no existía) vs 0 vs «No» | `shared/lib/decimal.ts`, `business-forms/responses.ts`, `whatsapp/lib/formReplyValue.ts` |
| Fecha sin hora corrida por zona | `DateOnly` formateada sin convertir | `shared/lib/dateTime.ts` (`formatDateOnly`) |
| Plegar la respuesta mueve el historial | `preserveViewport` conserva la posición | `whatsapp/components/FormReplyValues.tsx` |
| Miniaturas que se piden todas de golpe | Solo se piden las visibles; los originales recién al abrir; documentos al descargar | `shared/ui/FormRecordFiles.tsx` |
| Miniatura que desaparece con el visor abierto | El foco vuelve a la tarjeta | `whatsapp/components/FormReplyCard.tsx` |
| Un aviso por archivo fallido | Un único aviso por respuesta | idem |
| Filtros privados de la planilla en la URL | Búsqueda, personas, condiciones y fechas en memoria por dueño y formulario; salir del formulario los limpia | `components/ResponseWorkspaceProvider.tsx`, `hooks/useResponseRouteQuery.ts` |
| Conteos calculados sobre la página visible | Los conteos de facetas los da el servidor con los otros filtros; se recalculan 300 ms después; opciones con 0 deshabilitadas | `components/ResponseFiltersPanel.tsx`, `ResponseFacetControl.tsx` |
| Fechas de rango en otra zona | Los días se convierten a límites UTC en la zona de la cuenta (el "hasta" incluye el día completo) | `responses.ts` (`receivedDateBoundary`) |
| Anterior/Siguiente en el borde de página | Pide la página vecina y salta, conservando filtros y orden; ignora resultados si cambió el dueño | `pages/FormResponseDetailPage.tsx` |
| Detalle con un formulario equivocado en la URL | Si la respuesta es de otro formulario, se muestra «no disponible» | idem |
| Exportar con datos viejos | El botón se habilita solo con la planilla fresca (sin refetch ni placeholder); >10.000 bloqueado; cerrar cancela | `components/ResponseExportDialog.tsx`, `pages/FormResponsesPage.tsx` |
| Catálogo Filters con más de 200 opciones | Guardar vuelve a probar y no escribe; aviso warning | `pages/CatalogFiltersEditorPage.tsx` |
| Rol guardado en una condición que ya no existe | Se conserva el ID y se muestra «Valor no disponible», sin exponer el GUID | `components/CatalogFiltersEditor.tsx` |
| Buscar personas pierde el foco | Espera de 300 ms y el autocompletado no se deshabilita durante la consulta | `pages/CatalogFiltersEditorPage.tsx` |
| Perder `formcatalogs.manage` con el editor abierto | Se limpia la persona y la vista previa; queda solo lectura mientras siga la lectura | idem |
| `usageCount: null` | Se muestra «—» (desconocido), nunca 0 | `components/CatalogListTable.tsx` |
| Movimientos de opciones entre páginas | Se encolan (ItemId + dirección) y se aplican en orden con el mismo Guardar; las nuevas no se mueven hasta guardarse | `catalogDraft.ts`, `components/CatalogManualItemsEditor.tsx` |
| Selección de catálogo fuera de la página | Se conserva y se lee su detalle aparte | `components/CatalogQuestionSelector.tsx` |
| Búsqueda de catálogos sin coincidencias | No se presenta como "no hay catálogos" | idem |
| Reglas con aprobación seguida de otras acciones | Validación local: aprobación última, solo al recibir; PDF solo al aprobar; adjuntar requiere armar antes | `rules.ts`, `components/FormRulesSection.tsx` |
| 403 en reglas | Se desmonta el editor de reglas y se abortan/retiran sus consultas; no alcanza con ocultar textos | `components/FormRulesSection.tsx` |
| Nueva revisión del formulario con reglas a medio editar | Se refrescan preguntas/opciones sin remontar el borrador de reglas | idem |
| Vista previa del PDF vieja | Editar o cambiar idioma la retira; el idioma real sale de `Content-Language` | `pages/FormPdfDesignPage.tsx`, `components/FormPdfPreview.tsx` |
| Pregunta seleccionada en el PDF que ya no existe | Se conserva con «Pregunta retirada» | `pages/FormPdfDesignPage.tsx` |
| Decidir lo propio | El servidor dice `cannotDecideReason: "OwnResponse"`; botones deshabilitados y texto | `components/ApprovalDecisionPanel.tsx` |
| Sin lectura de respuestas en Por aprobar | El número abre un diálogo solo con metadata y decisión | `pages/ApprovalInboxPage.tsx` |
| Repetir una acción ya hecha | Solo «Consultar» para Sin confirmar y «Reintentar» para No se pudo con `canRetry`; nunca para Hecho/No aplica | `workflow.ts` |
| Revisión de envío con resultado incierto | Tras red/5xx/409 se exige «Consultar» antes de otro POST; se congela la revisión al abrir | `hooks/useResponseWorkflow.ts`, `components/ResponseWorkflowHistory.tsx` |
| Blobs de PDF y archivos persistidos | Object URLs que se revocan al cerrar/cambiar; blobs de PDF fuera de QueryCache; miniaturas en un scope que se borra al salir | `components/FormPdfPreview.tsx`, `shared/hooks/usePrivateFormFile.ts`, `shared/hooks/PrivateFormFilesScope.tsx` |
| Leyenda del intervalo de actualización | No se muestra; el chat refresca cada 5 s solo con la pestaña visible | `whatsapp/hooks/useChatThread.ts` |

---

## 5. Observaciones del relevamiento (lo que conviene saber al replicar)

Encontrado en el código; no son decisiones de diseño documentadas:

1. **Editor de catálogo Manual:** la confirmación de descartar usa `cancelLabel={t("keepEditing")}` con el namespace
   `forms`, que **no tiene** esa clave (existe en `assistant.json`). Con la configuración de i18next del repo (sin
   `fallbackNS`), el botón mostraría el texto literal `keepEditing`. El editor de formularios usa «Seguir editando»
   (`stay`). Archivo: `pages/CatalogEditorPage.tsx`. No hay test que lo cubra.
2. **Editor Filters:** sus dos confirmaciones de descartar no son destructivas y usan el «Cancelar» por defecto (el
   Manual y el de formularios sí son destructivas).
3. **Detalle de respuesta desde Por aprobar:** la flecha vuelve a la bandeja, pero su rótulo dice «Volver a la
   planilla» (el estado vacío sí dice «Volver a Por aprobar»). `pages/FormResponseDetailPage.tsx`.
4. **Por aprobar:** el error de la tabla reutiliza «No pudimos cargar los formularios.»
5. **Algunos errores operativos se muestran como párrafo dentro de la superficie** y no (solo) como toast: guardar
   reglas, guardar el diseño del PDF, decidir una aprobación, descargar un PDF fijo, leer el historial de acciones.
   El contrato pide toast para fallos generales; el plan de unificación de mensajes del front sigue abierto.
6. **Planilla:** los botones «Recibida» y «Más filtros» abren el mismo panel (no hay un selector de fechas aparte).
7. **Vista previa del PDF:** el segmentado de idioma tiene «Español»/«English» fijos en el código (no traducidos).
8. **Editor de formularios en 404:** el estado vacío ofrece «Reintentar» aun cuando el formulario no existe.
9. **Lista de chats:** «Todos»/«Sin leer» son dos botones, la búsqueda y la página no van a la URL (solo la conversación).

---

## Anexo: endpoints HTTP que consume este front

Formularios (`api/forms.ts`): `GET /api/forms/definitions?…`, `GET /api/forms/definitions/facets?search=`,
`GET /api/forms/definitions/{id}`, `POST /api/forms/definitions`, `PUT /api/forms/definitions/{id}/draft`,
`GET /api/forms/draft-changes/{requestId}` (recuperación de guardado), `POST …/{id}/publication-checks`,
`POST …/{id}/publications`, `GET …/{id}/publications/{requestId}` (estado propio),
`GET …/{id}/versions/{versionId}/preview?culture=`.

Catálogos (`api/catalogs.ts`): `GET /api/forms/catalogs?…`, `GET /api/forms/catalogs/{id}`,
`GET /api/forms/catalogs/{id}/items?…`, `POST /api/forms/catalogs`, `PUT /api/forms/catalogs/{id}`,
`POST /api/forms/catalogs/filters`, `PUT /api/forms/catalogs/{id}/filters`, `GET /api/forms/catalog-filter-options?…`,
`POST /api/forms/catalog-previews`.

Respuestas (`api/responses.ts`, `shared/api/formFiles.ts`): `GET /api/forms/navigation?…`,
`POST /api/forms/definitions/{id}/records/search`, `POST /api/forms/definitions/{id}/records/filter-counts/search`,
`GET /api/forms/records/{id}`, `POST /api/forms/definitions/{id}/records/export` (XLSX),
`GET /api/forms/records/{recordId}/files/{fileId}/{thumbnail|content}`.

Reglas, historial, PDF y aprobación: `GET|POST /api/forms/definitions/{id}/rules`, `GET …/rules/options`,
`GET …/rules/revisions?page=&pageSize=10`, `GET …/rules/revisions/{n}`; `GET /api/forms/records/{id}/workflow`,
`POST /api/forms/records/{id}/actions/{runId}/{consult|retry}`, `POST …/actions/{runId}/review`;
`GET|POST /api/forms/definitions/{id}/pdf-design`, `POST …/pdf-design/preview` (PDF),
`GET /api/forms/records/{id}/documents/{documentId}/content?download=`; `GET /api/forms/approvals?…`,
`GET /api/forms/approvals/count?…`, `GET /api/forms/approvals/options?onlyMine=`, `GET /api/forms/approvals/{id}`,
`POST /api/forms/approvals/{id}/decisions` (decisión, con `expectedRevision`).

WhatsApp (`whatsapp/api/chat.ts`, `forms.ts`): `GET /api/whatsapp/chats?…`, `GET …/chats/{id}/messages`,
`PUT …/{id}/read`, `PUT …/{id}/mode`, `POST …/{id}/messages`, `GET …/{id}/outgoing/{requestId}`,
`POST …/{id}/outgoing/{requestId}/review`, `GET …/{id}/forms?…`,
`GET …/{id}/forms/{definitionId}/versions/{versionId}/preparation`, `POST …/{id}/form-messages`.
