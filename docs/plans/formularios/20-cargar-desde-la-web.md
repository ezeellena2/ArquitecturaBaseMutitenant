# E20 · Cargar una respuesta desde la web (resumen)

**Estado:** resumen de la etapa 20 del bloque (decisiones 73, 78 y 83 a 88), después de la E19. Corresponde a la Etapa 1 de «Cargar formularios desde la app» de ArquitecturaBase, programada y revisada allá el 2026-10-30, y a su Etapa 2 (lo propio), decidida allá y sin programar. Se planifica con `/plan-etapa` cuando le toque.

**Dónde está el detalle,** en ArquitecturaBase (los commits todavía no están en GitHub, se leen en la copia local):
- el traspaso 3: `docs/plans/2026-10-30-traspaso-multitenant-carga-desde-la-app.md`;
- las decisiones y las etapas de allá: `docs/plans/2026-10-29-cargar-formularios-desde-la-app.md`;
- las reglas programadas: `docs/features/formularios.md`, «Carga desde la app»;
- las pantallas elegidas: `ArquitecturaBaseFront/docs/design/carga-desde-la-app.md` y los tableros CW0 a CW16 de su lienzo.

Se toman las pantallas y las reglas de producto; el código sale de este repo (18). Las marcas **[P1]** a **[P8]** son las decisiones pendientes de la [§7 de la hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md#7-decisiones-pendientes).

## Qué se construye

- **Back:**
  - **Un canal más para responder:** un empleado completa un formulario desde la web, a su nombre, como si fuera por WhatsApp. La respuesta guarda el canal `web`, el `SourceKey` `web:<clave de idempotencia>` y la hora de `TimeProvider`.
  - **Quién:** un miembro activo con `formrecords.create`, y solo los formularios activos que son para empleados o para los dos (50). Se comprueba antes de leer y otra vez dentro de la UoW. Un cliente sigue respondiendo por WhatsApp. **[P1]**
  - **Las rutas,** todas con `formrecords.create`:
    - los formularios que la persona puede cargar, con el filtro de disponibilidad de la E17. Es una ruta aparte de la lista de `forms.read`, porque `[HasPermission]` pide un solo permiso. Trae solo lo que necesita quien carga, sin conteos;
    - la versión activa para cargar: preguntas, títulos, texto del botón, tope por archivo y, en cada pregunta de catálogo, la única opción que dejan los filtros o el dato que le falta a la persona;
    - las opciones de una pregunta de catálogo: 50 por página, con búsqueda en el servidor y con los filtros fijos y por destinatario de quien carga, en SQL antes de paginar. No tiene el tope de 200 de WhatsApp. En «elegir varias», lo que se elige sigue hasta el máximo publicado;
    - el `POST` multipart con las respuestas y los archivos, con `AssistantControllerBase.Upload` como modelo.
  - **La misma captura que WhatsApp:** un solo lugar valida y numera. Valida contra la versión activa sellada, con las mismas reglas: catálogos, archivos (E18), condiciones, cálculos y líneas (E19). La numeración es la misma.
  - **Solo la versión activa.** Una sesión de WhatsApp conserva su versión; la web exige la activa al enviar:
    - si se publicó otra, 409 `Forms.Entry.VersionChanged`, y el front ofrece pasar a la versión nueva sin perder lo cargado;
    - si el formulario se dio de baja, 409 `Forms.Entry.Unavailable`;
    - una pregunta que no existe, 404 `Forms.Entry.QuestionNotFound`.
  - **La oferta sella solo lo elegido.** Antes de guardar se comprueba que cada opción esté activa y pase los filtros para esa persona, también en un catálogo de miembros o de contactos. La lista completa no se guarda, porque puede tener miles. Una opción dada de baja entre abrir y enviar responde `Forms.Record.OptionWithdrawn` en su pregunta. **[P6]**
  - **Los filtros por persona (E16):** si a la persona le falta el dato que pide un filtro, la pregunta frena con `Forms.Record.RecipientDataMissing` y el nombre del dato. Si los filtros dejan una sola opción, esa opción ya viene elegida.
  - **Archivos:**
    - viajan en el mismo `POST`, sin una subida previa aparte;
    - tienen los límites de la E18: los de cada pregunta, hasta 10 por respuesta, el tope por archivo de la instalación y 100 MiB en total;
    - se verifican por contenido, igual que los que llegan por WhatsApp. Completa o nada;
    - la ruta acepta hasta 101 MiB (`[RequestSizeLimit]`).
  - **Reintentos:** `[Idempotent(Mode = IdempotencyMode.DurableReference)]` con su `[IdempotencyDurable]`, y el `SourceKey` como vínculo privado:
    - la misma clave con las mismas respuestas devuelve el primer resultado, aunque después se haya publicado otra versión o se haya dado de baja el formulario: el filtro responde antes de que corra la acción;
    - el número y los cálculos son privados y no pueden quedar en el almacén global de claves, por eso no va en `BufferedJson`;
    - la misma clave con otras respuestas es 422, y para el front otras respuestas son otro envío: usa `sameOperation` de `useIdempotentMutation`, que cambia la clave si cambió lo cargado.
  - **Lo propio** (85): quien tiene `formrecords.create` y no `formrecords.read` lee solo sus respuestas, en la planilla, el detalle y los archivos. El servidor filtra por `RespondentUserId`, también en una dirección directa: lo ajeno responde 404. Vale para lo que cargó y para lo que respondió por WhatsApp.
  - **El canal en las respuestas:** la columna «Canal» de la E15 suma el valor nuevo, y su filtro llega a los conteos, la navegación y el Excel. **[P2]**
  - **Las empresas:** la respuesta guarda las empresas activas de quien carga, como una que llega por WhatsApp (9).
- **Front:**
  - **Las entradas** (83):
    - «Cargar» al final de cada fila de «Formularios», para quien tiene `formrecords.create`. No aparece en los dados de baja, y en un formulario solo para clientes queda grisado, con el motivo en su tooltip (43);
    - «Agregar respuesta» en el encabezado de la planilla: en la compu abre el panel, y en el teléfono, la página;
    - quien solo carga entra a «Gestión de formularios» y ve la lista con sus formularios y «Cargar», sin las columnas de respuestas. El nombre abre la pestaña «Respuestas» con lo suyo. No hay «Mis cargas» aparte (85).
  - **La página «Nueva respuesta»** (84), en `/org/formularios/{id}/cargar`, para la compu y el teléfono:
    - Cancelar y el botón del formulario van en la banda; en el teléfono, ese botón queda fijo al pie;
    - se valida al enviar: cada error aparece en su pregunta y el foco va a la primera con error. Después, en vivo;
    - salir con algo cargado pide confirmación (`useUnsavedChangesGuard`);
    - mientras sube, el botón queda grisado con su mismo texto (43). **[P5]**
  - **El panel** (84), al costado de la planilla, solo en la compu: «Mantener las respuestas para la siguiente» conserva todo menos los archivos, y «Pantalla completa» pasa lo cargado a la página. Al enviar, el aviso dice el número y la planilla se relee con la respuesta marcada.
  - **Las listas de catálogo:** una pregunta de catálogo es siempre una lista con búsqueda en el servidor, con el total, la descripción de cada opción y «Mostrar 50 más». En el teléfono ocupa la pantalla, con la búsqueda arriba y «Listo» al pie al elegir varias.
  - **Las fotos y los documentos:** se arrastran o se eligen, con un contador. Tipo, tamaño y cantidad se revisan antes de enviar. Quitar no pide confirmación, porque nada se subió todavía. En el teléfono, «Tomar foto» va primero, y una pregunta «Solo cámara» no ofrece la galería. Las fotos son JPEG o PNG, y con `accept` en esos tipos el iPhone convierte las suyas al elegirlas.
  - **«Enviada»:** muestra lo que se mandó, el número y la hora, con «Cargar otra» y «Ver en la planilla». Se arma con lo enviado y con lo que devuelve el `POST`, que suma los cálculos: no vuelve a pedir la respuesta guardada. **[P7]**
  - **Lo cargado** vive en la página o en el panel: nunca va a la URL, al storage ni a la caché.
  - **En la planilla y el detalle:** el canal en su columna y en sus filtros, y en el detalle, «Recibida · … · la cargó …» en el lugar del origen de WhatsApp. No hay «Abrir conversación».

## Cómo se arma cada pregunta (86 y 87)

El autor decide el contenido: orden, textos, ayuda, obligatoria, opciones, largo máximo, rango de «elegir varias» y límites de los archivos. La app decide la forma, con una tabla fija por tipo de pregunta:

| Ocupa | Tipos de pregunta |
| --- | --- |
| Una columna | Número, fecha, sí o no, texto corto de hasta 30 caracteres y elegir una con lista desplegable |
| Dos columnas | De un catálogo (una o varias), elegir una con opciones en fila (hasta cinco, de hasta 20 caracteres) y texto corto de hasta 80 |
| El renglón entero | Texto largo, texto corto de más de 80 o sin largo máximo, elegir varias con opciones propias, foto, documento y grupo de líneas |

- **Columnas:** tres en la compu, dos en la tableta y una en el teléfono y en el panel. Salen del ancho de la zona: tres desde 46 rem, dos desde 30 rem y una por debajo.
- **Orden:** las preguntas no se reordenan. Una que no entra baja de renglón, con una línea entre cada renglón visible. Por eso, el orden del editor decide dónde quedan huecos.
- **Zonas** (88):
  - cada título de sección de la E14 arma una zona. Las preguntas que van antes del primer título quedan en «Datos»;
  - las fotos y los documentos van al final, en «Fotos y documentos». El dibujo de los títulos (CW15-B) lo confirma;
  - todas las zonas se pliegan y arrancan abiertas. Plegada, la banda dice «N de M respondidas», y la de los archivos, lo que se adjuntó, como «3 fotos» o «2 fotos · 1 archivo». Sin archivos dice «0 de 1 respondidas»;
  - al enviar con errores, la zona plegada que los tiene se abre sola.
- **Controles:**
  - número y fecha son angostos;
  - sí o no arranca sin nada marcado;
  - hasta cinco opciones cortas van en fila, y con más, una lista desplegable;
  - elegir varias muestra «Elegí hasta N»;
  - el texto largo tiene de 2 a 6 renglones.
- **La unidad** va en la etiqueta que escribe el autor, como «Densidad (kg/ha)». No hay un dato «Unidad» (86).

**Lo de la E19,** que ArquitecturaBase no tiene. Es la propuesta para que Diseño la dibuje, con CW16 como base:
- **una pregunta condicional** aparece en su lugar y los renglones se recalculan. Lo que se ocultó no se manda. «N de M respondidas» cuenta solo las preguntas visibles, y una zona sin preguntas visibles no se ve;
- **un grupo de líneas** ocupa el renglón entero;
- **un cálculo** no se ve al cargar **[P7]**.

## Lo que ya se pagó allá (no repetir)

Cada punto lleva su test, escrito durante la etapa:
1. **Los renglones se arman con las columnas en uso.** Se armaban siempre de a tres, y en el teléfono dos preguntas quedaban en un mismo bloque, sin línea entre ellas. El ancho de cada pregunta se recorta a las columnas disponibles.
2. **Las columnas salen del ancho de la zona, no del de la pantalla.** En una tableta con un panel del menú abierto entra una sola columna. Con el ancho de la pantalla, un campo se salía de la zona. Se mide con `ResizeObserver`.
3. **En el panel, número y fecha siguen angostos.** Ir «a lo ancho» con una sola columna vale solo para la página.
4. **Las migas también miden su contenedor.** Si la barra superior mide menos de 40 rem, quedan solo el formulario y la hoja. Cuando dependían del ancho de la pantalla, quedaban segmentos vacíos.

## Tableros

Los dibuja Diseño, con nuestras piezas y la decisión 43. La referencia son los tableros elegidos del lienzo de ArquitecturaBase, entre paréntesis. El texto para el chat de Diseño va en [20-texto-diseno.md](20-texto-diseno.md).

- `FRM-Formularios-20` y su teléfono (CW1-B): «Cargar» en cada fila y lo que ve quien solo carga;
- `FRM-Cargar-Respuesta` y su teléfono (CW2-A, CW12, CW13-A y CW16): la página, con las zonas, la grilla, los títulos, las preguntas que aparecen, las líneas, los errores y la confirmación al salir;
- `FRM-Cargar-Panel` (CW2-B): el panel junto a la planilla;
- `FRM-Cargar-Lista` y su teléfono (CW3): la lista, los filtros por persona, una sola opción, sin el dato y elegir varias;
- `FRM-Cargar-Archivos` y su teléfono (CW4): agregar, los límites, el envío, el corte y la cámara;
- `FRM-Cargar-Enviada` y su teléfono (CW5): «Enviada», versión nueva, dado de baja, opción dada de baja y sin conexión;
- `FRM-Respuestas-20` y `FRM-Respuesta-20` (CW6-A): el canal, su filtro y el origen en el detalle;
- `FRM-Respuestas-Propias` y su teléfono (CW6-C): la misma planilla con lo suyo.

**Mandan sobre esta disposición:**
- **el teléfono es una app:** «Cargar» va en la lista del teléfono con nuestra forma de fila, no en una tabla con desplazamiento;
- **las fichas del front:** un dato por celda, fechas completas y sin rótulos que expliquen;
- **la decisión 43** en cada botón, incluso donde el lienzo de allá muestre otra cosa.

## Decisiones tomadas (no se vuelven a preguntar)

- **Un empleado, a su nombre** (73). Cargar a nombre de otra persona queda para después.
- **La misma captura que WhatsApp:** un solo lugar valida y numera.
- **Sin permisos nuevos.** Alcanza con `formrecords.create`.
- **Sin borradores guardados:** lo cargado no sobrevive a la página.

## Fuera de esta etapa

- Cargar a nombre de otra persona, como lo que dicta un cliente.
- Importar muchas respuestas desde un Excel.
- Borradores guardados y carga sin conexión.
- La carga en filas (CW2-C).
- Ofrecer en la web qué cargar después, como las sugerencias del chat.
- El estado, el PDF y el motivo de rechazo en lo propio, que suma la E21.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más un **recorrido real**:
1. cargar desde la web una respuesta con una condición, dos renglones, un cálculo y una foto, y ver que queda igual que una por WhatsApp: el mismo número correlativo, los mismos valores y el cálculo, y en la planilla con su canal;
2. repetir el envío después de un corte: devuelve el primer número, sin saltos;
3. publicar otra versión con la página abierta: pasa a la nueva sin perder lo cargado;
4. entrar con una cuenta que solo tiene `formrecords.create`: ve sus formularios, carga y ve solo lo suyo. Una dirección directa a una respuesta ajena da 404;
5. sacar una foto con la cámara de un teléfono real.

## Requisitos previos

La E19 cerrada.

## Ajustes ya decididos

- **Idioma:** el contenido no tiene idioma (35), así que el idioma por respuesta de allá (`PresentationCulture`) no se toma. Los textos del sistema van en el idioma del menú.
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** las tablas de la E14, con RLS. El canal es un valor más de `FormRecords.Channel`, y no hace falta una columna como `EnteredInApp`. Cada ruta nueva lleva su test de aislamiento, y lo propio, el suyo: otra persona de la misma organización recibe 404.
- **Accesos:** las rutas llevan `[Access(Access.Business)]`. La organización sale del claim, y quien carga, de la sesión: nunca del pedido.
- **Datos de las personas:** los filtros usan los datos de esa persona en esa organización (42).
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos:** `formrecords.create`, sin permisos nuevos.
- **Paginado:** 50 es uno de los tamaños de `PagedRequest.AllowedPageSizes`.
- **Cuotas:** las rutas cuentan en las cuotas generales. Un perfil propio sería un cambio de esa ficha.
- **Almacenamiento (E10):** los archivos van en `IFileStore`, como los de la E18.
