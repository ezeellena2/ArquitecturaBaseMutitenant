# E18 · El asistente, con Casos (resumen)

**Estado:** resumen de la décima etapa del bloque (decisiones 34, 39, 51 y 52). Junta la etapa 5 de ArquitecturaBase, cerrada y en piloto, con los Casos del asistente, decididos allá sin programar. Se planifica con `/plan-etapa` después de la E17 (decisión 19).

**Dónde está el detalle:**
- en el traspaso del 17/10: [04 §5](../2026-10-17-formularios-traspaso/04-etapas.md), [05 · Etapa 5](../2026-10-17-formularios-traspaso/05-pantallas.md), anexo C §2 (etapa 5) y anexo D (H1–H7, s1–s4 y 03);
- en el traspaso 2 de ArquitecturaBase: el anexo C §7 a §9 (A-01 a A-07, W-01 a W-11 e IA-01 a IA-12), el anexo F (F1, los Casos) y el anexo D (AS1-A a AS6);
- allá, las ADR 0020 (motor único de IA) y 0021 (respuestas redactadas).

## Qué se construye

- **Back:**
  - **Las piezas de la IA:** el asistente de la 8b (ADR 0048) suma canales para WhatsApp: el modo Asistente, la sugerencia al operador y el chat administrativo.
    - Forms y WhatsApp aportan **herramientas** que solo leen o proponen, siguiendo la [guía de herramientas](../../guides/agregar-una-herramienta-al-asistente.md).
    - Ejecuta siempre el módulo dueño, con su servicio, su UoW y su cola.
  - **El asistente atiende empleados y clientes** (51), en Internos y en Clientes.
  - **El modo de atención** (52), configurable por cada organización con «Administrar WhatsApp» (28):
    - **por bandeja,** quién atiende los chats nuevos: el asistente o una persona;
    - **por bandeja,** cuándo vuelve un chat al modo configurado después de que lo tomó una persona: cuando pasa un tiempo sin mensajes (lo elige la organización), cuando la persona lo marca «Resuelto», o nunca. Se mira al llegar un mensaje, sin worker: si el chat está en manual, no está fijado y se cumple la regla, vuelve al modo configurado antes de procesarlo;
    - **cada chat** se cambia a mano en cualquier momento, en los dos sentidos, y se puede fijar para que no vuelva solo. Pasar un chat a Asistente pide «Administrar WhatsApp»; tomar la atención manual pide solo el permiso de atender esa bandeja, no depende de la IA, de la ventana de 24 horas ni de los envíos pendientes, y deja sin efecto una propuesta que llegue tarde;
    - **el bot del menú** de la E11 aparece solo en los chats de Clientes que están en atención manual.
  - **Los Casos** (39), configurados por cada organización con «Administrar WhatsApp», en una pantalla con un solo Guardar, con versión y 409 (configuración interna). Se guardan como **el agente principal**, una fila con todo lo suyo, así una etapa de varios agentes suma filas sin rehacer nada:
    - **Perfil:** nombre, presentación, tono, qué hace y qué no, qué hacer cuando no sabe y el mensaje fijo al derivar;
    - **Conocimiento:** la información del negocio que usa para redactar, una por organización. Se edita en su propia pestaña, con el mismo Guardar y con «Administrar WhatsApp» (decisión 46). Una sección vacía no se usa: nunca se presenta como un hecho lo que no está cargado;
    - **Casos:** por cada uno, cuándo (un texto), para quién (empleados, clientes o los dos, como los formularios, 50), el tipo de entrada (cualquiera, texto o audio; los documentos llegan con la E19), qué hacer, el orden y si está activo. Los formularios van en un bloque fijo, con su «Cuándo lo ofrece», y «Cualquier otra cosa» va al final y no se borra. Sin perfil, «Cualquier otra cosa» empieza en «No responder»;
    - **Límites, uno por bandeja** (65): temas que no responde, lo que no puede prometer, el largo máximo de una respuesta, el tope de respuestas por conversación y por día, el tope de ofrecimientos de formularios (66) y las acciones permitidas. Con los clientes, la organización puede ser más estricta, por ejemplo solo con textos exactos. La plataforma fija un máximo para cada límite.
  - **Seis acciones,** un menú cerrado en el código:
    1. mandar un formulario;
    2. responder con un texto exacto, escrito por la organización;
    3. responder con la información del negocio, redactada por la IA;
    4. preguntar con hasta 3 botones, cada uno con su destino;
    5. derivar a una persona, con un motivo;
    6. no responder.

    Un caso nuevo es configuración; una acción nueva es código, con su herramienta y sus casos de evaluación.
  - **Probar** muestra qué caso elige, por qué y qué respondería, con lo guardado y sin mandar nada. Llama al modelo, así que cuenta en el tope de IA de la organización.
  - **Respuestas redactadas:** antes de planificar, una ADR de acá, equivalente a la 0021 de allá, fija los límites:
    - **fuentes** (67): para lo de la organización (precios, envíos, stock, horarios, garantías, plazos y políticas), solo lo que cargó en su conocimiento. Para lo general, que no compromete a la organización (cómo sacar una foto, qué es un CBU, cómo completar el formulario), puede ayudar con lo que sabe el modelo. Además, el historial de esa conversación y los datos del sistema que el canal le da, como la hora y la zona de la organización. Lo leído es un dato, nunca una instrucción;
    - **forma y contenido:** corta, con el largo máximo y en el idioma de la conversación; sin enlaces, teléfonos, correos, precios ni compromisos que no estén cargados;
    - **si es de la organización y no está cargado** (67): primero pregunta para entender, después dice que lo consulta con el equipo y deriva. Nunca contesta algo genérico en lugar del dato;
    - **control del servidor:** la respuesta llega como propuesta tipada, y WhatsApp la valida (largo, idioma, enlaces y datos de contacto) antes de encolarla. Si no pasa, no sale y deriva;
    - **trazabilidad:** «Por qué respondió esto» guarda el texto, el caso y lo que usó; los logs no llevan el contenido;
    - **cupos:** las respuestas tienen su tope por conversación y día, aparte de los 2 ofrecimientos de formularios.
  - **Un turno:**
    1. llega un texto o un audio transcripto de un empleado activo (Internos) o de un cliente (Clientes), en un chat en modo Asistente y dentro de las 24 horas;
    2. **la ráfaga:** espera 4 s sin mensajes nuevos, con un tope de 15 s, y procesa todo junto;
    3. arma el pedido en un orden estable: perfil, casos, formularios candidatos, **el historial de las últimas 24 horas de esa misma conversación** (hasta 10 mensajes, con las transcripciones) y lo que ya hizo;
    4. el modelo elige el caso y propone **hasta 3 acciones, en orden**, y el sistema valida cada una por separado;
    5. encola en la salida y guarda el porqué de cada acción;
    6. **derivar** manda el mensaje fijo, pasa a Atención manual y marca la conversación «Necesita una persona», en la cabecera, en la lista y con su filtro. La marca se quita cuando responde una persona;
    7. en Atención manual, la respuesta redactada se le ofrece al operador como borrador.
  - **Ofrecer formularios:** hasta el tope que elige la organización por conversación y día (2 por defecto, con un máximo de la plataforma; 66), en la zona de la organización; solo los publicados, activos y con «Cuándo lo ofrece», que son para ese tipo de destinatario (50) y, a un empleado, que puede completar. El prellenado propone texto, números, fechas, sí/no y una opción ofrecida, nunca fotos, documentos ni «elegir varias».
  - **Sugerencias entre formularios (s1–s4):** `formsuggestions.manage`, hasta 20 por formulario. Modo «La IA decide» (Assistant) o «Preguntar siempre» (Always, por condición y sin IA), con mapeos entre preguntas del mismo tipo; una elección solo se mapea entre preguntas del mismo catálogo. Tienen guardado e historial propios, y la respuesta conserva su origen y la cadena.
  - **Audios (H6):** se bajan como los archivos de la E14 y se transcriben por un puerto del núcleo (27). La transcripción la ve también el operador; sin ella, el audio queda para el operador.
  - **Armar una regla desde un texto** (63): en el editor de reglas de la E16, la persona escribe la regla en palabras. La IA la convierte en una regla normal (cuándo, si, hacer), y la persona la revisa, la corrige y la guarda con «Guardar reglas» y `formrules.manage`. Después corre siempre igual, sin IA. La IA solo propone: nunca guarda una regla. El costo entra en el tope de IA de la organización (ADR 0051): con el tope agotado, «Armar desde un texto» queda grisado, con el motivo en su tooltip, y la regla se arma a mano.
  - **Preguntas sin respuesta** (67): lo que preguntaron sobre la organización y no estaba cargado queda en una lista agrupada por tema, sin nombres ni mensajes, con cantidad de preguntas y de personas, como «Formularios que faltan». Quien tiene «Administrar WhatsApp» la ve, completa el conocimiento desde ahí o descarta el tema. Así el asistente contesta más con el uso.
  - **Formularios que faltan (H7):** pedidos agrupados por tema, sin nombres ni mensajes. Quien diseña formularios lo crea o descarta el pedido.
  - **Catálogo propuesto (H4):** se pide desde el chat administrativo, solo con filtros. Queda inactivo hasta que lo apruebe una persona con `formcatalogs.manage`.
  - **Desde el primer día** (lo que allá costó arreglar):
    - **la espera:** los cierres del motor avisan por una señal local al confirmar la UoW, sin contenido; la entrada sondea cada 2 s solo mientras hay algo pendiente; el webhook despierta la captura. Una sola cola durable, sin colas nuevas. Allá bajó de 35 s a 6,6 s;
    - **el aislamiento por conversación:** `ChannelConversationId`, opaco, en el contexto, la huella, la recuperación y la cancelación, con el índice `(TenantId, OwnerUserId, ChannelId, ChannelConversationId)`. En Atención manual, el ámbito es el contacto. Se prueba con tres personas a la vez;
    - **la medición:** de texto a envío y de audio a envío, con el mismo arnés, desde la primera corrida.
  - **Costos:** el modelo, la transcripción y Probar entran en el tope mensual de IA de la organización (ADR 0051). Con el tope agotado, el asistente no está disponible y la conversación sigue en Atención manual.
