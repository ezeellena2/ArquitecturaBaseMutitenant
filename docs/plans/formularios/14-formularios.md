# E14 · Formularios: crear, publicar, mandar y recibir (spec)

**Estado:** pendiente · spec de la etapa 14 del bloque (decisiones 34 y 78), con las decisiones 35, 38, 43, 79, 80, 103, 111, 112, 121, 129, 145, 165, 175, 176 y 186. Nace con la pregunta «Elegir de un catálogo», sobre los catálogos de la E12 y sus campos de la E13. `/plan-etapa` la convierte en plan cuando le toque, después de verificar los puntos marcados **[E11]**, **[E12]**, **[E9]** y **[Meta]** contra el código real y la documentación vigente.

**Fuentes:**
- alcance y decisiones de producto: [hoja de ruta](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md), §E14 y §5;
- reglas: [AGENTS.md](../../../AGENTS.md) y las [fichas](../../rules/README.md);
- conocimiento de Meta: ArquitecturaBase ([§14](14-parte-archivos-tests-y-meta.md#14-referencia-de-meta-tomada-de-arquitecturabase)). Es solo referencia, no código para copiar.

**Requisitos:** la E13 cerrada (con ella, el módulo `Forms`, los catálogos y sus campos), la E11 (números por organización, bandejas, token por organización) y la E9 (Redis, límites y fuzz). La prueba con Meta pide una organización con su número conectado, y queda pendiente hasta la I1 de la 10 (123).

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros, con los mismos números de sección. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| §2, §6 y §6b | [decisiones técnicas y flujos](14-parte-decisiones-y-flujos.md) |
| §3, §4, §5 y §7 | [datos y contrato](14-parte-datos-y-contrato.md) |
| §8, §10, §11, §12, §14 y §16 | [archivos, tests y Meta](14-parte-archivos-tests-y-meta.md) |

## Qué leer

Antes de empezar:
1. **Lo anterior:** las specs de la [E12](12-catalogos.md) y la [E13](13-campos.md), sobre todo sus datos, su contrato y sus archivos.
2. **Decisiones** de la hoja de ruta: las de la §13 de esta spec.
3. **Fichas:** [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [multitenancy](../../rules/multitenancy.md), [persistencia-ef](../../rules/persistencia-ef.md), [auditoria](../../rules/auditoria.md), [api-http](../../rules/api-http.md), [permisos](../../rules/permisos.md), [paginado-y-busqueda](../../rules/paginado-y-busqueda.md), [result-y-errores](../../rules/result-y-errores.md), [validacion](../../rules/validacion.md), [textos-libres](../../rules/textos-libres.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [numeros-y-moneda](../../rules/numeros-y-moneda.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [datos-personales](../../rules/datos-personales.md), [logs](../../rules/logs.md), [cuotas](../../rules/cuotas.md), [modulos](../../rules/modulos.md) y [tests](../../rules/tests.md). Además, cómo caen en este módulo: [§11](14-parte-archivos-tests-y-meta.md#11-premisas-aplicadas).
4. **Código para copiar:** el módulo Asistente y lo que deje la E11, pieza por pieza, en la §8.
5. **Pantallas:** los tableros de la §9.
6. **Referencia de Meta:** la §14.

## 1. Alcance

**Entra:**
- **«Formularios»** (79): una sola lista, con «Nuevo formulario». Cada formulario se abre con sus pestañas, y esta etapa trae «Diseño»; lo que ve cada persona depende de sus permisos.
- **Editor:** formularios de la organización con siete tipos de pregunta, entre ellos «elegir varias» (38), títulos de sección entre las preguntas (88), prefijo de numeración y vista previa en el teléfono. El contenido es un solo texto por campo, tal como lo escribe la persona, en el idioma que quiera (35).
- **Publicación en WhatsApp:**
  - un Flow por versión y por cuenta de WhatsApp Business (WABA);
  - operación durable: su estado lo dice la etiqueta del encabezado, sin pasos (175), y la vista oficial de Meta se recorre en un diálogo (176).
- **Envío desde el chat:** «Enviar formulario» en un chat de Internos (a un empleado) o de Clientes (a un cliente), con la sesión de 24 horas. Cada formulario dice para quién es: solo empleados, solo clientes, o los dos (49 y 50). Sin texto del operador, el mensaje dice «Completá el formulario «Visita a cliente».» (165). Sale por la cola de WhatsApp y, si no se sabe si salió, se reintenta solo, como los demás (80).
- **Recepción:**
  - el `nfm_reply` se convierte en una respuesta numerada (`VIS-000123`), sin duplicados;
  - en el chat aparece una tarjeta con los valores, o solo con el número si quien mira no puede ver respuestas.
- **La pregunta «Elegir de un catálogo»** (78): «elegir una» y «elegir varias» toman sus opciones de la pregunta o de un catálogo de la E12 (a mano, de los miembros o de los contactos). Ver la §6b.
  - **la presentación:** la pregunta usa la del catálogo (E13) o tiene la suya, que se publica con su versión;
  - **al publicar:** un catálogo a mano con más de 200 opciones activas en «elegir una», o más de 20 en «elegir varias», no se publica, porque nunca se podría mandar;
  - **al mandar:** las opciones se arman para cada destinatario y viajan dentro del mensaje. No se puede mandar con más de 200 opciones en «elegir una», con más de 20 en «elegir varias», ni sin ninguna si la pregunta es obligatoria;
  - **al responder:** la opción tiene que estar en la oferta, y se sellan el texto, el detalle, el código, el valor y los campos que vio la persona. Si era una opción a mano dada de baja, no se guarda nada, el operador ve el motivo y puede mandar de nuevo;
  - **en los catálogos:** «Lo usan» en el listado y en el catálogo, sin la cantidad en el encabezado de la sección. Dado de baja, un catálogo no se elige en preguntas nuevas, y los formularios publicados que lo usan no se pueden mandar: la confirmación de la baja lo advierte. Un campo que usa la presentación propia de una pregunta no se quita: el diálogo dice qué formulario y qué pregunta, con «Ir a la pregunta». Una opción que eligió una respuesta no se borra, y se puede dar de baja (129).
- **El módulo** (nace en la E12):
  - los permisos de formularios;
  - los cuatro puertos neutrales de los formularios, con sus implementaciones apagadas;
  - el retiro probado una vez, con los catálogos de la E12 y la E13.

**No entra:**
- dar de baja, reactivar, borrar y deprecar los Flows (E17);
- los datos de las personas y los filtros por destinatario (E16);
- fotos y documentos (E18);
- las respuestas: pestañas «Respuestas» y «Tablero», planilla, detalle y Excel (E15);
- reglas, aprobación, PDF y avisos (E21);
- biblioteca de plantillas y «Desde una plantilla» (E22);
- el asistente (E23);
- copiar formularios (E17);
- condiciones, cálculos y líneas (E19);
- cargar desde la web (E20);
- límites por plan.

## 9. Pantallas (tableros)

**Navegación (34, 78 y 79):** el menú principal dice «Gestión de formularios», y su panel ya tiene «Catálogos» (E12). Esta etapa suma «Formularios»: una sola lista, con «Nuevo formulario», y cada formulario se abre en «Diseño», sin barra de pestañas mientras sea la única (193). No existe «Diseñar formularios». Los tableros de la etapa muestran solo lo que existe hasta ella.

**Aprobados y publicados en el lienzo v150** (2026-11-02), con sus teléfonos, sin idioma en el contenido y con los botones de la decisión 43. Reemplazan a los de la v139:
- **`FRM-Formularios`:** la lista «Formularios» (79), una tabla con Formulario, Preguntas, En WhatsApp y Último cambio (fecha y hora completas), el filtro «Estado ▾» y «Nuevo formulario». El nombre abre el formulario con su pestaña «Diseño». Reemplaza a `FRM-Listado`;
- **`FRM-Editor`:** el editor en una sola pantalla:
  - el formulario: nombre, «Para quién es» (empleados, clientes o los dos; 50), texto del botón final y prefijo, cada uno en su campo;
  - las preguntas, que se abren en el lugar, con el número, el nombre y, debajo, el tipo y si es obligatoria (130), y ↑ ↓ ✎ 🗑 a la vista (127). Las opciones de una pregunta llevan ↑ ↓ 🗑;
  - «elegir varias», con el mínimo y el máximo (en el ejemplo, «Fallas», de 1 a 3);
  - la pregunta «De un catálogo»: el combo de la E13 (111), que ofrece los tres orígenes y muestra apagados, con su motivo, los dados de baja y los que no se pueden usar; «Las opciones se preparan al mandar el formulario.» va en la (i) de «Catálogo»; «Permitir elegir varias» con mínimo y máximo, «varias, hasta N» en la lista de preguntas, un catálogo de más de 20 y la presentación de la pregunta (la del catálogo o la suya), en la zona «En WhatsApp», que arranca plegada con lo configurado en su banda y se abre sola con un error o un aviso (145);
  - «Así lo ve quien lo recibe»: el teléfono al costado, con el borrador (176).

  En el teléfono, la pregunta se edita en una hoja y los botones van fijos abajo.

  **Con los títulos de sección (88):** «Agregar título» en el editor, el título entre las preguntas con las mismas acciones y el subtítulo en el teléfono al costado, en `FRM-Editor` y `M-FRM-Editor`.
- **`FRM-Catalogos-14`:** el listado de catálogos de la E12 con la columna «Lo usan».
- **`FRM-Catalogo-14` y `M-FRM-Catalogo-14`**: el catálogo, con «Usado en 2 formularios» en la banda, la sección «Lo usan», un campo en uso (quitarlo dice dónde está, con «Ir a la pregunta», y no se quita), una opción que eligió una respuesta (no se borra y se puede dar de baja) y la advertencia al darlo de baja.
- **`Campana-14`**: «WhatsApp rechazó la versión N de …», que lleva al formulario.
- **`WA-Mensajes-14`:** «Mensajes», en el perfil personal, con la tarjeta de una respuesta propia de un cliente que vinculó su cuenta.
- **`FRM-Publicar`**: «Publicar la versión N en WhatsApp», por zonas (112 y T25), cada una en su caja y con la explicación en su (i): «Problemas», si hay alguno, con «Ir a la pregunta» en cada uno, y «Qué se publica», siempre: Borrador, Preguntas, lo agregado y lo cambiado y, la primera vez, el Prefijo con «Queda fijo al publicar».
- **Borrar una opción (129):** una que eligió una respuesta no se borra: `Forms.Catalog.ItemInUse` suma `uses: records`, y debajo de la fila se lee «No se puede borrar: la eligió una respuesta. Podés darla de baja.».
- **`FRM-Publicando`:** cómo termina, en el encabezado del editor, sin zona ni pasos (175; «Cómo termina», abajo).
- **`WA-Chats-Formulario` y `M-WA-Chats-Formulario`:** «Enviar formulario» al lado de «Enviar» en Internos (en el teléfono, en el ⋮ del chat). El diálogo «Enviar un formulario a …» tiene una tarjeta por formulario, con la versión y la cantidad de preguntas cada una en su lugar (con buscador y paginado si son muchos; el elegido de otra página va primero, marcado, 138), la cantidad de opciones por pregunta de catálogo y los problemas de la oferta, el mensaje de hasta 1024 caracteres (al elegir un formulario propone «Completá el formulario «Visita a cliente».», sin pisar lo escrito; 165) y hasta cuándo se puede mandar, con fecha y hora. Sin «Ya lo revisé» ni «Resolver el envío» (80).
- **`WA-Chats-Formulario-Cliente` y `M-WA-Chats-Formulario-Cliente`** (nuevos): «Enviar formulario» en un chat de Clientes, con solo los formularios para clientes o para los dos (49 y 50). En el ejemplo, «Entrega de pedido» es para los dos.
- **`WA-Chats-Respuesta` y su teléfono:** la tarjeta con las primeras 3 respuestas en grilla, «Ver las N respuestas» (en el teléfono, en una hoja) y «Guardada como VIS-000123»; el valor de catálogo con su código, y el rechazo de una opción dada de baja con «Mandar de nuevo».

**Sin rastro de etapas posteriores:** los tableros de la v141 no tienen «4 reglas · Revisión 3» ni «Más de 10 archivos». `Rol` suma los cinco permisos de formularios.

**Redibujados con el traspaso 5, aprobados y publicados en el lienzo v162** (138, 142 y 145): `FRM-Editor` y su teléfono, con la revisión de «otra persona guardó» lado a lado (el teléfono suma «Revisar los cambios») y la zona «En WhatsApp» plegada en cada pregunta de catálogo, y `WA-Chats-Formulario` y su teléfono, con «Elegido en otra página»: el elegido de otra página primero y la tarjeta elegida con su borde.

**Redibujados con el traspaso 10, aprobados y publicados en el lienzo v168** (165, 175 y 176): `FRM-Publicando` y `M-FRM-Editor`, con la publicación sin pasos, sin una hoja con su estado en el teléfono y sin el segmentado «Borrador | Versión N en WhatsApp»; `WA-Chats-Formulario` y `WA-Chats-Formulario-Cliente`, con sus teléfonos, con el mensaje que se propone al elegir; y `WA-Chats-Respuesta`, `M-WA-Conversacion-Respuesta`, `WA-Mensajes-14` y `M-WA-Mensaje-14`, con «Completá el formulario «Visita a cliente».» en el chat.

**Del traspaso 15** (v175, sin publicar): `FRM-Publicando` y `M-FRM-Editor`, con «Descartar esta publicación» y su confirmación (225).

**Nuevos, aprobados y publicados en el lienzo v168:** `FRM-Vista-Oficial` y `M-FRM-Vista-Oficial` (176). «Ver vista oficial», en la banda cuando hay una versión publicada, abre el diálogo «Vista oficial · versión N»: el marco de Meta, de 480 × 840, achicado entero para entrar en la ventana, sin barras ni cortes, que arranca en la primera pantalla y se recorre como en el celular. Cada lista de catálogo trae sus primeras 20 opciones ([§6, punto 5](14-parte-decisiones-y-flujos.md#6-flujos-con-sus-transacciones)). «Abrir en otra pestaña» abre la misma vista. En el teléfono ocupa la pantalla, también a 320.

**Estados que se dibujan** (cada uno tiene que poder producirse; si no, es un bug):
- **Editor:** con cambios, guardando («Guardando…», con su aro), guardado, no se guardó (lo escrito se conserva), otra persona guardó (lado a lado, 138: Qué cambió, Dato, Guardado actualmente y Mis cambios, solo lo distinto y con los nombres del editor; una pregunta que una versión no tiene muestra sus datos y «Quitada»), prefijo ocupado, para quién es, quitar una pregunta, cambiar el tipo, descartar, solo lectura, cargando y no existe.
- **Publicar:** versión nueva, primera publicación, comprobando (un esqueleto en «Qué se publica»), con cambios sin guardar (publicar grisado, con el motivo en su tooltip), una pregunta que no se puede mostrar, un texto más largo que su límite, un catálogo demasiado grande, cambió después de comprobar, ya se está publicando, sin número conectado, sin cambios, con una cuenta conectada que no tiene el formulario (103), y un título sin preguntas debajo, con «Ir al título» (T24).
- **Cómo termina** (175): sin zona ni pasos. La etiqueta del encabezado dice el estado de la última publicación: «Publicando la versión 4», «Publicado · versión 4», «Sin confirmar · versión 4», «Necesita revisión · versión 4» o «No se pudo publicar la versión 4». Mientras se publica, la pantalla vuelve a leer sola. La banda suma, solo cuando hacen falta:
  - «Consultar publicación», sin confirmar, en revisión o si la lectura sola se cortó: dice «Consultando…» con su aro, nunca «Reintentar»;
  - «Repetir el mismo intento», si el pedido de publicar no tuvo respuesta y al volver a leer no aparece («Si no se sabe si algo salió», abajo);
  - «Salió» y «No salió», solo con «Necesita revisión». Cada uno pide confirmar con el número, «¿La versión 4 salió en WhatsApp?» o «¿La versión 4 no salió en WhatsApp?», con el nombre del Flow, la principal y el foco en «Cancelar» (186);
  - «Descartar esta publicación», solo con «Sin confirmar» (225): pide confirmar, «¿Descartar la publicación de la versión 4?», con el foco en «Cancelar», y la cierra como «No se pudo publicar la versión 4»; sus Flows quedan sin usar. Quien publica mientras sigue abierta recibe cuál es, y el editor muestra su estado con esta salida. El «Descartar» de la banda sigue siendo el de los cambios del borrador.

  «Publicar en WhatsApp» apagado dice su motivo en el tooltip: una publicación en curso, sin confirmar o en revisión, un pedido sin respuesta, cambios sin guardar, sin número conectado o sin permiso. Lo rechazado queda marcado en su pregunta, sin una zona «Problemas»; un rechazo de la cuenta, que no es de ninguna pregunta (`AccountUnavailable`), va en el aviso importante, con su ⓘ. «Publicamos la versión 4.» y «No pudimos publicar la versión 4.» se avisan solo en el editor donde empezó la publicación. En el teléfono, el estado no va en una hoja: las acciones van en el pie fijo y el motivo de lo apagado, en un tooltip sobre el botón, como en la compu (211). Son hojas la ⓘ de «Necesita revisión» y las confirmaciones de «Salió» y «No salió».
- **Vista oficial** (176): cargando, lista, vencida («Cargarla de nuevo») y con error, con la medida del marco; sin versión publicada no hay «Ver vista oficial».
- **Enviar:**
  - En cola, Leído, y No se pudo mandar (con ⓘ, sin reintento);
  - con el diálogo abierto, ventana cerrada, formulario que ya no está y formulario que no está publicado para este número (103).
- **Al mandar una pregunta de catálogo:** la cantidad de opciones por pregunta; en rojo y con el envío grisado: más de 200 (o 20 en varias), menos que el mínimo, ninguna en una pregunta obligatoria, un catálogo que no se puede usar o una opción que no entra.
- **Respuesta:**
  - guardando, no se pudo guardar, no reconocida y sin permiso;
  - el mensaje ya no está;
  - con «elegir varias»;
  - una opción dada de baja no se guarda y ofrece «Mandar de nuevo»;
  - venció sin respuesta.

**Mandan sobre esta spec en:**
- **Las fichas del front** (pedido del usuario del 2026-10-25): un dato por columna (206); las fechas completas, con la hora después de una coma (205) y en la zona de quien mira, nunca «hoy», «ayer» ni «hace…»; sin leyendas (109), así que la vista previa es el teléfono al costado; y «Dados de baja», cuando llegue, es una opción del filtro «Estado».
- **Valen las de toda la app** (160 a 164, 173 a 175 y 180): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez. Llegan con las piezas de la base de la 12.
- **Textos:** la lista se llama «Formularios», la pestaña del editor «Diseño», y la acción del chat es «Enviar formulario» (79).
- **Botones (43 y 115):** «Guardar cambios» se habilita cuando hay cambios, sin «Cambios sin guardar». Mientras trabaja, dice «Guardando…» con su aro. Si falta un dato obligatorio, el botón sigue habilitado y al tocarlo se marca el campo.
- **Si no se sabe si algo salió (43):** ante un corte de red al guardar, al mandar o al publicar, la pantalla vuelve a leer sola y muestra lo que quedó, sin pisar lo escrito:
  - **al guardar:** relee el borrador y compara **solo el contenido**, sin las identidades que emite el servidor. Si es igual a lo mandado, quedó guardado y el editor toma la versión y las identidades de esa relectura; si no, Guardar sigue habilitado con lo escrito y la versión de antes. Nunca reintenta solo. Tomar la versión de una relectura es una excepción a [concurrencia](../../rules/concurrencia.md), que se escribe en la ficha de formularios del front;
  - **al mandar:** relee el chat. Si la sesión existe, se ve el envío; si no, se puede mandar de nuevo con la misma `Idempotency-Key`, que devuelve el resultado guardado si el primero sí llegó;
  - **al publicar** (175): relee el formulario. Si aparece la publicación, la etiqueta dice su estado; si no, la banda trae «Repetir el mismo intento», que manda el mismo pedido, con la misma `Idempotency-Key` y el mismo `contentHash`: si el primero llegó, devuelve su resultado, y si está en curso, espera ([idempotencia](../../rules/idempotencia.md)). Mientras tanto, «Publicar en WhatsApp» queda apagado, para no empezar otro intento con otra clave.
- **Envío pendiente:** un envío pendiente bloquea el chat (`WhatsApp.Chat.SendPending`). No bloquea que haya otro formulario sin responder.
- **Un envío que no se sabe si salió** lo reintenta la cola sola, como a los demás mensajes; no hay «sin confirmar» ni «Ya lo revisé» (80).
- **«Cambió después de comprobar»** es `Forms.Publication.DraftChanged` (T8).
- **«Necesita revisión»:** `FormPublication` tiene el estado `NeedsReview`. Llega cuando la consulta del canal no puede decidir (`RequiresReview`, [§6, punto 4](14-parte-decisiones-y-flujos.md#6-flujos-con-sus-transacciones)): Meta no confirmó la creación de un Flow y no hay id que consultar (un Create sin id no se adopta por nombre), o el Flow no coincide con lo publicado. Una persona con `forms.publish` lo busca en Meta por el nombre del Flow, que trae el formulario, y lo resuelve con «Salió» o «No salió», que van solo acá (175).
- **«No reconocida»:** una respuesta de Flow que no corresponde a ninguna sesión se muestra en el chat como un mensaje sin valores y no crea nada en Forms.
- **La banda de conflicto** («Otra persona cambió esto mientras lo editabas.») lleva ⚠.
- **«Venció sin respuesta»:** se calcula al leer: la sesión sigue `Pending` y ya pasó `ExpiresAtUtc`. `Expired` queda para una respuesta que llegó tarde.
- **El código de la opción de un catálogo se ve en la tarjeta de la respuesta,** debajo del valor (nunca en WhatsApp).
- **Cualquier catálogo de la organización** sirve en cualquier formulario: el contenido no tiene idioma (35).

## 13. Decisiones de producto de la etapa

Están en la §5 de la hoja de ruta y no se vuelven a preguntar: 86 (la unidad va en la etiqueta, sin un dato «Unidad»), 88 (títulos de sección), 9 (la respuesta guarda todas las empresas activas de quien respondió), 10 (manda cualquiera con «Atender internos», y a un cliente, con «Atender clientes»), 11, 21 y 36 (un nombre de más de 30 caracteres se corta en 29 con «…»), 12, 22 y 37 (el tope de 200 frena al mandar, no al guardar), 49 y 50 (los clientes completan los formularios que son para clientes), 35 (el contenido no tiene idioma), 38 («elegir varias» va acá), 43 (botones y avisos), 78 (la E14 nace con la pregunta «Elegir de un catálogo»), 79 (una sola lista «Formularios», con la pestaña «Diseño»), 80 (lo que sale por la cola de WhatsApp se reintenta solo, sin «sin confirmar»), 103 (un número en otra cuenta habilita publicar), 109, 110, 115 y 116 (leyendas, motivos, esperas y ayuda), 111 (el combo del catálogo), 112 (publicar por zonas), 121 (el límite se avisa al publicar), 129 (una opción que eligió una respuesta no se borra), 145 (la zona «En WhatsApp» arranca plegada), 165 (el mensaje nombra el formulario, y el nombre del Flow), 175 (el estado de la publicación, una vez y sin pasos), 176 (la vista oficial, en un diálogo que se recorre), 186 («Salió» y «No salió» piden confirmar) y 225 (descartar una publicación sin confirmar).

## 15. Cómo se cierra

La puerta de la etapa ([etapa en paralelo](../../guides/etapa-en-paralelo.md)) tiene las suites, el E2E real, la documentación en 0, la galería, el recorrido y la lista de control de `/puerta`. Además, hay una **prueba real con Meta** con el número de una organización de prueba, abriendo cada variante en un celular. Lo que allá ya se probó con Meta, acá se prueba igual:
1. **Formulario de prueba:** con los siete tipos de pregunta, entre ellos «elegir varias» con mínimo 1 y máximo 3, y con un título de sección, que se ve como subtítulo (`TextSubheading`, que allá Meta aceptó el 2026-11-06). Meta confirma `PUBLISHED`, con un solo Flow, que en el administrador de WhatsApp se llama «Visita a cliente · v1 · ab63a1f9» (165).
2. **Mandar a una cuenta en inglés:** el formulario llega como está escrito, y el mensaje sin texto del operador («Complete the form “Visita a cliente”.»), el botón, la ayuda de los números y «Elegí hasta N» llegan en inglés. Se abre en Android y en iPhone.
3. **Completar:** dejar vacía una opcional, poner `12.50` en el número y marcar dos opciones. En el chat aparece el número de la respuesta tras recargar, y las dos opciones quedan en su orden.
4. **Mirar lo que llega de Meta:** qué manda para una opcional vacía y para «elegir varias» sin marcar, y que `12.50` y `12,50` se guardan iguales.
5. **Una WABA sin medio de pago** (si se puede probar): queda `AccountUnavailable` visible, no «Sin confirmar» para siempre.
6. **Un catálogo a mano,** con y sin detalle, en un formulario publicado. Se manda, se responde, y la respuesta guarda el texto, el detalle, el código y el catálogo; se ven igual después de recargar.
7. **«Elegir varias» con un catálogo:** mínimo 1 y máximo 3; se marcan dos y quedan en su orden.
8. **Miembros y contactos:** un catálogo de miembros con filtros, vacío y con opciones, con «Sin incluir a quien lo recibe», y uno de los contactos para elegir a un cliente.
9. **Opción dada de baja después de mandar:** la respuesta se rechaza sin número. «Mandar de nuevo» da el número siguiente, sin saltos.
10. **El tope:** un catálogo de 200 opciones con detalle viaja y vuelve con los ids intactos.
11. **La presentación y el sellado:** «Proveedores» con el CUIT en la descripción (la del catálogo) y una pregunta con su presentación propia se ven así en el celular; al responder, los campos quedan sellados, y cambiar el CUIT en el catálogo no cambia lo respondido.
12. **Lo que allá anduvo:** un `Dropdown` con datos y sin endpoint (el de cada pregunta de catálogo, T21), y un mensaje de unos 35 KB con 331 opciones, en preguntas de catálogo de hasta 200 cada una. Allá salió uno de 34,7 KB y abrió en el celular.
13. **Lo que no se verificó en Meta:** el `flow_token` de 128, el cuerpo de 1024 y mandar un formulario publicado por su `flow_name`, en lugar de su `flow_id` ([§6, punto 4](14-parte-decisiones-y-flujos.md#6-flujos-con-sus-transacciones)).
14. **«Mensajes»:** un cliente que vinculó su cuenta ve la tarjeta de su respuesta en su perfil personal.
15. **La vista oficial** (176): «Ver vista oficial» abre la versión publicada en su diálogo, en su pantalla, y cada lista de catálogo trae sus primeras 20 opciones. Una opción con `%` («2 4 D 100%») se ve bien, y el marco carga con la política de seguridad de la Api: allá, sin `frame-src`, solo se veía con el servidor de desarrollo.

