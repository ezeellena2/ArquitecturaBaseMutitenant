# E21 · Reglas, aprobación, PDF y avisos (spec)

**Estado:** pendiente · spec de la etapa 21 del bloque (decisiones 34, 78, 82, 91 a 93, 100, 106, 113, 114, 158, 159, 161, 166, 170, 177, 228, 230, 236, 237, 239, 240, 261 a 264, 266, 267, 275 y 276; la 240 cambia la 80, y la 266, la 267 y la 276, la 25, la 139, la 166 y la 209), con el [formato](plantilla.md) de las etapas. Corresponde a la etapa 4 de ArquitecturaBase, con piloto real; a la etapa 3 de su carga desde la app, corregir y anular, programada allá el 2026-11-05, y a las etapas 3 y 4 de sus renglones: el PDF con el bloque, programado allá el 2026-11-07, y las reglas con el bloque, probadas allá de punta a punta con Meta el 9/11 (T12). «Por resolver», el aviso a quien cargó y lo demás del motor son de allá, del 15 al 17/11 (T14).

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros tres, con las mismas secciones. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| «Adjuntar el PDF», el bloque en las reglas, el motor de acciones, los avisos y los correos, el chat en «Mi cuenta», Contrato y Datos | [datos y contrato](21-parte-datos-y-contrato.md) |
| «Corregir y anular» | [corregir y anular](21-parte-corregir-y-anular.md) |
| La pregunta «Correo», Pasos, el detalle de «Acciones del asistente», «Qué verificar al arrancar» y «Lo que ya se pagó allá» | [pasos y qué verificar](21-parte-pasos-y-verificar.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T4, T5, T7 y la §6, punto 7; la [E15](15-respuestas.md): lista, planilla, detalle y actividad (266 y 267); la [E17](17-ciclo-de-vida.md): copiar y borrar; la [E18](18-fotos-y-documentos.md): archivos; la [E19](19-condiciones-calculos-y-lineas.md): condiciones y el bloque, y la [E20](20-cargar-desde-la-web.md): la captura, la forma editable y lo propio.
2. **Decisiones** de la hoja de ruta: 25, 35, 42, 43, 47, 49, 55, 59 a 62, 82, 85, 89, 91 a 93, 100, 106, 109, 113 a 116, 138 a 140, 146, 148, 149, 158, 159, 161, 162, 166, 170, 177, 206, 209, 226, 228 a 230, 236, 237, 239, 240, 257, 260 a 264, 266 a 270, 273, 275 y 276. Y las ADR [0040](../../decisions/0040-para-resolver.md), [0063](../../decisions/0063-topes-de-correo-de-la-plataforma.md), [0066](../../decisions/0066-lo-que-no-se-sabe-si-salio.md) y [0068](../../decisions/0068-acciones-del-asistente.md), con su [receta](../../guides/agregar-una-accion-al-asistente.md).
3. **Fichas:** [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [modulos](../../rules/modulos.md), [multitenancy](../../rules/multitenancy.md), [permisos](../../rules/permisos.md), [persistencia-ef](../../rules/persistencia-ef.md), [auditoria](../../rules/auditoria.md), [emails](../../rules/emails.md), [textos-libres](../../rules/textos-libres.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [logs](../../rules/logs.md), [api-http](../../rules/api-http.md) y [tests](../../rules/tests.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](21-parte-pasos-y-verificar.md#pasos).
5. **Referencia de producto,** solo para leer: en el traspaso del 17/10, retirado el 2026-11-08 (`git show e9f1a599:docs/plans/2026-10-17-formularios-traspaso/`), el anexo C, «Etapa 4», el anexo B, «Etapa 4», y el anexo D (G1 a G6); en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §5 y §6 (L-05 a L-08); en ArquitecturaBase, `docs/features/formularios.md`, «Motor de acciones y aprobación», con «Acciones por resolver», «Corregir y anular» y, en «El bloque de renglones en la lectura», el PDF y las reglas, y el traspaso 12 (`docs/plans/2026-11-11-traspaso-multitenant-renglones.md`); y en `ArquitecturaBaseFront`, `docs/design/carga-desde-la-app.md`, «Corregir y anular», y `renglones-en-formularios.md`, RG5-A.

## Qué se construye

**Reglas** «Cuando llega una respuesta»:
- cada guardado crea una revisión inmutable, con 409. No crea versión ni republica, y guardar una lista vacía las apaga. Comprueba que cada opción que nombra una condición siga en la versión (`Forms.Rules.OptionUnavailable`, que nombra la opción y marca su condición), y una regla cuyo aviso cita preguntas, adjunta el PDF o manda el PDF a un cliente pide también «Ver respuestas»: sin él, 403, con el texto del servidor en el editor (239);
- **cada regla:** un evento (Recibida, Aprobada o Rechazada), hasta 10 condiciones unidas con «y», con la gramática de la E15, y hasta 10 acciones en orden. Hasta 20 reglas. Sobre una «Ubicación» valen solo las de presencia (`answered`, `unanswered` y `notAsked`, 230);
- **las acciones:** pedir aprobación a un rol, armar el PDF, mandarle un mensaje a quien la cargó, avisar a un rol, avisar a una persona (62), mandar por correo (62) y mandar el PDF a un cliente (47);
- **lo que se elige, en la pieza** (262): el evento, cada parte de una condición, el tipo de una acción, el rol, la persona y la plantilla;
- **el mensaje con datos** (149): «Agregar dato» abre la lista de la pieza, con el buscador adentro, con el número, el motivo del rechazo al rechazar y las preguntas, sin fotos ni documentos, y el dato entra como una etiqueta que se borra entera. «Así llega» muestra el mensaje con datos de ejemplo. Se guarda `{FieldId}`, nunca el nombre, y una pregunta que ya no está se ve «Pregunta no disponible» y no deja guardar (`Forms.Rules.PlaceholderInvalid`). Es un solo texto (35). Una «Ubicación» va con el punto y, en el renglón siguiente, el enlace al mapa (230). Una condición sobre una pregunta que no existía o no se mostró cuenta como «sin respuesta»;
- **el bloque** (146 y 177): en una condición, «Algún ítem con…», y en el mensaje, un solo dato con todos sus ítems, como dice la [parte](21-parte-datos-y-contrato.md#el-bloque-en-las-reglas-146-y-177);
- **el orden:** pedir aprobación es la última acción de su regla, y solo al recibir. **El PDF** (106): si alguna regla pide aprobación, se arma solo al aprobar, y lo que espera aprobación no tiene PDF; si no, se arma al recibir. Adjuntarlo exige una acción anterior que lo arme en la misma regla;
- cada acción se ordena con ↑ ↓ y se quita con 🗑, a la vista, como en ArquitecturaBase (135);
- **cómo se lee una regla guardada** (138): en pasos, cada uno con su ícono: el cuándo (una flecha al recibir, una tilde al aprobar, un círculo tachado al rechazar); «Si», con una condición por renglón, la pregunta y el valor en negrita, y cada acción, con su dato aparte en una etiqueta (el rol, «con el PDF») y el mensaje entero en su recuadro;
- **el historial** (140): «Historial» abre un panel al costado con las revisiones, de la más nueva a la más vieja, y cada una se lee en pasos. Sin revisiones queda apagado, con «Todavía no hay revisiones.» en su tooltip.

**«Adjuntar el PDF»** (113 y 162): las acciones que mandan un mensaje o un correo llevan esa casilla, y «Mandar el PDF a un cliente» lo manda siempre. Nada se desmarca solo, y si «Armar el PDF» falta o está después, la regla lo dice y lo arregla, como dice la [parte](21-parte-datos-y-contrato.md#adjuntar-el-pdf-113-y-162).

**Un solo motor de acciones,** con ejecuciones idempotentes y el efecto fuera de la transacción: está en la [parte de datos y contrato](21-parte-datos-y-contrato.md#el-motor-de-acciones).

**Aprobación de un nivel** (60):
- decide una cuenta activa con `formapprovals.decide` y el rol de la regla; quien cargó la respuesta o la corrigió, solo si la regla lo deja (25 y 276);
- una sola tarea abierta por respuesta. Decidir es un solo commit; si otro decidió antes, 409. Rechazar lleva un motivo de hasta 500 caracteres, obligatorio salvo que la regla no lo exija (276);
- **«Pedir aprobación», con dos casillas** (276): debajo del rol, «Puede decidir quien cargó», sin marcar, y «Exigir motivo al rechazar», marcada, cada una con su (i); por defecto queda como hasta ahora. Se guardan con la revisión de las reglas, y cada respuesta las sella al llegar: lo que ya espera aprobación sigue con lo que tenía. Con la primera, quien cargó la respuesta y quien la corrigió también la deciden, la ven en «Por aprobar» y en sus conteos, y cuentan como personas del rol para pedir aprobación (239). Sin la segunda, rechazar sin motivo vale, y su renglón de la actividad dice «Sin motivo». Un motivo que falta cuando se exige es 400 `Forms.Approval.ReasonRequired`;
- **leer y decidir son independientes:** con `formapprovals.decide` y sin `formrecords.read`, se ve la tarea en la vista rápida, sin valores ni pantalla completa (100). Su cola es la vista «Por aprobar» (148): el número, la fecha y el rol;
- **«Aprobar» pide confirmar con el número** (114 y 161): un diálogo como el de rechazar, «¿Aprobar VIS-000003?», con quién la cargó y cuándo; quien solo aprueba ve solo cuándo (100). «Aprobar» es la principal, sin rojo, y el foco empieza en «Cancelar», así Enter no aprueba; mientras manda, espera abierto con «Aprobando…» (115);
- **sin el rol de la tarea:** quien tiene el permiso y no el rol, como una cuenta administradora, ve el detalle sin error. `approval.canDecide` dice si puede decidir, y el detalle pide la tarea solo entonces; si no, la vista rápida muestra la decisión de solo lectura (el rol, quién decidió y cuándo), y la pantalla completa no reserva la barra de decisión.

**«Por aprobar» en «Formularios»,** con la misma fuente que «Para resolver» (ADR 0040):
- **la lista** (275) suma la columna «Por aprobar», solo para quien aprueba, cuyo número abre la planilla en «Por aprobar», y el filtro «Estado» suma «Con algo por aprobar». La columna ordena (264), y es el orden con que la lista le abre a quien aprueba, de mayor a menor. Cuenta solo lo que esa cuenta puede decidir, sin lo propio salvo con «Puede decidir quien cargó» (276). Sin `formapprovals.decide`, ese filtro y ese orden dan 403. El filtro trae los formularios activos y los dados de baja, y un solo predicado cuenta para la lista, `GET /overview`, «Para resolver» y la campana, así los números no se separan (como allá);
- con solo `formapprovals.decide`, ve los formularios con tareas, sin respuestas ni última respuesta;
- **la vista «Por aprobar»** (148): arriba de la planilla, quien puede decidir ve «Por aprobar (N) | Todas». «Por aprobar» es una tabla corta (N°, Cargó, «Es cliente» como en la E15, Recibida y Rol), de la más vieja a la más nueva, sin filtros ni valores, y no ordena por columnas, porque es una cola (264). «Revisar», en el ⋮ de la fila (261), abre la vista rápida; ↑ ↓ recorren esa cola, y la vista rápida y la pantalla completa la cuentan igual, «1 de 2 por aprobar». Se abre ahí si hay algo para decidir, y la vista va en la dirección. Quien solo aprueba ve solo esta tabla, sin Cargó ni «Es cliente», sin el total de respuestas y sin las pestañas que su permiso no abre (79, 100 y 148). La campana y «Para resolver» abren la más vieja.

**«Por resolver»** (228), la tercera vista del segmentado (79 y 148): una respuesta está por resolver cuando una acción de sus reglas quedó «No se pudo» sin reintentarse sola, o en duda o «Sin confirmar» esperando a una persona (240); cuenta también el aviso fuera de las 24 horas sin plantilla (61). No cuenta lo que la app todavía consulta sola, lo que espera, lo que terminó ni lo que no aplica:
- quien tiene «Administrar reglas y PDF» y ve todas las respuestas tiene «Por aprobar (N) | Por resolver (N) | Todas», sin «Por aprobar» si no decide, con la misma planilla y el filtro puesto. La vista aparece con la cuenta mayor que cero o ya elegida, y nunca es con la que abre la planilla. Cuenta respuestas, no acciones, sobre las de «Todas» y sin los filtros de la barra;
- **la columna «Reglas»,** después de «Estado», dice «Por resolver» o «—», y ordena (264), para todo el que ve la planilla, también en lo propio, y sale cuando alguna fila de la página la tiene o en la vista;
- el Excel pedido desde la vista exporta lo que se ve, y su diálogo lo dice con el chip «Por resolver» en «Filtros» (226). Resolver algo desde el detalle relee la planilla y su cuenta;
- sigue hasta anular o corregir la respuesta, y la corrección arranca sin marca. No suma a «Para resolver» ni a la campana.

**La planilla** suma la columna «Estado», en los formularios que piden aprobación, que ordena por el circuito (264), y la marca de la copia PDF confirmada. El filtro «Estado», en la pieza (262), va en todos: con aprobación, Recibida, Por aprobar, Aprobada y Rechazada, y siempre, «Anulada». Lo comparten la lista, los conteos y el Excel, que lleva «Estado» entre sus columnas fijas, después de «Canal», en todos los formularios. El tablero suma «Por estado».

**La actividad de una respuesta** (267, que cambia la 139): la tabla de la E15, un renglón por cada cosa que pasó, suma los de esta etapa en sus bandas («Al recibirla», «Aprobación», «Al aprobarla» o «Al rechazarla»), con lo que se hace con cada uno en su ⋮ (261):
- la aprobación y el rechazo, con quién y el motivo entero en «Detalle», o «Sin motivo» (276);
- cada acción de las reglas, con su estado, y cada PDF, con «Ver PDF» y «Bajar PDF»;
- cada persona de un aviso, con su estado («Entregado», «Leído», «No se pudo», «Sin confirmar» o «No aplica» si dejó el rol, 239) y, si el aviso fue a un rol, el nombre actual del rol en «Detalle»;
- «Reintentar», en una persona solo con la falla confirmada (WhatsApp lo rechazó o no lo pudo mandar después de sus dos reintentos), y en una acción, como dice la [parte](21-parte-datos-y-contrato.md#el-motor-de-acciones). «No se pudo» lleva su (i): «Reintentar» manda el aviso solo a esa persona, y antes hay que revisar el WhatsApp vinculado y la ventana de 24 horas. Sin permiso de reglas se ve la falla, sin «Reintentar» (158). Cada pedido de reintento es su renglón, con quién y cuándo (239);
- **lo que no se sabe si salió queda «Sin confirmar»** (240): no se repite solo; lo revisa en su bandeja cualquiera que puede enviar en ella, y el ⋮ de su renglón lleva ahí. La revisión es su renglón, con quién y cuándo; revisar no reenvía ni dice que llegó;
- la corrección y la anulación, con su motivo, como dice su [parte](21-parte-corregir-y-anular.md).

**Corregir y anular** (91 a 93, 166 y 266, como en ArquitecturaBase): una respuesta confirmada no se modifica: queda reemplazada por su corrección, con el mismo número, o anulada, sin borrarse. Se corrige en el lugar: «Corregir», a la vista, destraba los mismos controles en la misma pantalla. Está en su [parte](21-parte-corregir-y-anular.md).

**El PDF** (59, 82, 106 y 236): un diseño con revisiones propias y, al armarlo, una copia fija A4 por idioma del sistema, que no cambia; cada dato de la opción de un catálogo va en su renglón y, en el bloque, en su columna (263). Está en la [parte de datos y contrato](21-parte-datos-y-contrato.md#el-pdf-59-82-106-236-y-263).

**Los avisos por WhatsApp y los correos** (61 y 62), con el aviso a quien cargó cuando su regla se frena (237), están en la [parte de datos y contrato](21-parte-datos-y-contrato.md#los-avisos-y-los-correos).

**La pregunta «Correo»,** un tipo nuevo, está en la [parte de pasos](21-parte-pasos-y-verificar.md#la-pregunta-correo-141-y-196).

**Lo propio** (85 y 166): la planilla de quien solo carga suma el estado, el PDF y la columna «Reglas» (228). Su actividad suma solo la decisión, sin el rol ni quién decidió y con el motivo del rechazo en «Detalle», no en una columna de la planilla, la corrección y la anulación; las acciones de las reglas, no. «Corregir» va a la vista, y «Anular», en el ⋮, mientras pueda (266).

**El chat de WhatsApp en «Mi cuenta»** (159 y 170) está en la [parte de datos y contrato](21-parte-datos-y-contrato.md#el-chat-de-whatsapp-en-mi-cuenta-159-y-170).

**Copiar un formulario** suma el diseño del PDF y las reglas vigentes, como su primera revisión.

## Pantallas

Aprobados en el lienzo v175, con el armado del front real de ArquitecturaBase (126 y 135) y los recorridos guiados `armar-las-reglas` y `aprobar-o-rechazar`. El ejemplo es «Visita a cliente», versión 4, que pide aprobación al rol Supervisor, con tres por aprobar; las reglas con el bloque usan «Pedido de materiales». Muestran solo lo que existe hasta acá (78). Lo que falta dice «Se redibujan», con su decisión.

| Tablero | Qué muestra | Estado |
| --- | --- | --- |
| `FRM-Formularios-21` y su teléfono | la lista de la 20, con «Cargar», «Por aprobar» y «Quien solo aprueba» | Se redibujan: «Estado» y «Dónde se completa» en lugar de «En WhatsApp» (229); «Por aprobar» solo para quien aprueba y «Con algo por aprobar» en «Estado» (275); «Cargar» en el ⋮ (261) y el encabezado que ordena (264) |
| `FRM-Editor-Reglas`, `M-FRM-Reglas` | las reglas, como una pestaña más, con su historial, las siete acciones, «Adjuntar el PDF» (113) y la plantilla para fuera de las 24 horas | Redibujados en la v175 con el 403 de «Ver respuestas» (239). Se redibujan: una opción que ya no está, nombrada y con su condición marcada (239), lo que se elige, en la pieza (262), y «Pedir aprobación» con sus dos casillas (276) |
| `FRM-Editor-Reglas-Bloque` y `M-FRM-Reglas-Bloque` | las reglas de «Pedido de materiales»: «Algún ítem de Productos con…», el bloque en «Agregar dato» y en «Así llega» | `FRM-Editor-Reglas-Bloque`, redibujado en la v175 con el 403 al guardar (239). Se redibujan: lo que se elige, en la pieza (262) |
| `FRM-Pdf` | el diseño, con tres estilos: Clásico, Moderno y Compacto (59), el color, el encabezado de la página pública (82), secciones y firmas | Redibujado en la v175: las horas en la zona de quien cargó (236) y una «Ubicación» (230). Se redibuja: cada dato de una opción de catálogo en su renglón o su columna (263), y el estilo en la pieza (262) |
| `FRM-Respuesta-Aprobacion` y su teléfono | la decisión, con «¿Aprobar VIS-000003?» (114 y 161), la actividad y el PDF | Redibujados en la v175: «No aplica» de quien dejó el rol, el rol sin personas, el último reintento (239) y el ⋮ (209). Se redibujan: «Sin confirmar», quién lo revisó y cuándo, y «Reintentar» después de revisarlo (240 y ADR 0066 §2); la respuesta como el formulario, con «Aprobar» y «Rechazar» arriba de las pestañas y «Corregir» a la vista (266); la actividad renglón por renglón, con su ⋮ (261 y 267); «Sin motivo» (276) y «1 de 2 por aprobar» |
| `FRM-Respuestas-21` y su teléfono | sobre `FRM-Respuestas-20`: «Estado», su filtro con «Anulada», la marca del PDF, la cola desde la campana y «Quien solo aprueba» (100) | Redibujados en la v175: «Por resolver (N)», la columna «Reglas», la vista vacía, el Excel desde la vista (228) y «Es cliente» en la cola (206). Se redibujan: «Estado» por el circuito y «Reglas», que ordenan (264); el filtro «Estado» en la pieza (262), y «Revisar» en el ⋮ de la cola (261) |
| `FRM-Tablero-21` y su teléfono | la tarjeta «Por estado» | Aprobados. Se redibujan: como `FRM-Tablero` de la E15, con la tabla de la tarjeta por el circuito (263 y 264) |
| `FRM-Respuesta-Corregir` y su teléfono | corregir, lo de antes, «Corregida», la versión anterior y «Ver la actual» | Redibujados en la v175: «Corregir» en el ⋮, arriba de «Anular» (209). Se redibujan: corregir en el lugar, en la vista rápida, la pantalla completa y el teléfono, sin panel ni página (266), y sus renglones en la actividad (267) |
| `FRM-Respuesta-Anular` y su teléfono | el ⋮ con «Anular», «¿Anular VIS-000127?», el filtro «Anulada» y el Excel con las anuladas | Aprobados en la v168 (144 y 166). Se redibujan: «Corregir» a la vista (266), el renglón «Anulada» con su motivo (267) y el filtro en la pieza (262) |
| `FRM-Respuestas-Propias-21` y su teléfono | lo propio, con estado y PDF, y corregir lo suyo mientras se pueda | Redibujados en la v175: la columna «Reglas» (228). Se redibujan: «Corregir» a la vista (266), su actividad con la decisión y su motivo (267), y «Estado» y «Reglas», que ordenan (264) |
| `Cuenta-21` y su teléfono | «Mi cuenta» con la zona «Chat de WhatsApp», con «Llegan hasta el …» y «Solo con plantilla» | Aprobados, nuevos en la v168 (159 y 170). Se redibujan: «Vincular» en el ⋮ de la organización (261) |
| `FRM-Editor-21` y `FRM-Cargar-Respuesta-21`, con sus teléfonos | la pregunta «Correo» | Aprobados en la v168 (141 y 177); «Ubicación» en los tipos, en la v175 (230). Se redibuja `FRM-Editor-21`: «Dónde se completa» en «El formulario» (229) y el tipo en la pieza, con «Correo» apagado en el bloque (262); y `FRM-Cargar-Respuesta-21`, sobre la forma de la E20 (262 y 266) |
| `Campana-21` y su teléfono | «3 respuestas por aprobar» y «No se pudo mandar el aviso de …» | Aprobados. Se redibuja `Campana-21`: la lista de atrás con «Estado» y «Dónde se completa» (229) |
| `WA-Chats-Avisos` y su teléfono | los avisos, con «Sin plantilla aprobada» (61) | Redibujados en la v175 con el aviso a quien cargó cuando su regla se frena (237). Se redibujan: un aviso con una «Ubicación», con el punto y el enlace al mapa en el renglón siguiente (230) |
| `WA-Plantilla-Nueva-21` y su teléfono | «Nueva plantilla» con «Encabezado»: «Sin encabezado» o «Documento (el PDF)», y «Así llega» con el documento arriba (201) | Aprobados, nuevos en la v170. Se redibujan: «Encabezado» en la pieza (262) |
| `WA-Chats-Respuesta-21` y `M-WA-Conversacion-Respuesta-21` | la tarjeta de una respuesta que después se corrigió o se anuló, con su marca al pie | Aprobados, nuevos en la v168 (166) |
| `FRM-Dar-De-Baja-21` y `FRM-Editor-Baja-21` | «Se conserva» suma el PDF y las aprobaciones, y lo que espera se sigue decidiendo | Aprobados; `FRM-Editor-Baja-21`, en la v169 (88, 175 y 177). Se redibuja `FRM-Dar-De-Baja-21`: «App · No se carga más» en «Deja de pasar» (229) |

`FRM-Por-Aprobar` y `M-FRM-Por-Aprobar` están en el Archivo.

**Mandan sobre esta spec en:** «Ver todos» de «Para resolver» abre la lista con «Con algo por aprobar». «Reglas de la revisión N» se queda en Actividad, porque es un historial. El diseño guarda aparte de las reglas, y su vista previa no guarda nada. Las reglas y el diseño valen sin publicar: lo dice la (i) de su título, sin leyendas (109 y 116).

**Valen las de toda la app** (160 a 164, 173 a 175, 180, 261 a 264 y 269).

## Decisiones tomadas (no se vuelven a preguntar)

- **Un solo motor,** sin una segunda cola de salida.
- **El rol** delimita quién decide, y nunca reemplaza al permiso.
- **Aprobación de un nivel** (60), y **quien aprueba** es la 25, con las dos casillas de la 276.
- **PDF con PDFsharp-MigraDoc,** como el Asistente: no se suma otra biblioteca de PDF.
- **El PDF, en el idioma de quien lo recibe** (42): una copia fija por idioma del sistema.
- **Las condiciones del bloque, como «Filtros»** y como allá en la etapa 4 de renglones: las que tienen valor, en un mismo ítem; la presencia, en la respuesta entera.
- **Sin `ExecutionActor` en el worker:** quien respondió puede ser un cliente sin cuenta.
- **Lo propio tiene sus rutas,** como en la E20.
- **«Adjuntar el PDF»** (113) y **confirmar antes de aprobar,** con el número (114 y 161), con las esperas y la ayuda de las reglas de todo el sistema (109, 115 y 116).
- **Corregir en el lugar** (266) **y anular, como allá** (166), con quién corrige de la 93: quien la cargó también corrige una rechazada, que allá no se corrige.
- **«Por resolver», el aviso a quien cargó y las acciones, como allá** (228, 237 y 239), con el segmentado de la 148 en lugar de una pestaña y «Sin confirmar» de la 240.

## Acciones del asistente

Anular una respuesta, `formrecords.void@1`, con «¿Anular VIS-000127?» (268 y [ADR 0068](../../decisions/0068-acciones-del-asistente.md)). Lo demás no se declara, y por qué, en la [parte de pasos](21-parte-pasos-y-verificar.md#acciones-del-asistente).

## Fuera de esta etapa

- Varios niveles o varios aprobadores.
- Acciones de otros módulos y compensaciones.
- Condiciones sobre todos los ítems de un bloque o sobre su cantidad.
- El asistente, las sugerencias y armar una regla desde un texto (E23).
- Que decida quien pasó la respuesta desde un documento, con «Puede decidir quien cargó» (238 y 276): E25.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta y el almacenamiento**:
1. llega un aviso al recibir;
2. otra cuenta con el rol aprueba desde la lista y desde «Para resolver», con «¿Aprobar VIS-…?»;
3. el PDF con el logo, la foto, los campos «Dato», el bloque, el total, una ubicación y las horas en la zona de quien cargó se abre en el celular, y las acciones quedan en «Hecho»;
4. una cuenta en inglés recibe el aviso y el PDF, con los textos del sistema en inglés;
5. un aviso fuera de las 24 horas llega con la plantilla de la organización, y «Mi cuenta» dice «Solo con plantilla»;
6. un correo con el PDF llega a una dirección fija y al correo escrito en el formulario;
7. corregir en el lugar una aprobada: conserva el número, vuelve a «Por aprobar», al aprobar sale un PDF nuevo, y en el chat la tarjeta de la anterior dice «Después se corrigió…»;
8. anular una con aprobación abierta: la tarea se cierra, sale de la planilla, los conteos y el Excel, y aparece con «Anulada»;
9. una regla «Si algún ítem de Productos con ¿Urgente? es Sí» avisa con los ítems, uno por línea, y una respuesta en la que ningún ítem cumplía todas las condiciones no pidió aprobación;
10. pedir aprobación a un rol sin personas deja la respuesta «Por resolver»; a quien cargó le llega el aviso una vez, y con alguien en el rol, «Reintentar» abre la tarea y la marca se va;
11. con «Puede decidir quien cargó», quien cargó aprueba la suya, y sin «Exigir motivo al rechazar», un rechazo sin motivo dice «Sin motivo» en la actividad.

## Requisitos previos

- La E20 cerrada. El almacenamiento de la E10 no frena: alcanza con `LocalFileStore`, y su prueba queda pendiente (123).
- La prueba con Meta de una plantilla con documento en el encabezado (47 y 61). La organización lo marca en «Nueva plantilla», con «Encabezado» (201).

## En el multitenant

- **Datos de la organización:** todo con RLS, salvo `platform.FormActionWorks`.
- **Workers:** el de acciones abre un scope por organización con `SetFromAccess(tenantId, TenantKind.Business)`, como `WhatsAppInboundProcessor`, y suma a `TenantScopeUsageTests`: un aviso en un chat vinculado escribe la copia compartida, y con `Enter`, que usa `TenantJobRunner`, ese guardado falla ([multitenancy](../../rules/multitenancy.md)).
- **Feature `forms`:** las rutas la llevan.
- **WhatsApp:** los avisos salen por el número de la organización.
- **Almacenamiento:** los PDF van en `IFileStore`, con la categoría `forms`.
- **«Para resolver»:** un `IPendingGroupSource` del módulo.
- **Cuotas:** las rutas cuentan en las cuotas generales.