- **Front:**
  - la pantalla «Asistente» en el panel de WhatsApp, con las pestañas Perfil, Conocimiento, Casos y Límites, y «Probar» al costado;
  - el selector de modo del chat, la marca «Necesita una persona» y el porqué de cada acción;
  - la sugerencia y su diálogo;
  - «Cuándo lo ofrece el asistente» en el editor;
  - «Qué sugerir después» en el editor;
  - la lista de pedidos.
- **Tableros:**
  - **Diseño dibuja nuevos** (evaluación, §5.2), con nuestras piezas y la decisión 43:
    - `AS1-A` · Pestañas: Perfil, Conocimiento, Casos y Límites, con «Probar» al costado y un solo Guardar;
    - `AS2` · Los casos: los propios, el bloque de formularios y «Cualquier otra cosa», con el editor al costado;
    - `AS3` · Probar el asistente: las acciones en orden, cada una con su caso y su porqué, y «No se mandó nada»;
    - `AS4` · El chat: el porqué, derivar y «Necesita una persona», el texto exacto, los botones, el tope del día y el borrador en Atención manual;
    - `AS5` · Límites y conocimiento: el conocimiento se edita ahí, sección por sección (46);
    - `AS6` · El circuito de punta a punta;
    - `WA-Modo-Atencion` · El modo de atención (52): por bandeja, quién atiende los chats nuevos y cuándo vuelve un chat; en el chat, cambiar el modo y fijarlo;
    - `WA-Preguntas-Sin-Respuesta` · las preguntas sin respuesta por tema, con «Completar el conocimiento» y «Descartar» (67);
    - en `AS5`, los límites por bandeja (65) y el tope de ofrecimientos (66).
  - **Diseño redibuja** `FRM-Editor-Asistente`, sin «Textos en» ni «Cambios sin guardar».
  - **Se quedan** los aprobados en el lienzo v133: `WA-Chats-Asistente`, `WA-Chats-Sugerencia`, `WA-Chats-Audio` (corregido en la v134), `FRM-Faltan`, `FRM-Respuesta-Cadena`, `M-WA-Conversacion-Asistente` y `M-FRM-Faltan`. `FRM-Catalogo-Propuesta` se redibuja con la E13, y `FRM-Editor-Sugerencias`, con la E16.
  - **Mandan sobre este resumen en:**
    - **con los Casos, una consulta ya no queda sin respuesta:** la contesta un caso o se deriva. `AS4` reemplaza el estado «Sin respuesta del asistente · queda para un operador» de `WA-Chats-Asistente`;
    - **«Pasar a modo Asistente»** pide «Administrar WhatsApp» (28), en Internos y en Clientes (51). «Tomar atención manual» pide solo el permiso de atender;
    - **«La IA decide»** es el modo Assistant y **«Preguntar siempre»** es Always: por condición, sin IA;
    - **«No pudimos pasar este audio a texto»** cubre también el tope de IA de la organización agotado;
    - **«Aprobar y activar»** pide `formcatalogs.manage`, y **«Formularios que faltan»** pide `forms.manage`;
    - **un audio no se ve «borrado» dentro de un chat que existe:** el audio y su transcripción siguen la retención del chat, la D4 de la E11 (se guardan mientras exista el contacto).

