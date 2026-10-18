# El asistente en WhatsApp, sugerencias y audios (resumen)

**Estado:** resumen del 2026-10-17, armado desde el [traspaso](../2026-10-17-formularios-traspaso/README.md). Corresponde a la etapa 5 de ArquitecturaBase, que tiene el diseño elegido y todavía no se programó (antes se llamaba F7).
- **Se diseñó el 2026-10-18** (lienzo v133), después de los tableros de la E16, y **se programa después del bloque** (decisión 19 de la hoja de ruta).
- No tiene número de etapa todavía. Se planifica con `/plan-etapa` cuando le toque.

**Dónde está el detalle:**
- en el traspaso: [04 §5](../2026-10-17-formularios-traspaso/04-etapas.md), [05 · Etapa 5](../2026-10-17-formularios-traspaso/05-pantallas.md), anexo C §2 (etapa 5) y anexo D (H1–H7, s1–s4 y 03);
- en ArquitecturaBase: la ADR 0020 (motor único de IA) y `docs/features/inteligencia-artificial.md`.

## Qué se construye

- **Back:**
  - **Las piezas de la IA:** el asistente de la 8b (ADR 0048) suma canales para WhatsApp: el modo Asistente, la sugerencia al operador y el chat administrativo.
    - Forms y WhatsApp aportan **herramientas** que solo leen o proponen, siguiendo la [guía de herramientas](../../guides/agregar-una-herramienta-al-asistente.md).
    - Ejecuta siempre el módulo dueño, con su servicio, su UoW y su cola.
  - **Modo Asistente por conversación (H1, H3):**
    - lee el mensaje, reconoce el formulario por su «Cuándo lo ofrece el asistente» y lo manda prellenado por la cola existente, marcado como del asistente;
    - deja anotado por qué lo mandó y de dónde salió cada dato;
    - «Tomar atención manual» lo corta.
  - **En atención manual (H2):** una sugerencia arriba del compositor, con los prellenados que se pueden destildar. Manda el operador.
  - **Límites (H5):**
    - si duda, pregunta con botones;
    - lo que no es un formulario no se contesta y queda sin leer para un operador;
    - hasta 2 ofrecimientos por día y por conversación, en la zona de la organización;
    - solo a cuentas activas, con permiso y dentro de las 24 horas;
    - un formulario que la persona no puede completar se trata como inexistente.
  - **Sugerencias entre formularios (s1–s4):** origen, destino, «cuándo», condición opcional, modo Assistant (decide la IA) o Always (por condición, sin IA) y mapeo de preguntas del mismo tipo. Las respuestas guardan su origen.
  - **Catálogo propuesto (H4):** se pide desde el chat administrativo, solo con filtros. Queda inactivo hasta que lo apruebe una persona con `formcatalogs.manage`.
  - **Audios (H6):** se bajan como los archivos de la E14 y se transcriben por un puerto del núcleo (`Application/Interfaces/Integrations`), como el transporte del asistente. La transcripción la ve también el operador. El proveedor se elige con su ADR al planificar (decisión 27).
  - **Formularios que faltan (H7):** pedidos agrupados por tema, sin nombres ni mensajes. Quien diseña formularios lo crea o descarta el pedido.
- **Front:**
  - el selector de modo del chat;
  - la sugerencia y su diálogo;
  - el campo «Cuándo lo ofrece el asistente» en el editor (tablero 03);
  - las sugerencias en el editor;
  - la lista de pedidos.
