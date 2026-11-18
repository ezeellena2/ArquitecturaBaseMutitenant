# E20 · Cargar una respuesta desde la web (spec)

**Estado:** pendiente · spec de la etapa 20 del bloque (decisiones 73, 78, 83 a 90, 94 a 98, 101, 104, 153, 166, 177, 178, 183 y 184), con el [formato](plantilla.md) de las etapas. Corresponde a la Etapa 1 de «Cargar formularios desde la app» de ArquitecturaBase, programada y revisada allá el 2026-10-30, y a sus etapas 2 (lo propio), 4 (listas dependientes) y 5 (títulos de sección), programadas allá el 4 y el 5/11. La carga del bloque es la etapa 2 de sus renglones, programada allá el 2026-11-07. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E19.

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otro, con las mismas secciones. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| «Cómo se arma cada pregunta», Contrato, Datos, Pasos y «Lo que ya se pagó allá» | [armado, contrato, datos y pasos](20-parte-contrato-datos-y-pasos.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T4, T22, T23, T24 y la §6, punto 7; la [E15](15-respuestas.md): rutas y filtros; la [E16](16-datos-de-las-personas.md): los filtros por destinatario; la [E17](17-ciclo-de-vida.md): la disponibilidad; la [E18](18-fotos-y-documentos.md): verificar y guardar archivos, y la [E19](19-condiciones-calculos-y-lineas.md): la captura y el bloque.
2. **Decisiones** de la hoja de ruta: 9, 35, 37, 43, 50, 54, 73, 78, 83 a 90, 94 a 98, 101, 104, 141, 147, 153, 166, 177, 178, 183 y 184.
3. **Fichas:**

   | Ficha | Por qué acá |
   | --- | --- |
   | [idempotencia](../../rules/idempotencia.md) | el `POST` con archivos, en modo durable |
   | [guardado](../../rules/guardado.md) y [multitenancy](../../rules/multitenancy.md) | subir fuera de la UoW; quien carga sale de la sesión |
   | [permisos](../../rules/permisos.md) y [api-http](../../rules/api-http.md) | rutas aparte para cada permiso |
   | [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) | las opciones, de a 50, y lo propio |
   | [validacion](../../rules/validacion.md) y [result-y-errores](../../rules/result-y-errores.md) | cada error en su pregunta, también el de un ítem |
   | [textos-y-traducciones](../../rules/textos-y-traducciones.md) y [tests](../../rules/tests.md) | «App», «ítem» y qué lleva test |

4. **Código para copiar:** la columna «Copia de» de los [pasos](20-parte-contrato-datos-y-pasos.md#pasos).
5. **Referencia de producto,** solo para leer: en ArquitecturaBase, `docs/plans/2026-10-30-traspaso-multitenant-carga-desde-la-app.md`, `docs/plans/2026-10-29-cargar-formularios-desde-la-app.md` y, en `docs/features/formularios.md`, «Carga desde la app», «Lo propio», «Listas dependientes» y «Títulos de sección»; para el bloque, `docs/plans/2026-11-07-renglones-etapa-2-implementacion.md`. En `ArquitecturaBaseFront`, `docs/design/carga-desde-la-app.md` y `renglones-en-formularios.md`. Los commits están en la copia local. Mandan esta spec y las reglas y los diseños de este proyecto (108), y el código sale de este repo.

## Qué se construye

**Un canal más para responder:**
- un empleado completa un formulario desde la web, a su nombre. Es un miembro activo con `formrecords.create`, sin teléfono verificado (89), y solo en los formularios activos que son para empleados o para los dos (50). Un cliente sigue respondiendo por WhatsApp;
- la respuesta guarda el canal `web`, el `SourceKey` `web:<clave de idempotencia>` y las empresas activas de quien carga (9);
- **la misma captura que WhatsApp:** un solo lugar valida y numera contra la versión sellada, con catálogos, archivos, condiciones, cálculos y el bloque;
- **un número escrito** se lee con la región del idioma de quien carga: en español, coma decimal y punto de miles. Lo que se lee distinto con y sin los miles («1.500») es un error de su pregunta, `numberAmbiguous`, que pide escribirlo sin separador de miles, y al servidor llega el número con punto (207);
- **solo la versión activa.** Si se publicó otra, el front ofrece pasar a la nueva sin perder lo cargado;
- **la oferta sella solo lo elegido:** cada opción tiene que estar activa y pasar los filtros para esa persona, también en un catálogo de miembros o de contactos. La lista completa no se guarda;
- **los filtros por persona:** si le falta el dato, la pregunta frena y lo nombra. Si los filtros dejan una sola opción, ya viene elegida. Las opciones no tienen el tope de 200: se piden de a 50, con búsqueda, y los filtros corren en SQL antes de paginar.

**Las listas dependientes** (94): un tercer filtro, «según otra respuesta»:
- la pregunta de la que depende va antes, es de elegir una y elige del mismo catálogo que el campo de papel «Filtro». No oculta la pregunta: achica sus opciones;
- al guardar se comprueba que la opción corresponda a esa respuesta. Si la pregunta de la que depende quedó oculta, la lista también;
- va en una pregunta suelta, no en una del bloque. Copiar un formulario la copia;
- no lleva los límites de WhatsApp: ni el aviso del máximo por persona ni el freno de «elegir varias» con más de 20.

**Archivos:** viajan en el mismo `POST`, con los límites de la E18, y se verifican por contenido, igual que los de WhatsApp. Completa o nada.

**Reintentos:** la misma clave con las mismas respuestas devuelve el primer resultado, aunque después se haya publicado otra versión o se haya dado de baja el formulario. El número y los cálculos son privados, por eso no va en `BufferedJson`. La misma clave con otras respuestas es 422; para el front, otras respuestas son otro envío.

**Formularios solo para la app** (94, 96, 98, 104, 178 y 200). No se pueden mandar por WhatsApp en cinco casos: la organización no tiene un número conectado; usa una lista dependiente; usa un catálogo a mano más grande de lo que admite WhatsApp, de más de 200 opciones en «elegir una» o de más de 20 en «elegir varias», tiene un bloque con un mínimo de más de 10 ítems, o tiene otra cosa que WhatsApp no puede mostrar, como una condición que compara números o fechas, demasiadas preguntas en una pantalla o una respuesta demasiado grande (200). En los cinco:
- si es para empleados o para los dos, se publica con un aviso que no frena (37). No pasa por Meta: sella la versión, la marca y la activa en la misma UoW, sin trabajo durable ni Flow;
- **los frenos de WhatsApp no corren:** una versión solo para la app no va a Meta, así que no pasa por `Check` ni por el tope de opciones de un filtro fijo. Vuelven a correr al publicar con número;
- si es solo para clientes, no se publica, porque ellos responden solo por WhatsApp. El mínimo de más de 10 frena como en la E19;
- **en el chat,** «Enviar formulario» lo muestra apagado, con su motivo en lugar de la versión y las preguntas (166 y 184): con una lista dependiente, ««Proveedor» depende de otra respuesta: se carga desde la app.», y con un catálogo demasiado grande o un mínimo de más de 10, el mismo motivo que dio al publicar; con más de un motivo, el de la primera pregunta. Uno publicado sin número, cuando después se conecta uno, aparece como en un número sin el Flow de su versión (103), hasta volver a publicar;
- para mandarlo cuando se pueda, hay que volver a publicar: `NothingToPublish` no aplica si la versión activa es solo para la app y ahora se puede mandar. Cambia lo que la E14 dice de `ChannelUnavailable` y `CatalogTooLarge`.

**Lo propio** (85): quien tiene `formrecords.create` y no `formrecords.read` lee solo sus respuestas, en la planilla, el detalle y los archivos, también lo que respondió por WhatsApp. El servidor filtra por `RespondentUserId`, y lo ajeno responde 404, también en una dirección directa. Quien solo carga ve «Formularios» con sus formularios, «Cargar», «Mis cargas» y «Mi última carga», con su número y su fecha («Todavía no cargaste» si no hay), nunca los totales de la organización (153, que cambia la 83). El nombre abre «Respuestas» con lo suyo: la planilla de siempre, con «Empresa» (24), sin la columna «Cargó», sin «Cargó» en «Filtros» y sin «Exportar a Excel», con «Agregar respuesta». Los conteos también se acotan: el de personas la nombra solo a ella, el de un catálogo trae solo las opciones que eligió y un filtro por otra persona no devuelve nada. **El filtro «Estado»** suma «Dados de baja», con los formularios dados de baja en los que cargó algo: ve lo suyo, sin «Cargar» (101).

**Las entradas** (83): «Cargar» al final de cada fila de «Formularios» y «Agregar respuesta» en el encabezado de la planilla, que en la compu abre el panel, y en el teléfono, la página. Las dos siguen la misma regla: no aparecen en un borrador ni en un dado de baja, y en uno solo para clientes quedan grisadas, con el motivo en su tooltip (43).

**La página «Nueva respuesta»** (84), en `/org/formularios/:id/cargar`, para la compu y el teléfono:
- Cancelar y el botón del formulario van en la banda; en el teléfono, ese botón queda fijo al pie;
- se valida al enviar: cada error en su pregunta y el foco en la primera. Después, en vivo. Salir con algo cargado pide confirmación;
- mientras sube, el botón dice «Enviando…» con su aro, y una barra de avance, sin palabras, muestra cuánto falta (95 y 115);
- lo cargado vive en la página: nunca va a la URL, al storage ni a la caché;
- **«Mantener las respuestas para la siguiente»** (147) va en la banda, antes de Cancelar y Enviar, y en el teléfono, en el pie fijo, arriba de Enviar. Marcada, al enviar el aviso dice el número y el formulario queda con lo cargado, sin los archivos y con el foco en la primera pregunta; sin marcar, se ve «Enviada». No se recuerda al volver a entrar.

**El panel** (84), al costado de la planilla, solo en la compu, con las mismas zonas que la página (141): «Mantener las respuestas para la siguiente» conserva todo menos los archivos, y «Pantalla completa» pasa lo cargado a la página. Al enviar, el aviso dice el número y la planilla se relee con la respuesta marcada. **Cada envío es otra respuesta:** después de un envío que llegó, el front arma otra clave de idempotencia; solo un reintento después de un corte repite la anterior.

**Las listas de catálogo:** una pregunta de catálogo es siempre una lista con búsqueda en el servidor, con el total, la descripción y «Mostrar 50 más». En el teléfono ocupa la pantalla, con «Listo» al pie al elegir varias. **Una lista dependiente espera** apagada, con «Elegí primero «Zona»» en su campo, y en el teléfono, en su botón (166). Con la respuesta elegida trae solo lo suyo, y se limpia si esa respuesta cambia.

**Las fotos y los documentos:** se arrastran o se eligen, con un contador. Tipo, tamaño y cantidad se revisan antes de enviar. En el teléfono, «Tomar foto» va primero, y «Solo cámara» no ofrece la galería.

**El bloque** (146 y 177):
- en la página, ocupa el renglón entero de su zona, donde lo puso el autor, con su nombre como etiqueta: una tabla con una fila por ítem y una columna por pregunta, con su etiqueta arriba. El número del ítem va en la primera columna, y ↑ ↓ y 🗑, en rojo, al final de cada fila; el tacho quita sin preguntar, como un archivo. Si no entra, la tabla se desplaza de costado en su caja, y el texto largo es una celda que crece;
- arranca con el mínimo de ítems. «Agregar ítem», al pie y sin contador, suma uno vacío y lleva el foco a su primera pregunta. Con el máximo se apaga, con «Hasta 20 ítems.» en su tooltip, y con el mínimo, el tacho, con el suyo. ↑ ↓ dejan el foco en el mismo botón del ítem movido, y quitar lo lleva al ítem que queda en su lugar o a «Agregar ítem»;
- al enviar, cada ítem se valida como preguntas sueltas: la celda con un error se marca, el mensaje va debajo de su fila con el nombre de la pregunta («Cantidad: completá esta respuesta.») y el foco va a la primera. Si faltan ítems, lo dice debajo de la tabla: «Agregá al menos 1 ítem.»;
- **en el panel y en el teléfono,** un recuadro por ítem, con «Ítem 2», ↑ ↓ y «Quitar» en su banda (183), y las preguntas una debajo de otra. El nombre del bloque va arriba, como etiqueta, así la banda entra en 320 aunque el bloque se llame «Materiales de construcción». En el teléfono, «Agregar ítem» va al pie de los recuadros, y Enviar sigue fijo abajo;
- «Mantener» conserva también los ítems; una versión nueva los conserva si el bloque sigue, con las preguntas que siguen con el mismo tipo, y «Enviada» muestra el bloque en una línea, como su columna en la planilla;
- **en el editor,** dos (i) suman la app: la del máximo dice «En WhatsApp se cargan hasta 10 ítems; en la app, hasta el máximo.», y la de «Se carga varias veces», «Se carga una vez por ítem, en la tabla del bloque.».

Los textos del bloque que suma esta etapa, con su inglés:

| Dice | En inglés |
| --- | --- |
| «Agregar ítem» · «Hasta 20 ítems.» · «Agregá al menos 1 ítem.» | «Add item» · «Up to 20 items.» · «Add at least 1 item.» |
| «Ítem 2», en la banda de un recuadro | «Item 2» |
| «En WhatsApp se cargan hasta 10 ítems; en la app, hasta el máximo.» | «WhatsApp loads up to 10 items; the app, up to the maximum.» |
| «Se carga una vez por ítem, en la tabla del bloque.» | «Loaded once per item, in the block's table.» |

**«Enviada»:** lo que se mandó, los cálculos, el número y la hora, con «Cargar otra», «Volver a Formularios» y «Ver en la planilla» (166). Se arma con lo enviado y con lo que devuelve el `POST`.

**En lo que ya existe:** «App» en «Canal» y en su filtro, en los conteos, la navegación y el Excel (90); en el detalle, «Recibida · … · en la app, la cargó …», sin «Abrir conversación»; en el editor, «Según otra respuesta»; al publicar, que queda solo para la app y por qué, y «Solo en la app» en «En WhatsApp» (98).

**Cómo se arma cada pregunta,** con los dos anchos, las zonas y lo que dice una zona plegada («3 de 5 respondidas», 166), está en la [parte](20-parte-contrato-datos-y-pasos.md#cómo-se-arma-cada-pregunta-86-87-y-141).

## Pantallas

Aprobados y publicados en el lienzo v154, con el armado del front real de ArquitecturaBase (126 y 134) y el recorrido guiado `cargar-una-respuesta`. El ejemplo es «Pedido de materiales», versión 4, que suma «Zona», «Proveedor» con su lista dependiente, «Foto del remito» y el bloque «Productos», y por eso queda solo para la app. Con los traspasos 5, 7 y 10 se redibujaron todos menos `FRM-Cargar-Archivos` y se sumó el chat, aprobados y publicados en el lienzo v168.

| Tablero | Qué muestra | Estado |
| --- | --- | --- |
| `FRM-Formularios-20` y su teléfono | «Cargar», «Solo en la app» y lo que ve quien solo carga | Aprobados en la v168: «Mis cargas» y «Mi última carga» (153) |
| `FRM-Cargar-Respuesta` y su teléfono | la página, con zonas, grilla, títulos, lo condicional, el bloque, «Mantener», errores y salir | Aprobados en la v168: los dos anchos, las casillas en columnas y «Mantener» (141 y 147); el bloque, con «Agregar ítem» y sin contador, en tabla y, en el teléfono, en recuadros con «Ítem 2» (177 y 183), y la zona plegada con «3 de 5 respondidas» (166) |
| `FRM-Cargar-Panel` | el panel junto a la planilla | Aprobado en la v168: con zonas (141), los recuadros del bloque y la zona plegada (166 y 177) |
| `FRM-Cargar-Lista` y su teléfono | la lista, una sola opción, sin el dato, elegir varias y la que espera otra respuesta | Aprobados en la v168: «Elegí primero «Zona»» (166) |
| `FRM-Cargar-Archivos` y su teléfono | agregar, los límites, la barra de avance, el corte y la cámara | Aprobado |
| `FRM-Cargar-Enviada` y su teléfono | «Enviada», versión nueva, dado de baja, opción dada de baja y sin conexión | Aprobados en la v168: «Volver a Formularios» y el bloque en una línea (166 y 177) |
| `FRM-Respuestas-20` y `FRM-Respuesta-20`, con sus teléfonos | «App», su filtro y el origen en el detalle | Aprobados en la v168: «Filtros» y los chips (144) y el detalle alineado (139) |
| `FRM-Respuestas-Propias` y su teléfono | la misma planilla con lo suyo | Aprobados en la v168: «Filtros» y los chips (144) |
| `FRM-Editor-Listas` y su teléfono | «Según otra respuesta» | Aprobados en la v168: el bloque, sin el «Grupo de líneas» (177) |
| `FRM-Publicar-20` y su teléfono | solo para la app, en los cuatro casos, y uno solo para clientes que no se publica | Aprobados en la v168: el bloque y el mínimo de más de 10 (177 y 178) |
| `WA-Chats-Formulario-20` y `M-WA-Chats-Formulario-20` | en «Enviar formulario», los formularios solo para la app, apagados con su motivo | Aprobados, nuevos en la v168 (166 y 184) |

**La página** sigue a la de allá: la banda con Cancelar y Enviar, y una zona por título, con la grilla de hasta tres por renglón. Lo que allá dice «Enviando… 64 %» acá es «Enviando…» con su aro y una barra sin palabras (95 y 115). Sin el dato de la persona, un cartel lo nombra, porque bloquea, y Enviar queda grisado con el motivo.

**Publicar solo para la app** va en la zona «Avisos» de la E16, con «En WhatsApp: No» en «Qué se publica», sin «Ver vista oficial» y con el botón «Publicar» en la banda. Cada motivo nombra su pregunta y dice que se carga «desde la app», como el canal (90), y no «desde la web»: el chat repite el del catálogo y el del mínimo (184). Uno solo para clientes frena en «Problemas» (134).

**«Agregar filtro ▾»,** el de la E16, suma «Según otra respuesta»: ofrece «Según quien lo recibe», «Filtro fijo» y «Según otra respuesta» (134).

**«Canal»** se suma a los filtros rápidos de la planilla, y la cantidad sale de la barra: ya está en la pestaña «Respuestas».

**Valen** las fichas del front y la decisión 43 en cada botón. El teléfono es una app: «Cargar» va en la lista con nuestra forma de fila.

**Valen las de toda la app** (160 a 164, 173 a 175 y 180): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E14] | La captura y la publicación. | Se pueden separar lo común y una publicación sin trabajo durable. |
| [E15] | Las lecturas. | Admiten un alcance «solo lo mío», sin copiar las consultas. |
| [E16] | La preparación de la oferta. | Sirve también paginada, sin el tope de 200. |
| [E18] | `IFileContentInspector` y el diario. | Sirven igual para un archivo que llega por multipart. |
| [E19] | `FormConditionEvaluator`, `lib/conditions.ts` y el bloque en la captura. | El front y el servidor dan lo mismo, y el bloque viaja en el mismo envío. |
| Allá | La etapa 2 de renglones, programada el 2026-11-07 y sin probar a mano: la tabla, los recuadros, los errores por ítem y «Mantener». | Se contrasta con el código de allá al planificar, y lo que allá cambió se adopta si no choca con las reglas de acá (126). LD1-A a LD3-A no entran (191). |
| Front | `useIdempotentMutation`, `useUnsavedChangesGuard` y el panel al costado. | Están, o llegan con la base de la 12. |
| Celular | Que el reenvío por USB llegue a `localtest.me` (ver «Cómo se cierra»). | Anda sin cambiar la configuración. |

## Decisiones tomadas (no se vuelven a preguntar)

- **Un empleado, a su nombre** (73), sin permisos nuevos ni teléfono verificado (89).
- **La misma captura que WhatsApp.**
- **Sin borradores guardados:** lo cargado no sobrevive a la página.
- **El canal se llama «App»** (90); el valor técnico sigue siendo `web`.
- **Se publica sin WhatsApp** (98), también con un mínimo de más de 10 en un formulario para empleados o para los dos (178).
- **Las listas dependientes** van acá y solo en la web (94).
- **El bloque se carga como allá** (177): una tabla en la página y un recuadro por ítem en el panel y en el teléfono, con «Agregar ítem» y sin contador, y «Ítem 2» en la banda de cada recuadro (183).
- **Un formulario solo para la app aparece apagado en el chat, con su motivo** (184), en los tres casos en que hay chat.
- **Lo propio tiene sus rutas:** `[HasPermission]` pide un solo permiso, y el mismo servicio les da el alcance.

## Fuera de esta etapa

- Cargar a nombre de otra persona desde la web (73), importar muchas respuestas desde un Excel, borradores guardados y carga sin conexión.
- La carga en filas, y ofrecer en la web qué cargar después.
- Las listas dependientes en WhatsApp, con el intercambio cifrado con Meta.
- Elegir primero por un campo y una lista dependiente dentro del bloque, que allá son LD1-A a LD3-A: quedan para después de la E25, con las listas dependientes en WhatsApp (191).
- El estado, el PDF y el motivo de rechazo en lo propio, y corregir y anular, también con ítems, que suma la E21.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más un **recorrido real**:
1. cargar una respuesta con una condición, dos ítems, un cálculo y una foto, y ver que queda igual que una por WhatsApp, con el canal «App»;
2. repetir el envío después de un corte: devuelve el primer número, sin saltos;
3. publicar otra versión con la página abierta: pasa a la nueva sin perder lo cargado, ni los ítems;
4. entrar con una cuenta que solo tiene `formrecords.create`: ve sus formularios, carga y ve solo lo suyo. Una dirección directa a una respuesta ajena da 404;
5. en una organización sin número conectado, publicar, cargar desde la web y, al conectar un número, volver a publicar y mandarlo por WhatsApp;
6. elegir en una lista dependiente, cambiar la respuesta de la que depende y ver que se limpia, y en el chat, ver ese formulario apagado con su motivo;
7. en un teléfono real, sacar una foto con la cámara y cargar dos ítems en sus recuadros.

**Desde el celular:** como allá, un Android entra por USB, con el reenvío de puertos de `chrome://inspect` (el 5174, a `localhost:5174`), y nada queda expuesto en internet. Allá abre `localhost`; acá el dominio de desarrollo es `https://localtest.me:5174`, con un subdominio por organización, y la carga va en el dominio principal. `localtest.me` y sus subdominios resuelven a 127.0.0.1 también en el teléfono, donde escucha el reenvío, y el proxy de Vite recibe el `Host` tal cual. Si el teléfono no resuelve `localtest.me`, porque su DNS filtra las direcciones privadas, se prueba con los datos móviles o con el DNS privado de Android apagado. El certificado de desarrollo no cubre `localtest.me`: Chrome avisa una vez por host, y se sigue con «Configuración avanzada». La primera prueba confirma que el ingreso anda así; si no, se anota en el plan de la etapa, sin cambiar la configuración para compensarlo. Con iPhone no hay reenvío desde Windows.

## Requisitos previos

La E19 cerrada.

## En el multitenant

- **Datos de la organización:** las tablas de la E14, con RLS. Cada ruta nueva lleva su test de aislamiento, y lo propio, el suyo: otra persona de la misma organización recibe 404.
- **Idioma:** el contenido no tiene idioma (35), así que `PresentationCulture` de allá no se toma.
- **Datos de las personas:** los de esa persona en esa organización (42).
- **Feature `forms`:** las rutas la llevan.
- **Permisos:** `formrecords.create`, sin permisos nuevos. `formrecords.create` y `forms.publish` completan su texto.
- **Almacenamiento:** los archivos van en `IFileStore`, como los de la E18.
- **Cuotas:** las rutas cuentan en las cuotas generales.