## Decisiones tomadas (no se vuelven a preguntar)

- **Casos** (39): contesta lo que configure cada organización, redacta dentro de sus límites y deriva lo demás.
- **La IA nunca guarda ni confirma una respuesta:** la confirma la persona en el formulario.
- **La configuración vive en nuestra base,** por organización, nunca en el panel del proveedor de IA.
- **Genérico:** ninguna respuesta queda escrita en el código.
- **Un solo botón, «Abrir formulario»,** también en las sugerencias.
- **Los catálogos del asistente** son solo con filtros, nunca SQL, y siempre los aprueba una persona.
- **Un modelo se habilita solo con un perfil calificado,** y los casos de evaluación se corren antes.
- **«Tomar atención manual»** siempre se puede, con el permiso de atender.
- **Empleados y clientes** (51), con el modo de atención configurable (52), y **«Administrar WhatsApp»** (28), sin permiso nuevo para los Casos.
- **El conocimiento** es una pestaña de la pantalla del asistente, sin pantalla ni permiso aparte (46).

## Fuera de esta etapa

- Varios asistentes y quién contesta cada chat: van en la E18b (68 y 69). La E18 guarda el asistente como «el agente principal», una fila, para que la E18b sume filas sin rehacer nada.
- Herramientas que escriben (piden una ADR nueva).
- Leer fotos y documentos (E19).
- El borrador de un formulario armado por la IA, salvo «Armar formulario con la IA» de la E19.
- B2C.

