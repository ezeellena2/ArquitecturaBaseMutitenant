# E23 · El asistente, con Casos (spec)

**Estado:** pendiente · spec de la etapa 23 del bloque (decisiones 34, 39, 51, 52, 63, 65 a 67, 78, 165, 169, 202, 219, 230, 261 a 264, 270, 271 y 278; la 278 precisa la 66), con el [formato](plantilla.md) de las etapas. Junta la etapa 5 de ArquitecturaBase, en piloto, con sus Casos del asistente, decididos y sin programar. El catálogo que propone el asistente pasa a la E25 (167). `/plan-etapa` la contrasta con el código real después de la E22.

**Partes.** Lo que no figura en esta tabla está acá.

| Secciones | Archivo |
| --- | --- |
| Cómo responde (respuestas redactadas, un turno y los topes), Contrato, Datos, En el multitenant, Lo que ya se pagó allá, Pasos y Qué verificar al arrancar | [datos y contrato](23-parte-datos-y-contrato.md) |

## Qué leer

Antes de empezar, en este orden:

1. **Lo anterior:** la [spec de la E14](14-formularios.md): T5, T6 y los puertos; la [E16](16-datos-de-las-personas.md): los filtros; la [E17](17-ciclo-de-vida.md): la disponibilidad; la [E18](18-fotos-y-documentos.md): bajar medios; la [E19](19-condiciones-calculos-y-lineas.md): el bloque; la [E21](21-reglas-y-aprobacion.md): reglas y cola; la etapa [«Acciones del asistente»](acciones-del-asistente.md): el alcance de solo lectura (B2), y la ficha [Asistente](../../features/asistente.md).
2. **Decisiones** de la hoja de ruta: 19, 27, 28, 35, 39, 46, 50 a 52, 63, 65 a 67, 78, 146, 165, 169, 177, 202, 219, 223, 230, 261 a 264, 268 a 272 y 278. ADR [0048](../../decisions/0048-asistente.md), [0051](../../decisions/0051-precision-tecnica-de-costos-ia.md), [0038](../../decisions/0038-whatsapp-de-atencion.md), [0054](../../decisions/0054-respuestas-redactadas-por-el-asistente.md), [0055](../../decisions/0055-transcripcion-de-audios.md), [0064](../../decisions/0064-reglas-de-toda-la-ia.md), las reglas de toda la IA, y [0068](../../decisions/0068-acciones-del-asistente.md), las acciones.
3. **Fichas y guías:** [modulos](../../rules/modulos.md), [guardado](../../rules/guardado.md), [concurrencia](../../rules/concurrencia.md), [multitenancy](../../rules/multitenancy.md), [cuotas](../../rules/cuotas.md), [logs](../../rules/logs.md), [textos-y-traducciones](../../rules/textos-y-traducciones.md), [tests](../../rules/tests.md), la [guía de herramientas](../../guides/agregar-una-herramienta-al-asistente.md) y la [receta de acciones](../../guides/agregar-una-accion-al-asistente.md).
4. **Código para copiar:** la columna «Copia de» de los [pasos](23-parte-datos-y-contrato.md#pasos).
5. **Referencia de producto,** solo para leer: en el traspaso del 17/10, retirado el 2026-11-08 (`git show e9f1a599:docs/plans/2026-10-17-formularios-traspaso/`), el anexo C, «Etapa 5», y el anexo D (H1 a H3, H5 a H7 y s1 a s4); en `../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`, el anexo C §7 a §9 y el anexo F, F1, con su ADR 0021. Allá también: `docs/features/inteligencia-artificial.md`, «Información del negocio», y en el front, `docs/design/asistente-casos.md` e `informacion-del-negocio-guiada.md`.
6. **Los dibujos** son de Diseño.

## Qué se construye

**Quién atiende** (51 y 52), con «Administrar WhatsApp» (28):
- el asistente atiende empleados en Internos y clientes en Clientes;
- **por bandeja,** quién atiende los chats nuevos, y cuándo vuelve un chat al modo configurado después de que lo tomó una persona: pasado un tiempo sin mensajes, al marcarlo «Resuelto» o nunca. Se mira al llegar un mensaje, sin worker;
- **cada chat** se cambia a mano y se puede fijar, y cada cambio de modo queda con quién y cuándo (al derivar, el asistente, sin persona). Pasarlo a Asistente pide `whatsapp.manage`. **Tomar la atención manual** pide solo el permiso de atender, siempre se puede, y deja sin efecto una propuesta que llegue tarde;
- el bot del menú aparece solo en los chats de Clientes en atención manual.

**Los Casos** (39), en una pantalla con un solo Guardar, con versión y 409. Se guardan como **el agente principal**, una fila por organización:
- **Perfil:** nombre, presentación, tono, qué hace y qué no, qué hacer cuando no sabe y el mensaje fijo al derivar;
- **Conocimiento** (46 y 169): la información del negocio, una por organización, como en ArquitecturaBase: los datos del negocio (nombre y qué hace), la ubicación y la zona de atención, con la dirección escrita y sin buscarla en Google, el horario, los productos y servicios, las políticas y condiciones, y las preguntas frecuentes. **El horario:** cada día dice «Sin definir», «Abierto», «Cerrado» o «24 horas»; abierto, lleva hasta cuatro franjas, de 15 en 15 minutos, y una que termina después de la medianoche lo dice. «Aplicar de lunes a viernes» copia un día a los demás días hábiles, y una fecha especial reemplaza su día. Una pregunta frecuente lleva su pregunta y su respuesta, y una sección vacía no se usa;
- **Casos:** cuándo (un texto), para quién (50), tipo de entrada, qué hacer, orden y si está activo. Los formularios van en un bloque fijo, y «Cualquier otra cosa» va al final y no se borra. Sin perfil, empieza en «No responder»;
- **Límites, uno por bandeja** (65 y 270): temas que no responde, lo que no puede prometer, largo máximo, tope de respuestas por conversación y día, tope de ofrecimientos (de 1 a 50 y 2 de entrada, 278), «Volver a ofrecer un formulario rechazado» («Nunca», el de entrada, o a los N días, de 1 a 365) y acciones permitidas. Cada uno tiene su mínimo y su máximo, y los tres topes, además, el máximo de la plataforma, que la pantalla dice. Se leen de la base en cada turno: sin un asistente guardado, o con uno guardado antes de un valor, vale el de entrada (0060).

**Seis acciones,** un menú cerrado en el código: mandar un formulario, responder con un texto exacto, responder con el conocimiento, preguntar con hasta 3 botones, derivar y no responder. Un caso nuevo es configuración; una acción nueva es código.

**Probar** acepta hasta cinco mensajes, uno por renglón, como una ráfaga, y muestra qué caso elige, por qué y qué respondería, con lo guardado y sin mandar nada ni gastar los topes de WhatsApp. Cuenta en el tope de IA, con el cupo de quien prueba, y sigue sus permisos: pide atender la bandeja que prueba, y sin `formrecords.read` no muestra el porqué ni lo que llenaría un formulario.

**Cómo responde** (39 y 67), con su ADR: las respuestas redactadas (fuentes, forma, validación y derivar), los siete pasos de un turno, lo que pasa cuando la IA no contesta y **los topes, que contestan o derivan y dicen por qué** (270), están en la [parte de datos y contrato](23-parte-datos-y-contrato.md#cómo-responde). Derivar manda el mensaje fijo, pasa a atención manual y marca «Necesita una persona», con su motivo en la franja del chat y en «Por qué respondió esto».

**Ofrecer formularios:** hasta el tope por conversación y día, en la zona de la organización; solo los publicados, activos y con «Cuándo lo ofrece», para ese destinatario (50), con el filtro de disponibilidad y sin las versiones solo para la app, ordenados por nombre antes de cortar en 100. Lo manda el asistente: la sesión lo guarda así, y no toma el chat. **La oferta** dice siempre «Te preparé el formulario «Pedido de service» para que lo completes.», con el nombre del formulario (165): es un texto fijo, que el modelo no redacta, también cuando un operador manda una sugerencia en atención manual. **El prellenado** propone texto, números, fechas, sí/no y una opción ofrecida, como valores iniciales de un perfil nuevo del Flow **[Meta]**. Deja afuera «elegir varias», las fotos, los documentos, la Ubicación y las preguntas del bloque (146), y tampoco prellena una pregunta con «Mostrar si» ni la que fija una marca de varios valores, que arranca en `false` y un valor prellenado no cambia (E19). Uno publicado antes se manda sin prellenar. **Con una Ubicación** (230), la oferta sale como en la E20: antes del Flow, el pedido de ubicación de Meta, y la ubicación que llega lo contesta sin abrir un turno. El asistente no ve los valores de las respuestas.

**El tope de ofrecimientos** (278): cuenta lo que ofrece el asistente en esa conversación y día. Una pregunta con botones que lleva a un formulario y el formulario elegido gastan una sola oferta, y elegir una opción de una pregunta ya ofrecida no gasta otra, aunque el tope haya bajado o haya cambiado el día. Con el tope lleno, lo que pidió la persona no sale y el chat deriva con el motivo «Llegó al tope de ofrecimientos del día»: la persona nunca queda sin respuesta. Los textos no nombran «dos». **Un formulario rechazado** (la persona dijo que no después de una oferta; no abrirlo no es rechazarlo) no se le vuelve a ofrecer en esa conversación hasta el plazo de su límite.

**Sugerencias entre formularios:** `formsuggestions.manage`, hasta 20 por formulario, hacia formularios para el mismo destinatario. «La IA decide» o «Preguntar siempre», por condición y sin IA, con mapeos entre preguntas del mismo tipo. Una hacia un dado de baja o «Solo en la app» deja de ofrecerse y vuelve si se reactiva o vuelve a WhatsApp. En uno «Solo en la app», «Cuándo lo ofrece» y «Sugerencias» van apagados con «Se completa solo en la app.» y conservan lo cargado (258). La respuesta conserva su origen y la cadena.

**Sin la feature `assistant`,** «Cuándo lo ofrece» y «Armar desde un texto» no aparecen, y una sugerencia queda en «Preguntar siempre», que no usa IA.

**Copiar un formulario** suma «Cuándo lo ofrece» y las sugerencias vigentes, como su primera revisión.

**Audios:** se bajan como los archivos del chat y se transcriben por un puerto del núcleo (27), siempre, también con el tope de ofrecimientos lleno, y siguen como un texto: si lo contesta un caso, lo contesta (278). La transcripción la ve también el operador.

**Armar una regla desde un texto** (63): la IA propone una regla normal, con el modelo calificado del canal, como Probar (218), y la persona la revisa y la guarda. Después corre sin IA. Con el tope agotado, queda grisado, con el motivo en su tooltip.

**Preguntas sin respuesta** (67) y **Formularios que faltan:** agrupadas por tema, sin nombres ni mensajes, con cantidades. Se completa el conocimiento, se crea el formulario o se descarta, desde el ⋮ del tema (261). Cada dato va en su columna, también la descripción, que ya no va debajo del tema, y en el teléfono cada tema es una tarjeta con sus datos nombrados (263). Toda columna ordena, el estado por el circuito (Sin revisar, Abierto, Descartado), y sin orden elegido va la última pregunta, de la más reciente a la más vieja (264); el filtro «Estado» va en la pieza, sin cantidades (262). **Un tema nuevo se revisa antes de usarse con otros** (219): nace «Sin revisar», y el asistente no lo usa en otros chats hasta que alguien con `whatsapp.manage` (preguntas) o `forms.manage` (formularios) lo marca «Revisado», con su versión y el 409; al asistente va solo el nombre. Un tema descartado vuelve, con la pregunta sumada, si alguien lo pregunta de nuevo (202), y vuelve «Sin revisar».

**Desde el primer día:**
- **la espera:** el motor avisa con una señal local al confirmar, y la entrada sondea cada 2 s solo si hay algo pendiente. Una sola cola durable;
- **el aislamiento por conversación:** `ChannelConversationId` en el contexto, la recuperación y la cancelación. Se prueba con tres personas a la vez;
- **la medición** de texto a envío y de audio a envío, desde la primera corrida.

**Costos y cupos** (270 y 271): el modelo, la transcripción y Probar entran en el tope mensual de IA (ADR 0051). Con el tope agotado, el asistente no está disponible y un mensaje que llega deriva como con los demás topes, con un motivo propio, distinto de «El asistente no pudo responder». Probar y «Armar desde un texto» gastan además el cupo diario de quien los usa, que es suyo en esta organización y se renueva a la medianoche de la zona de la organización; agotado, quedan grisados con «Llegaste a las N consultas de hoy. Se renuevan a las HH:mm.», no con el motivo del tope del mes.

## Pantallas

**Aprobados** en el lienzo v175. La pantalla «Asistente» no tiene modelo, ni en el lienzo ni en ArquitecturaBase. Lo demás sigue al front real de ArquitecturaBase, con nuestras piezas: el chat con su modo y «Por qué lo mandó», la sugerencia, los audios, «Qué sugerir después», «Cómo empezó» e «Información del negocio». El catálogo propuesto sale de esta etapa (167): `FRM-Catalogo-Propuesta` y `FRM-Catalogos-23` pasaron al Archivo. Los tableros muestran Probar con cinco mensajes y sin «Ver respuestas», «El asistente no pudo responder» con el motivo de una derivación, y «Sin revisar» y «Marcar como revisado» en los temas (219).

**Valen las de toda la app** (160 a 164, 173 a 175, 180, 261 a 264 y 269): lo que carga no salta, decidir a propósito, ninguna elección se descarta en silencio, mucho texto con jerarquía, sin parecer plantilla y con la escala de ArquitecturaBase, el texto largo entero y el estado dicho una vez.

Datos de ejemplo: Grupo Delta vende y repara heladeras. Martina López escribe por Clientes y Tomás Acosta, por Internos.

| Tablero | Qué muestra | Estados |
| --- | --- | --- |
| `WA-Asistente` y `M-WA-Asistente` | «Asistente» en el panel de WhatsApp: Perfil, Conocimiento, Casos, Límites y Atención, un solo Guardar en la banda y «Probar» siempre al costado. Se entra por Casos | perfil, sin perfil (abre en Perfil, con «Cualquier otra cosa» en «No responder»), faltan los datos, con cambios, guardando, guardado, otra persona guardó, descartar y cargando |
| `WA-Asistente-Casos` | los casos propios con ↑ ↓ ✎ 🗑 (127), que se abren en el lugar: cuándo, para quién, el tipo de entrada, qué hacer y si está activo. Debajo, el bloque fijo de formularios y «Cualquier otra cosa» | editar, el menú de las seis acciones, preguntar con botones, texto exacto, mandar un formulario, derivar, caso nuevo, con errores, «Cualquier otra cosa» y sin perfil |
| `WA-Asistente-Probar` | lo que haría, en orden y hasta tres acciones: cada una con su caso, su porqué, lo que usó y cómo la vería la persona. Al final, «No se mandó nada» | cuatro pruebas (envíos a Córdoba, la heladera, hablar con alguien, hola y la hora), probando, con cambios sin guardar y tope de IA agotado (grisado, con el motivo en su tooltip) |
| `WA-Asistente-Limites` | Límites, por bandeja (65), con el tope de ofrecimientos (66) y el máximo de la plataforma debajo de cada número; Conocimiento, con la información del negocio (46 y 169): los datos, la ubicación con la dirección escrita, el horario de cada día con sus franjas, «24 horas» y «Aplicar de lunes a viernes», las fechas especiales, los productos y servicios, las políticas y condiciones, y las preguntas frecuentes | Internos, sobre el máximo, una sección vacía, un día con dos franjas, uno de 24 horas, una franja que pasa la medianoche, dos que se pisan, «Aplicar de lunes a viernes» y editar una pregunta frecuente |
| `WA-Modo-Atencion` y `M-WA-Modo-Atencion` | la pestaña Atención, por bandeja: quién atiende los chats nuevos y cuándo vuelve un chat (52). En el teléfono, el ⋮ del chat: pasar de modo, fijarlo y «Marcar como resuelta» | Internos, una persona atiende, al marcarlo «Resuelto», guardando y guardado; en el teléfono, atención manual, fijada y resuelta |
| `WA-Chats-Casos` y `M-WA-Chats-Casos` | el chat de Clientes con los casos: «Por qué respondió esto» en el pie de cada acción, que abre un globo, y derivar con «Necesita una persona» en la cabecera, la lista y su filtro. Muestra también el texto exacto, los botones, el tope del día y el borrador en atención manual | por qué respondió, sin la información (deriva), un formulario, botones, texto exacto, hablar con alguien, una ráfaga con tres acciones, el tope, el borrador, cambiar el modo, fijada, resuelta y el filtro |
| `WA-Preguntas-Sin-Respuesta` y `M-WA-Preguntas-Sin-Respuesta` | en el panel de WhatsApp, por tema, con preguntas, personas y la última. El ⋮ tiene «Completar el conocimiento» y «Descartar». Sin nombres ni mensajes | acciones, descartar, el filtro de estado, descartados, vacío, cargando y error |
| `WA-Chats-Asistente`, `WA-Chats-Sugerencia` y `WA-Chats-Audio`, con `M-WA-Conversacion-Asistente` | revisados: la barra de la bandeja y las fechas de WA-Chats, y el tope agotado. Sale «Sin respuesta del asistente», que pasa a `WA-Chats-Casos`. El audio, como allá: «Escuchar audio» y la transcripción debajo | los de antes, más escuchar el audio y «Ya no está» (D4 y 272) |
| `FRM-Editor-Asistente` | redibujado: «Cuándo lo ofrece el asistente», con su (i), en «El formulario» de la pestaña Diseño | sin «Cuándo lo ofrece» y sin la función del asistente |
| `FRM-Editor-Sugerencias` | redibujado: la pestaña «Sugerencias», como las reglas (79), con «Qué sugerir después», su historial tras un botón (140), en pasos (138) y con ✎ 🗑 en cada una | editar, preguntar siempre, un dato de otro tipo, hacia un dado de baja, hacia uno de la app (258), guardando, guardadas, sin sugerencias y sin el asistente |
| `FRM-Editor-Reglas-23` y `M-FRM-Reglas-23` | «Armar desde un texto» junto a «Agregar regla» (63): la IA propone una regla normal, que queda abierta y sin guardar | armar, armando, la regla propuesta, no se pudo, tope agotado (grisado, con el motivo) y sin el asistente |
| `FRM-Dar-De-Baja-23` y `FRM-Editor-Baja-23` | «Deja de pasar» suma que el asistente no lo ofrece y las sugerencias hacia el formulario, que vuelven si se reactiva | — |
| `FRM-Faltan` y `M-FRM-Faltan` | revisados: la banda cuenta temas y pedidos, y las fechas van completas | los de antes |
| `FRM-Respuesta-Cadena` | revisado, sobre la vista rápida de la 21: «Cómo empezó», con «Ver los datos propuestos», y la cadena de respuestas | los datos que propuso y la cadena |
| `Campana-23` y `M-Campana-23` | la campana suma las preguntas sin respuesta, para quien administra WhatsApp | — |
| `Plat-Asistente-Config-23` y `M-Plat-Asistente-Config-23` | «Máximos del asistente de WhatsApp»: el largo de la respuesta, y las respuestas y los formularios que ofrece por conversación y día | — |

**Se redibujan con el traspaso 18, tanda 4** (261 a 264, 270, 271 y 278):
- `WA-Asistente`, `WA-Asistente-Casos` y `WA-Asistente-Probar`: la bandeja de «Probar», en la pieza (262); los casos y los formularios del bloque, con «Para quién» y «Qué hacer» en sus columnas, no juntos con «·» debajo de «Cuándo» (263), y lo que se elige al editar un caso, en la pieza (262); Probar grisado con el cupo del día agotado, con su motivo, distinto del tope del mes (271). En `M-WA-Asistente`, los casos sin datos juntos con «·» (263);
- `WA-Asistente-Limites`: el tope de ofrecimientos, de 1 a 50 y sin nombrar «dos», y «Volver a ofrecer un formulario rechazado», con «Nunca» o los días (270 y 278); la bandeja, «Acciones que puede usar» y el estado de un día del horario, en la pieza (262);
- `WA-Modo-Atencion`: quién atiende y cuándo vuelve un chat, en la pieza, sin radios (262);
- `WA-Chats-Casos` y `M-WA-Chats-Casos`: el tope de ofrecimientos lleno, que deriva con «Llegó al tope de ofrecimientos del día» en la franja y en el porqué (278);
- `WA-Chats-Asistente` y `M-WA-Conversacion-Asistente`: con el tope del mes agotado, el mensaje que llega deriva con su motivo, y no queda sin respuesta (270); `WA-Chats-Audio`: un audio con el tope de ofrecimientos lleno se transcribe y lo contesta su caso (278);
- `WA-Preguntas-Sin-Respuesta`, `FRM-Faltan` y sus teléfonos: la descripción en su columna; en el teléfono, una tarjeta con cada dato nombrado, sin «·» (263); el encabezado y «Ordenar por» (264), y el filtro «Estado» en la pieza, sin cantidades (262);
- `FRM-Editor-Sugerencias`: «La IA decide» o «Preguntar siempre», el formulario y cada pregunta de un mapeo, en la pieza (262);
- `FRM-Editor-Reglas-23` y `M-FRM-Reglas-23`: «Armar desde un texto» grisado con el cupo del día agotado, distinto del tope del mes (271);
- `FRM-Respuesta-Cadena`: sobre la vista rápida de la E21 redibujada (266 y 267), y la cadena con cada dato en su columna, no debajo del número (263);
- `Plat-Asistente-Config-23` y su teléfono: el máximo de ofrecimientos, de 1 a 50 y 50 de entrada (278); hoy dice 3.

El recorrido guiado «Los casos del asistente» muestra a Lucía armando y probando los casos, y a Martina escribiendo, hasta la ráfaga con tres acciones.

**Mandan sobre esta spec en:**
- **Los nombres de la API:** «La IA decide» es `Assistant`, y «Preguntar siempre», `Always`.
- **Los audios:** «No pudimos pasar este audio a texto» cubre también el tope del mes agotado, y deriva (270); con el de ofrecimientos lleno, el audio se transcribe igual (278). Un audio sigue el plazo de los chats de la organización (272, que cambia la D4).
- **Probar** prueba lo guardado: con cambios sin guardar queda grisado, con el motivo en su tooltip.
- **Atención** es la quinta pestaña de la pantalla. Tiene su ruta y su versión (`/whatsapp/attention`), pero se guarda con el mismo botón.
- **El ⋮ del chat:** ahí van cambiar el modo, fijarlo y «Marcar como resuelta».
- **La oferta** (165): todo tablero que muestra un formulario que manda el asistente usa el texto fijo de «Ofrecer formularios», también en Probar.
- **El panel de «Gestión de formularios»** (275): «Formularios que faltan» suma su entrada después de «Biblioteca», con `forms.manage` y la feature `assistant`.

**Decisiones del usuario** (03/11):
- la pantalla «Asistente» se arma como el editor de un formulario: el contenido va a la izquierda y «Probar», siempre a la derecha, y los casos se editan en el lugar, con ↑ ↓ ✎ 🗑;
- «Por qué respondió esto» se abre al tocarlo, en un globo junto al mensaje que no corre el chat; en el teléfono, en una hoja. Nunca aparece abierto solo.

## Decisiones tomadas (no se vuelven a preguntar)

- **Casos** (39): contesta lo que configure cada organización, redacta dentro de sus límites y deriva lo demás.
- **La IA nunca guarda ni confirma una respuesta,** y siempre propone: ejecuta el módulo dueño.
- **La configuración vive en nuestra base,** por organización. **Genérico:** ninguna respuesta queda en el código.
- **Un solo botón, «Abrir formulario».**
- **La oferta** es un texto fijo con el nombre del formulario (165).
- **Un modelo se habilita solo con un perfil calificado.**
- **El conocimiento** es una pestaña, sin pantalla ni permiso aparte (46), y es la información del negocio de ArquitecturaBase, con la dirección escrita (169).
- **El agente vive en el módulo Assistant,** y el modo de cada chat, en WhatsApp.
- **Los temas se revisan antes de usarse con otros** (219), en las dos listas.
- **El cupo, las fallas del proveedor y el apagado** siguen la [0064](../../decisions/0064-reglas-de-toda-la-ia.md), como en el Inicio, y el cupo diario de una persona es de cada organización (271).
- **Un tope que corta contesta o deriva, y dice por qué** (270 y 278): ninguno deja a la persona sin respuesta ni al operador sin pista.

## Acciones del asistente

Con la [receta](../../guides/agregar-una-accion-al-asistente.md) (268 y [0068](../../decisions/0068-acciones-del-asistente.md)), registradas y sin ofrecer hasta la calificación. Ninguna tiene efecto fuera del sistema.

| Acción | Dónde se confirma | Permiso | Ruta que guarda |
| --- | --- | --- | --- |
| `whatsappattention.edit@1` · Atención | la pestaña «Atención» de «Asistente», sin guardar (`Page`): por bandeja, quién atiende y cuándo vuelve (`Choice`) y los minutos (`Integer`) | `whatsapp.manage` | `PUT api/organization/whatsapp/attention` |
| `unansweredtopics.dismiss@1` · Descartar un tema | la confirmación de descartar de «Preguntas sin respuesta» (`RemoveConfirmation`) | `whatsapp.manage` | `POST …/unanswered-topics/{id}/dismiss` |
| `missingforms.dismiss@1` · Descartar un tema | la de «Formularios que faltan» (`RemoveConfirmation`) | `forms.manage` | `POST …/forms/missing/{id}/dismiss` |

**No se declaran:**
- **Perfil, Conocimiento, Casos y Límites** (`PUT assistant/agent`): la ruta es de WhatsApp y guarda en el Assistant por `IConversationAgentSettings`, un puerto que una fuente no puede usar (0068 §5), y los casos, las franjas y las preguntas frecuentes son listas de objetos, sin tipo (§4);
- **las sugerencias entre formularios:** listas de condiciones y mapeos, sin tipo;
- **«Marcar como revisado» y el modo, fijar o resolver un chat:** son de un clic, sin confirmación ni formulario donde confirmar (§9);
- **mandar una sugerencia, armar una regla desde un texto y «Crear formulario» de un tema:** ya los propone la IA, o nacen en borrador (0057);
- **los máximos de Plataforma:** piden la prueba reciente, que no se declara (§4).

**El turno de canal no ofrece las tres piezas de acciones** (0068 §11): se suman por WhatsApp cuando el canal tenga lugar.

## Fuera de esta etapa

- Varios asistentes y quién contesta cada chat (E24).
- El catálogo que propone el asistente y armar formularios y catálogos con la IA: son propuestas que una persona crea con un botón (154 y 167), y llegan con la E25, como leer fotos y documentos.
- Algo nuevo en el acceso Personal: lo que manda el asistente se ve en la copia de la conversación de la E11.
- Que una carga desde una planilla no gaste el tope de ofrecimientos (278): llega con la lectura de planillas de la E25.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más:
- **pruebas de arquitectura:** nadie llama al modelo fuera de las piezas del asistente, y las herramientas no reciben `IUnitOfWork`;
- **casos de evaluación** antes de habilitar el modelo: con y sin la información, fuera de tema, saludo y hora, derivación e instrucciones escondidas, y un tema sin revisar que no se usa;
- **la medición** de texto a envío y de audio a envío;
- **prueba real con la IA y Meta:** «hola» y «¿qué hora es?» tienen sentido; una consulta con la información se responde y una sin ella deriva; «¿abren el domingo?» contesta con el horario cargado; «se rompió la heladera» manda el formulario, con la oferta que lo nombra; «quiero hablar con alguien» deriva; un audio transcripto, y un cliente recibe una respuesta y un formulario para clientes; Probar con cinco mensajes, y la IA caída deriva con «El asistente no pudo responder»; con el tope de ofrecimientos en 1, el segundo formulario del día deriva con «Llegó al tope de ofrecimientos del día», y un audio se transcribe igual.

## Requisitos previos

- La E22 cerrada.
- Rigen las ADR [0054](../../decisions/0054-respuestas-redactadas-por-el-asistente.md), las respuestas redactadas, y [0055](../../decisions/0055-transcripcion-de-audios.md), la transcripción (27).