- **Tableros:** H1 a H7, s1 a s4 y 03 de ArquitecturaBase (decisión 18), **aprobados en el lienzo v133** (2026-10-18, front `52b08ccf`):
  - **`WA-Chats-Asistente` (H1, H3 y H5), en Internos:**
    - en la cabecera, «Asistente» o «Atención manual», y la marca «Asistente» en la lista;
    - «Pasar a modo Asistente» va en el ⋮ del chat, con un diálogo de confirmación;
    - la burbuja «Asistente» lleva el formulario prellenado y «Por qué lo mandó»: qué reconoció, qué propuso y de dónde salió cada dato;
    - mientras atiende el asistente, en lugar del compositor va «Tomar atención manual»;
    - casos: pregunta con botones si duda, una consulta queda «Sin respuesta del asistente · queda para un operador», «ahora no», no hay formulario (anota el pedido), fuera de las 24 horas, tomar a mitad de un envío (ese envío termina) y asistente no disponible (sigue en atención manual).
  - **`WA-Chats-Sugerencia` (H2):** «El asistente sugiere…» arriba del compositor, con «No, gracias» y «Revisar y enviar». «Revisar y enviar» abre el envío de la E12 con «Datos que van prellenados» para destildar.
  - **`WA-Chats-Audio` (H6):** el audio con su transcripción, que ve también el operador. Estados: pasando a texto, transcripto, leído por el asistente, sugerencia en atención manual, «No pudimos pasar este audio a texto…» (queda sin leer para un operador) y borrado.
  - **`FRM-Catalogo-Propuesta` (H4):** la pantalla del catálogo con filtros de la E13, marcada «Propuesta del asistente», con quién la pidió y cuándo. Tiene «Descartar propuesta» y «Aprobar y activar»; lo que no se pudo filtrar se avisa debajo de la vista previa.
  - **`FRM-Faltan` (H7):** «Formularios que faltan», en el panel de Formularios y en Más › Formularios.
    - Por cada tema: la cantidad de pedidos y de personas y el último pedido, sin nombres ni mensajes.
    - «Estado ▾» (pendientes o descartados), y en el ⋮, «Crear formulario» y «Descartar».
  - **`FRM-Editor-Asistente` (03):** «Cuándo lo ofrece el asistente» en «El formulario» del editor. Sin ese texto, el asistente no lo ofrece.
  - **`FRM-Editor-Sugerencias` (s2):** «Qué sugerir después», debajo de las reglas.
    - Tiene «Guardar sugerencias» propio.
    - Cada sugerencia lleva el formulario, «La IA decide» o «Preguntar siempre», el «Cuándo» y los datos que pasan, solo entre preguntas del mismo tipo.
  - **`FRM-Respuesta-Cadena` (s4):** en D2, «La mandó el asistente · la confirmó …», y en Actividad, «Cómo empezó» y «La cadena».
  - **Teléfono:** `M-WA-Conversacion-Asistente` y `M-FRM-Faltan`.
  - **Mandan sobre este resumen en:**
    - **«Pasar a modo Asistente»** pide «Administrar WhatsApp» (decisión 28) y solo aparece en Internos (decisión 29). «Tomar atención manual» pide solo el permiso de atender;
    - **«La IA decide»** es el modo Assistant y **«Preguntar siempre»** es Always: por condición, sin IA;
    - **«No pudimos pasar este audio a texto»** cubre también el tope de IA de la organización agotado: la transcripción va por el puerto del núcleo y su costo entra en ese tope (decisión 27, ADR 0051);
    - **«Aprobar y activar»** pide `formcatalogs.manage`, y **«Formularios que faltan»** pide `forms.manage`;
    - **el estado «borrado» de un audio choca con la regla D4 de la E11:** el tablero dice «se borran a los 90 días, como el texto de los mensajes». Acá el texto de los chats de una organización se guarda mientras exista el contacto, y los 90 días son solo de la copia global de la entrada ([WhatsApp](../../features/whatsapp.md), «Retención de la atención»). Se lo avisé a la Guía el 2026-10-18; hasta que decida, manda la D4 (condición de la §5) y ese estado no se programa.

## Decisiones tomadas (no se vuelven a preguntar)

- **La IA nunca guarda ni confirma una respuesta:** la confirma la persona en el formulario.
- **Un solo botón, «Abrir formulario»,** también en las sugerencias.
- **Los catálogos del asistente** son solo con filtros, nunca SQL, y siempre los aprueba una persona.
- **Nada exige traducciones:** el asistente responde en el idioma de la conversación.
- **Un modelo se habilita solo con un perfil calificado,** y los casos de evaluación se corren antes.
- **Sugerencias:** tienen guardado e historial propios, sin republicar, y sin circuitos ni estados de proceso.
- **«Tomar atención manual»** siempre se puede, con el permiso de atender.

## Fuera de esta etapa

- El borrador de un formulario armado por la IA.
- Responder consultas generales.
- Herramientas que escriben (piden un ADR nuevo) e imágenes como entrada.
- B2C: formularios para clientes.

## Cómo se cierra

- **Pruebas de arquitectura:** nadie llama al modelo fuera de las piezas del asistente, y las herramientas no reciben `IUnitOfWork`.
- **Casos de evaluación** corridos antes de habilitar el modelo.
- **Con Docker:**
  - el tope diario;
  - una respuesta tardía después de tomar el chat;
  - propuestas con valores no permitidos;
  - un permiso revocado;
  - bucles de sugerencias;
  - la IA no disponible.
- **Prueba real con la IA y Meta:**
  - un audio transcripto;
  - la visita en inglés, con y sin traducción.

## Requisitos previos

- La E12 a la E16 cerradas: la cola marcada con su origen (E16) y la descarga de medios (E14).
- **Dos piezas que no están planificadas:**
  - el asistente con acciones con confirmación (ADR 0048, D8);
  - la IA en WhatsApp, que la ADR 0038 deja para después.

  Se planifican antes de programar el asistente.

## Ajustes ya decididos

- **Idioma:** «Cuándo lo ofrece», los pedidos y los temas se escriben en un solo idioma.
- **Pantallas:** se copian de ArquitecturaBase (decisión 18).
- **Audios:** con un puerto de transcripción del núcleo, la clave en user-secrets y el costo dentro del tope de IA de la organización. El proveedor, con su ADR al planificar (decisión 27).
- **Modo Asistente:** lo activa quien tiene «Administrar WhatsApp» (decisión 28).
- **Bandeja:** solo Internos (decisión 29).

## En el multitenant

- **Tope de IA por organización (ADR 0051):** el costo del modelo y de la transcripción entra en el tope mensual de la organización.
- **Datos de la organización:** sugerencias, pedidos y lo que anota el asistente son `ITenantOwned`, con RLS.
- **Workers:** entran con `ExecutionActor`, que usa `ITenantScope` por dentro.
- **Features:** `forms` y `assistant` prendidos en la organización.
- **Permisos de organización:**
  - `formsuggestions.manage`; pasar a modo Asistente pide «Administrar WhatsApp» (`whatsapp.manage`), sin permiso nuevo (decisión 28);
  - los pedidos se ven con `forms.manage`.
- **WhatsApp (E11):** el asistente actúa en el número de la organización y, solo en Internos (decisión 29).
- **Almacenamiento (E10):** los audios siguen la retención de los mensajes de WhatsApp: en una organización, mientras exista el contacto (D4 de la E11).