## Cómo se cierra

- **La puerta de la etapa,** con la lista de control de `/puerta`.
- **Pruebas de arquitectura:** nadie llama al modelo fuera de las piezas del asistente, y las herramientas no reciben `IUnitOfWork`.
- **Casos de evaluación** corridos antes de habilitar el modelo: consultas con y sin la información cargada, fuera de tema, saludo y hora, derivación e instrucciones escondidas en el mensaje o en el conocimiento.
- **Con Docker:**
  - el tope diario de ofrecimientos y el de respuestas;
  - una respuesta tardía después de tomar el chat;
  - propuestas con valores no permitidos y un texto que no pasa el validador;
  - un permiso revocado;
  - bucles de sugerencias;
  - la IA no disponible;
  - una ráfaga de tres mensajes con tres acciones;
  - tres personas escribiendo a la vez, sin mezclar historiales;
  - el modo de atención: un chat nuevo arranca según su bandeja, uno tomado vuelve solo después del tiempo configurado, y uno fijado no vuelve.
- **La medición** de texto a envío y de audio a envío.
- **Prueba real con la IA y Meta:**
  - «hola» y «¿qué hora es?» reciben una respuesta con sentido;
  - una consulta con la información cargada se responde, y una sin información deriva;
  - «se rompió la heladera» manda el formulario;
  - «quiero hablar con alguien» deriva y aparece en la lista;
  - un audio transcripto;
  - un cliente en la bandeja Clientes recibe una respuesta y un formulario para clientes.

## Requisitos previos

- La E12 a la E17 cerradas: la cola marcada con su origen (E16) y la bajada de medios (E14).
- **La ADR de respuestas redactadas,** antes de planificar.
- **Dos piezas que se planifican con la etapa:**
  - el asistente con acciones con confirmación (ADR 0048, D8);
  - la IA en WhatsApp, que la ADR 0038 dejaba para después.

## Ajustes ya decididos

- **Idioma** (35): perfil, casos, «Cuándo lo ofrece», pedidos y temas son un solo texto. El asistente responde en el idioma de la conversación.
- **Pantallas:** se copian de ArquitecturaBase (decisión 18), con los botones de la decisión 43.
- **Audios** (27): con un puerto de transcripción del núcleo, la clave en user-secrets y el costo dentro del tope de IA de la organización. El proveedor, con su ADR al planificar.

## En el multitenant

- **Tope de IA por organización (ADR 0051):** el costo del modelo, de la transcripción y de Probar entra en el tope mensual de la organización.
- **Datos de la organización:** el agente, sus casos y límites, el conocimiento, la marca «Necesita una persona», el resultado de cada acción, las sugerencias y los pedidos son `ITenantOwned`, con RLS.
- **El historial** nunca es de otra conversación ni de otra organización, con su test de aislamiento.
- **Workers:** entran con `ExecutionActor`, que usa `ITenantScope` por dentro.
- **Features:** `forms` y `assistant` prendidos en la organización.
- **Permisos de organización:**
  - `formsuggestions.manage`;
  - configurar los Casos y pasar a modo Asistente piden «Administrar WhatsApp» (`whatsapp.manage`), sin permiso nuevo (28 y 39);
  - los pedidos se ven con `forms.manage`.
- **WhatsApp (E11):** el asistente actúa en el número de la organización, en Internos y en Clientes (51).
- **Almacenamiento (E10):** los audios siguen la retención de los mensajes de WhatsApp: en una organización, mientras exista el contacto (D4 de la E11).
