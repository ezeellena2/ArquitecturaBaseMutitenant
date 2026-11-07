# E21 · Reglas, aprobación, PDF y avisos (spec)

**Estado:** pendiente · spec de la etapa 21 del bloque (decisiones 34, 78, 80, 82, 91 a 93, 100, 106, 113, 114, 158, 159, 161, 166, 170 y 177), con el [formato](plantilla.md) de las etapas. Corresponde a la etapa 4 de ArquitecturaBase, con piloto real; a la etapa 3 de su carga desde la app, corregir y anular, programada allá el 2026-11-05, y a la etapa 4 de sus renglones, las reglas con el bloque, que allá no tenía plan el 2026-11-07. `/plan-etapa` la contrasta con el código real cuando le toque, después de la E20.

**Partes.** Esta spec no entra en un solo archivo, así que sigue en otros tres, con las mismas secciones. Lo que no está en la tabla está acá.

| Secciones | Archivo |
| --- | --- |
| Contrato, Datos y «Lo que ya se pagó allá» | [datos y contrato](21-parte-datos-y-contrato.md) |
| «Corregir y anular» | [corregir y anular](21-parte-corregir-y-anular.md) |
| Pasos y «Qué verificar al arrancar» | [pasos y qué verificar](21-parte-pasos-y-verificar.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T4, T5, T7 y la §6, punto 7; la [E15](15-respuestas.md): lista, planilla y detalle; la [E17](17-ciclo-de-vida.md): copiar y borrar; la [E18](18-fotos-y-documentos.md): archivos; la [E19](19-condiciones-calculos-y-lineas.md): condiciones y el bloque, y la [E20](20-cargar-desde-la-web.md): la captura y lo propio.
2. **Decisiones** de la hoja de ruta: 25, 35, 42, 43, 47, 49, 55, 59 a 62, 80, 82, 85, 89, 91 a 93, 100, 106, 109, 113 a 116, 138 a 140, 148, 149, 158, 159, 161, 162, 166, 170 y 177. Y la [ADR 0040](../../decisions/0040-para-resolver.md).
3. **Fichas:** [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [idempotencia](../../rules/idempotencia.md), [modulos](../../rules/modulos.md), [multitenancy](../../rules/multitenancy.md), [permisos](../../rules/permisos.md), [persistencia-ef](../../rules/persistencia-ef.md), [auditoria](../../rules/auditoria.md), [emails](../../rules/emails.md), [textos-libres](../../rules/textos-libres.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [logs](../../rules/logs.md), [api-http](../../rules/api-http.md) y [tests](../../rules/tests.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](21-parte-pasos-y-verificar.md#pasos).
5. **Referencia de producto,** solo para leer: en [`../2026-10-17-formularios-traspaso/`](../2026-10-17-formularios-traspaso/README.md), el anexo C, «Etapa 4», el anexo B, «Etapa 4», y el anexo D (G1 a G6); en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §5 y §6 (L-05 a L-08); en ArquitecturaBase, `docs/features/formularios.md`, «Corregir y anular», y en `ArquitecturaBaseFront`, `docs/design/carga-desde-la-app.md`, «Corregir y anular», y `renglones-en-formularios.md`, RG5-A.

## Qué se construye

**Reglas** «Cuando llega una respuesta»:
- cada guardado crea una revisión inmutable, con 409. No crea versión ni republica, y guardar una lista vacía las apaga;
- **cada regla:** un evento (Recibida, Aprobada o Rechazada), hasta 10 condiciones unidas con «y», con la gramática de la E15, y hasta 10 acciones en orden. Hasta 20 reglas;
- **las acciones:** pedir aprobación a un rol, armar el PDF, mandarle un mensaje a quien la cargó, avisar a un rol, avisar a una persona (62), mandar por correo (62) y mandar el PDF a un cliente (47);
- **el mensaje con datos** (149): «Agregar dato» abre una lista con buscador, con el número, el motivo del rechazo y las preguntas, sin fotos ni documentos, y el dato entra como una etiqueta que se borra entera. «Así llega» muestra el mensaje con datos de ejemplo. Se guarda `{FieldId}`, nunca el nombre, y una pregunta que ya no está se ve «Pregunta no disponible» y no deja guardar (`Forms.Rules.Invalid`). Es un solo texto (35). Una condición sobre una pregunta que no existía o no se mostró cuenta como «sin respuesta»;
- **el bloque** (146 y 177): al elegir en una condición una pregunta del bloque («Productos › ¿Urgente?»), la condición empieza con «Algún ítem con» y se cumple si algún ítem la cumple; varias del mismo bloque buscan un mismo ítem que las cumpla todas. La regla guardada dice «Si algún ítem de Productos con ¿Urgente? es Sí», con la pregunta y el valor en negrita, y en inglés, «If any item of Productos with…». Una respuesta de antes del bloque no cumple ninguna condición sobre sus preguntas. En «Agregar dato», el bloque es un dato que trae todos sus ítems, y sus preguntas no se ofrecen sueltas. En «Así llega» y en el aviso, cada ítem va en su línea, numerado, con sus valores separados por « · » en el orden de las preguntas: «1. Caño de 1/2 pulgada · 10 · Sí»;
- **el orden:** pedir aprobación es la última acción de su regla, y solo al recibir. **El PDF** (106): si alguna regla pide aprobación, se arma solo al aprobar, y lo que espera aprobación no tiene PDF; si no, se arma al recibir. Adjuntarlo exige una acción anterior que lo arme en la misma regla;
- cada acción se ordena con ↑ ↓ y se quita con 🗑, a la vista, como en ArquitecturaBase (135);
- **cómo se lee una regla guardada** (138): en pasos, cada uno con su ícono: el cuándo (una flecha al recibir, una tilde al aprobar, un círculo tachado al rechazar); «Si», con una condición por renglón, la pregunta y el valor en negrita, y cada acción, con su dato aparte en una etiqueta (el rol, «con el PDF») y el mensaje entero en su recuadro;
- **el historial** (140): «Historial» abre un panel al costado con las revisiones, de la más nueva a la más vieja, y cada una se lee en pasos. Sin revisiones queda apagado, con «Todavía no hay revisiones.» en su tooltip.

**«Adjuntar el PDF»** (113 y 162). Las acciones que mandan un mensaje o un correo llevan esa casilla, y «Mandar el PDF a un cliente» lo manda siempre:
- **nada se desmarca solo:** ni al mover, quitar o cambiar una acción, ni al cambiar el evento;
- **si «Armar el PDF» falta o está después,** debajo de la casilla aparece en el momento «Para adjuntarlo, «Armar el PDF» tiene que ir antes de este mensaje.». Debajo, un botón lo arregla: «Subir "Armar el PDF"» pone el primero que está después justo antes del mensaje; si no hay ninguno, «Agregar "Armar el PDF" antes» lo agrega ahí, y el foco vuelve a la casilla. Con 10 acciones, «Agregar» queda apagado, con el motivo en su tooltip;
- **cuándo se puede** (106): si el formulario pide aprobación, el PDF existe solo al aprobar. En otro evento, la casilla sin marcar se apaga, con el motivo en su tooltip. Marcada no se apaga: dice lo mismo debajo, y la regla no se guarda hasta sacarla o cambiar el evento. Sin aprobación, vale al recibir;
- **«Mandar el PDF a un cliente»** no tiene casilla: el aviso y el arreglo van debajo de la acción;
- **cambiar el tipo de una acción conserva lo escrito:** el mensaje, el rol, la persona y el adjunto vuelven si se elige otra vez el tipo anterior. Al guardar, cada dato va solo con el tipo que lo usa.

**Un solo motor de acciones,** con ejecuciones idempotentes y el efecto fuera de la transacción: está en la [parte de datos y contrato](21-parte-datos-y-contrato.md#el-motor-de-acciones).

**Aprobación de un nivel** (60):
- decide una cuenta activa con `formapprovals.decide` y el rol de la regla, **nunca quien cargó la respuesta** (25), ni quien la corrigió;
- una sola tarea abierta por respuesta. Decidir es un solo commit; si otro decidió antes, 409. Rechazar exige un motivo de hasta 500 caracteres;
- **leer y decidir son independientes:** con `formapprovals.decide` y sin `formrecords.read`, se ve la tarea en la vista rápida, sin valores ni pantalla completa (100). Su cola es la vista «Por aprobar» (148): el número, la fecha y el rol;
- **«Aprobar» pide confirmar con el número** (114 y 161): un diálogo como el de rechazar, «¿Aprobar VIS-000003?», con quién la cargó y cuándo; quien solo aprueba ve solo cuándo (100). «Aprobar» es la principal, sin rojo, y el foco empieza en «Cancelar», así Enter no aprueba; mientras manda, espera abierto con «Aprobando…» (115);
- **sin el rol de la tarea:** quien tiene el permiso y no el rol, como una cuenta administradora, ve el detalle sin error. `approval.canDecide` dice si puede decidir, y el detalle pide la tarea solo entonces; si no, la vista rápida muestra la decisión de solo lectura (el rol, quién decidió y cuándo), y la pantalla completa no reserva la barra de decisión.

**«Por aprobar» en «Formularios»,** con la misma fuente que «Para resolver» (ADR 0040):
- la lista suma la columna «Por aprobar» y el filtro «Aprobación», y lo que espera decisión va primero. Cuenta solo lo que esa cuenta puede decidir, sin lo propio;
- con solo `formapprovals.decide`, ve los formularios con tareas, sin respuestas ni última respuesta;
- **la vista «Por aprobar»** (148): arriba de la planilla, quien puede decidir ve «Por aprobar (N) | Todas». «Por aprobar» es una tabla corta (N°, Cargó, Recibida, Rol y «Revisar»), de la más vieja a la más nueva, sin filtros ni valores; «Revisar» abre la vista rápida y ↑ ↓ recorren esa cola. Se abre ahí si hay algo para decidir, y la vista va en la dirección. Quien solo aprueba la ve sin Cargó (100). La campana y «Para resolver» abren la más vieja.

**La planilla** suma la columna «Estado», en los formularios que piden aprobación, y la marca de la copia PDF confirmada. El filtro «Estado» va en todos: con aprobación, Recibida, Por aprobar, Aprobada y Rechazada, y siempre, «Anulada». Lo comparten la lista, los conteos y el Excel. El tablero suma «Por estado».

**La actividad de una respuesta** (139): cuatro columnas fijas, Fecha, Qué pasó, Estado y Acciones, en bandas por momento («Al recibirla», «Aprobación», «Al aprobarla» o «Al rechazarla»). Quién y el detalle van debajo de qué pasó, en gris, y el estado y las acciones («Ver PDF», «Reintentar»), siempre en su columna. «Reintentar» va en el renglón de cada persona, solo con la falla confirmada: WhatsApp lo rechazó o alguien resolvió «No salió». Su (i) dice que manda el aviso solo a esa persona y qué revisar antes, el WhatsApp vinculado y la ventana de 24 horas; sin permiso de reglas se ve la falla, sin el botón (158). El PDF va en su renglón, debajo de «Armar el PDF», y cada persona de un aviso a un rol, en el suyo, con sus acuses. En la vista rápida las acciones van debajo del texto; en el teléfono, también el estado.

**Corregir y anular** (91 a 93 y 166, como en ArquitecturaBase): una respuesta confirmada no se modifica: queda reemplazada por su corrección, con el mismo número, o anulada, sin borrarse. Está en su [parte](21-parte-corregir-y-anular.md).

**PDF:**
- un diseño con revisiones propias, con «Guardar diseño»: nombre, tres estilos, color, encabezado, secciones con título y orden, mostrar u ocultar preguntas, «Incluir las fotos», quién aprobó, hasta 3 firmas y pie. Sin editor libre (59);
- el logo y los datos salen de la página pública, y el color arranca con el del sistema (82). Sin página pública, va sin logo y con el nombre;
- muestra los campos «Dato», el valor de un catálogo tipado, las fotos, los cálculos y «No se mostró». «Incluir las fotos» sin marcar dice cuántas hay;
- **el bloque entero:** el diseño lo elige con su nombre, no sus preguntas, y el PDF lo pone como título de una tabla, con el número del ítem y una columna por pregunta, que sigue en la página siguiente con su encabezado si no entra. Sin ítems, «Sin respuesta». La vista previa suma dos ítems de ejemplo;
- al armarlo se fija una **copia fija** A4, una por ejecución, que se escribe una sola vez. Cambiar reglas, catálogos o el diseño no la altera. Los textos del sistema van en el idioma de la cuenta de quien cargó; si es un cliente sin cuenta, en el de la organización;
- solo se ve por la Api, con `formrecords.read`, con `<object>` y descarga como alternativa.

**Los avisos por WhatsApp y los correos** (61 y 62) están en la [parte de datos y contrato](21-parte-datos-y-contrato.md#los-avisos-y-los-correos).

**La pregunta «Correo»:** un tipo nuevo, validado con la ficha de correos. Suma a todo lo que existe: el editor, el Flow (un `TextInput` de correo), el verificador, la planilla, sus filtros (`contains` y `equals`), el detalle, el Excel, las condiciones, la carga desde la web, donde ocupa una columna (141), y el PDF.

**Lo propio** (85 y 166): la planilla de quien solo carga suma el estado y el PDF. Su detalle dice en qué quedó: si la rechazaron, el motivo va en los momentos, sin el rol ni quién decidió, y no en una columna de la planilla. No tiene Actividad, y «Corregir» está en la fila del título mientras pueda corregirla.

**El chat de WhatsApp en «Mi cuenta»** (159 y 170): debajo de los medios de ingreso, una zona «Chat de WhatsApp» con cada organización de la persona: «Vinculado», «Sin vincular» con «Vincular» o «Sin chat todavía» con «Abrir el chat». Con el chat vinculado, los avisos dicen «Llegan hasta el …» y, pasadas las 24 horas, «Solo con plantilla», con su (i): fuera de esa ventana llega solo un aviso con una plantilla aprobada de la organización (61). Sin vincular, «No llegan». Vincular confirma el número con un código, como hoy, y «Verificado» sigue siendo entrar con el número. Lo da el módulo de WhatsApp; apagado, la zona no está.

**Copiar un formulario** suma el diseño del PDF y las reglas vigentes, como su primera revisión.

## Pantallas

Aprobados y publicados en el lienzo v155, con el armado del front real de ArquitecturaBase (126 y 135) y los recorridos guiados `armar-las-reglas` y `aprobar-o-rechazar`. El ejemplo es «Visita a cliente», versión 4, que pide aprobación al rol Supervisor, con tres por aprobar; las reglas con el bloque usan «Pedido de materiales». Muestran solo lo que existe hasta acá (78). Con los traspasos 5, 9 y 10 se redibujan casi todos, y se suman tres.

| Tablero | Qué muestra | Estado |
| --- | --- | --- |
| `FRM-Formularios-21` y su teléfono | la lista de la 20, con «Cargar», «Por aprobar» y «Quien solo aprueba» | Se redibuja: «Mis cargas» y «Mi última carga» (153) |
| `FRM-Editor-Reglas`, `M-FRM-Reglas` | las reglas, como una pestaña más, con su historial, las siete acciones, «Adjuntar el PDF» (113) y la plantilla para fuera de las 24 horas | Se redibujan: en pasos, con el historial detrás de su botón y «Agregar dato» (138, 140 y 149) |
| `FRM-Editor-Reglas-Bloque` y `M-FRM-Reglas-Bloque` | las reglas de «Pedido de materiales»: «Algún ítem de Productos con…», el bloque en «Agregar dato» y en «Así llega» | Por dibujar (177) |
| `FRM-Pdf` | el diseño, con tres estilos: Clásico, Moderno y Compacto (59), el color, el encabezado de la página pública (82), secciones y firmas | Aprobado |
| `FRM-Respuesta-Aprobacion` y su teléfono | la decisión, con «¿Aprobar VIS-000003?» (114 y 161), la actividad y el PDF | Se redibujan: la actividad en columnas fijas, «Reintentar» por persona y la decisión de solo lectura (139 y 158), y «Corregir» en la fila del título (166) |
| `FRM-Respuestas-21` y su teléfono | sobre `FRM-Respuestas-20`: «Estado», su filtro con «Anulada», la marca del PDF, la cola desde la campana y «Quien solo aprueba» (100) | Se redibujan: la vista «Por aprobar» y «Filtros» (144 y 148) |
| `FRM-Tablero-21` y su teléfono | la tarjeta «Por estado» | Aprobados |
| `FRM-Respuesta-Corregir` y su teléfono | el panel y la página de corregir, lo de antes, «Corregida», la versión anterior y «Ver la actual» | Se redibujan: «Corregir» en la fila del título, el motivo al final, «Guardar corrección» y «Ya cargado · 86 KB» (166) |
| `FRM-Respuesta-Anular` y su teléfono | el ⋮ con «Anular», «¿Anular VIS-000127?», el filtro «Anulada» y el Excel con las anuladas | Se redibujan: el ⋮ solo con «Anular», en rojo, y «Filtros» (144 y 166) |
| `FRM-Respuestas-Propias-21` y su teléfono | lo propio, con estado y PDF, y corregir lo suyo mientras se pueda | Se redibujan: el motivo del rechazo en los momentos, sin columna, y «Filtros» (144 y 166) |
| `Cuenta-21` y su teléfono | «Mi cuenta» con la zona «Chat de WhatsApp», con «Llegan hasta el …» y «Solo con plantilla» | Por dibujar (159 y 170) |
| `FRM-Editor-21` y `FRM-Cargar-Respuesta-21`, con sus teléfonos | la pregunta «Correo» | Se redibujan: el editor con el bloque, sin el «Grupo de líneas» (177), y la carga en dos anchos (141) |
| `Campana-21` y su teléfono | «3 respuestas por aprobar» y «No se pudo mandar el aviso de …» | Aprobados |
| `WA-Chats-Avisos` y su teléfono | los avisos, con «Sin plantilla aprobada» (61) | Se redibujan: un aviso con el bloque, un ítem por línea (177) |
| `WA-Chats-Respuesta-21` y `M-WA-Conversacion-Respuesta-21` | la tarjeta de una respuesta que después se corrigió o se anuló, con su marca al pie | Por dibujar (166) |
| `FRM-Dar-De-Baja-21` y `FRM-Editor-Baja-21` | «Lo que se conserva» suma el PDF y las aprobaciones, y lo que espera se sigue decidiendo | `FRM-Dar-De-Baja-21`, aprobado; `FRM-Editor-Baja-21` se redibuja con la E17: «Agregar título» (88) y la baja en una frase (175) |

`FRM-Por-Aprobar` y `M-FRM-Por-Aprobar` están en el Archivo.

**Mandan sobre esta spec en:** «Ver todos» de «Para resolver» abre la lista con «Con algo por aprobar». «Reglas de la revisión N» se queda en Actividad, porque es un historial. El diseño guarda aparte de las reglas, y su vista previa no guarda nada. Las reglas y el diseño valen sin publicar: lo dice la (i) de su título, sin leyendas (109 y 116). Donde un tablero dice «Ver la vigente», vale «Ver la actual».

## Decisiones tomadas (no se vuelven a preguntar)

- **Un solo motor,** sin una segunda cola de salida.
- **El rol** delimita quién decide, y nunca reemplaza al permiso.
- **Aprobación de un nivel** (60), y **quien aprueba** es la 25.
- **PDF con PDFsharp-MigraDoc,** como el Asistente: no se suma otra biblioteca de PDF.
- **Sin `ExecutionActor` en el worker:** quien respondió puede ser un cliente sin cuenta.
- **Lo propio tiene sus rutas,** como en la E20.
- **«Adjuntar el PDF»** (113) y **confirmar antes de aprobar,** con el número (114 y 161), con las esperas y la ayuda de las reglas de todo el sistema (109, 115 y 116).
- **Corregir y anular, como allá** (166), con quién corrige de la 93: quien la cargó también corrige una rechazada, que allá no se corrige.

## Fuera de esta etapa

- Varios niveles o varios aprobadores.
- Acciones de otros módulos y compensaciones.
- Condiciones sobre todos los ítems de un bloque o sobre su cantidad.
- El asistente, las sugerencias y armar una regla desde un texto (E23).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta y el almacenamiento**:
1. llega un aviso al recibir;
2. otra cuenta con el rol aprueba desde la lista y desde «Para resolver», con «¿Aprobar VIS-…?»;
3. el PDF con el logo, la foto, los campos «Dato», el bloque y el total se abre en el celular, y las acciones quedan en «Hecho»;
4. una cuenta en inglés recibe el aviso y el PDF, con los textos del sistema en inglés;
5. un aviso fuera de las 24 horas llega con la plantilla de la organización, y «Mi cuenta» dice «Solo con plantilla»;
6. un correo con el PDF llega a una dirección fija y al correo escrito en el formulario;
7. corregir una aprobada: conserva el número, vuelve a «Por aprobar», al aprobar sale un PDF nuevo, y en el chat la tarjeta de la anterior dice «Después se corrigió…»;
8. anular una con aprobación abierta: la tarea se cierra, sale de la planilla, los conteos y el Excel, y aparece con «Anulada»;
9. una regla «Si algún ítem de Productos con ¿Urgente? es Sí» avisa con los ítems, uno por línea.

## Requisitos previos

- La E20 cerrada. El almacenamiento de la E10 no frena: alcanza con `LocalFileStore`, y su prueba queda pendiente (123).
- La prueba con Meta de una plantilla con documento en el encabezado (47 y 61).

## En el multitenant

- **Datos de la organización:** todo con RLS, salvo `platform.FormActionWorks`.
- **Workers:** el de acciones abre un scope por organización con `SetFromAccess(tenantId, TenantKind.Business)`, como `WhatsAppInboundProcessor`, y suma a `TenantScopeUsageTests`: un aviso en un chat vinculado escribe la copia compartida, y con `Enter`, que usa `TenantJobRunner`, ese guardado falla ([multitenancy](../../rules/multitenancy.md)).
- **Feature `forms`:** las rutas la llevan.
- **WhatsApp:** los avisos salen por el número de la organización.
- **Almacenamiento:** los PDF van en `IFileStore`, con la categoría `forms`.
- **«Para resolver»:** un `IPendingGroupSource` del módulo.
- **Cuotas:** las rutas cuentan en las cuotas generales.
