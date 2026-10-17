# 04 · Etapas

Una sección por etapa, con lo necesario para replicarla:
- objetivo y tableros;
- qué se construye en backend y front;
- decisiones clave;
- cómo se cerró;
- qué cambia en multitenant.

Las reglas completas, con fecha y fuente, están en el [anexo C §2](anexo-c-reglas.md#2-reglas-y-decisiones-por-etapa); las clases, tablas y rutas exactas, en el [anexo A](anexo-a-backend.md); y las pantallas, en [05](05-pantallas.md) y el [anexo B](anexo-b-front.md).

**Orden en ArquitecturaBase:** 1 → 2 → 2B → 3 → 4 → 5. Cada etapa necesita la anterior: los catálogos usan el envío de la 1; la planilla nace mostrando archivos (2B); el PDF necesita el almacenamiento ya probado (2B) y la gramática de condiciones (2); el asistente necesita la cola con `Origin`/`Kind` (4) y la descarga de medios (2B).

**Equivalencia con la hoja de ruta de acá:**

| ArquitecturaBase | Multitenant |
| --- | --- |
| 1 | F1 |
| 2 | F2 |
| — | F3, la biblioteca de plantillas (nueva) |
| 2B | F4 |
| 3 | F5 |
| 4 | F6 |
| 5 | F7 |

---

## Etapa 1 · Crear, publicar, mandar y recibir

**Objetivo.** El recorrido mínimo completo:
1. crear, guardar y descartar un formulario con seis tipos de pregunta (texto corto, texto largo, número con decimales, fecha, sí/no y elección de una lista manual);
2. publicarlo como WhatsApp Flow;
3. mandarlo desde el chat;
4. capturar la respuesta numerada y mostrarla en el chat.

**Tableros.** Se entregó en cuatro incrementos:
1. Listado, alta, prefijo, conflicto e idiomas.
2. Publicar, publicando, resultado y vista.
3. Elegir, mensaje, cambios y estado del envío desde el chat (C1–C5).
4. Llega la respuesta, quién la ve y cómo se muestran los valores (R1–R3).

**Qué se construye**

- **Modelo:**
  - **Definición** editable, con nombre (mapa por idioma), idioma original inmutable, prefijo de numeración y botón final.
  - **Borrador** con `DraftRevision` y esquema JSON `format=1`, que lleva solo las preguntas.
  - **Versión** inmutable al publicar, con `ContentHash`, su presentación sellada (nombre, botón, idioma, cantidad de preguntas) y las identidades registradas en `BusinessFormFields`.
  - Sin una tabla por formulario ni una columna por pregunta: JSON canónico más una proyección tipada.
- **Identidad:**
  - El servidor genera `FieldId` y `ChoiceId` (Guid v7) al guardar y devuelve el mapa de los IDs provisionales del navegador.
  - Renombrar, traducir o reordenar conserva los IDs. Cambiar el tipo crea otra identidad (`replacesFieldId`).
  - Las preguntas retiradas quedan en un ledger y no resucitan.
- **Guardar el borrador:**
  - con UUID de pedido y huella del cuerpo: el mismo pedido devuelve el mismo resultado y otro cuerpo con el mismo UUID da 409;
  - revisión esperada y 409;
  - recuperación del resultado durante 30 días.

  Ojo: con el ADR 0018 posterior, hoy la configuración interna se haría solo con revisión y 409, sin recuperación de 30 días; ArquitecturaBase la conservó porque ya estaba hecha.
- **Publicar:**
  - **Chequeo local:** compara con lo guardado; con cambios sin guardar, 409 `DraftMustBeSaved`. Valida idiomas y límites y devuelve un `checkToken` de 10 minutos.
  - **Aceptación durable:** crea la versión y la operación y bloquea el prefijo; responde 202.
  - **Worker con lease/fence:** Create → Upload → Validate → Publish → Verify en Meta, con la verificación de que el número pertenece al WABA.
  - **Estados:** Accepted → Preparing → Publishing → Uncertain o ActivationPending → Published o Failed. Una sola operación abierta por definición.
  - **Cada versión crea un Flow físico nuevo,** uno por idioma.
- **Preview oficial** de Meta por idioma, como consulta explícita, `no-store`.
- **Envío desde el chat:**
  - lista los formularios que la cuenta vinculada al contacto puede completar: activa, con idioma y con su propio `formrecords.create`;
  - una sesión cifrada fija contacto, cuenta, versión, idioma, binding y vencimiento (24 h);
  - la salida queda en la cola durable de WhatsApp: mensaje interactivo de tipo flow, `flow_token` aleatorio y texto acompañante obligatorio de hasta 1024 caracteres.
- **Captura:**
  - el webhook reconoce `nfm_reply` y guarda la respuesta cifrada; un worker independiente la procesa;
  - el mapping sellado define claves, tipos y opciones;
  - `BusinessFormRecordRecorder` vuelve a autorizar, numera `PREFIJO-NNNNNN` bajo lock con índice único y graba recibo, registro y valores tipados en una transacción;
  - el recibo con huella hace idempotente el replay.
- **Inmutabilidad:** una respuesta confirmada no se edita nunca. Una corrección futura será una respuesta sucesora.
- **Lectura en el chat:** los valores requieren `formrecords.read`; la lista de chats muestra un texto fijo.
- **Front:** listado, alta, editor con idiomas, diálogo de publicar con su estado, diálogo de envío en el chat y tarjeta de respuesta.

**Decisiones clave:**
- **Numeración:** por prefijo (`VIS-000024`), editable hasta la primera publicación, única y no reciclable.
- **«Abrir formulario»:** siempre es el botón del mensaje, distinto del botón final del formulario.
- **Publicar compila solo lo guardado** (D4).
- **Idiomas (D5):** todos los textos en es/en para publicar. Esto se reemplaza por la regla nueva: ver [07](07-pendientes.md).
- **Permisos de dos segmentos:** `formtemplates.read/manage/publish` y `formrecords.create/read`.
- **Sin respuesta automática al empleado** en esta etapa: el operador contesta a mano.

**Cierre.** Piloto real con Meta: `PRB1010-000001` con los seis tipos. Backend 2685 tests, front 1455. El usuario cerró la etapa con deuda visual declarada.

**En multitenant (F1):**
- **Canal:** cada organización publica en su WABA con el token de E11, y el envío sale de la bandeja de su número. El webhook es global y el worker resuelve la organización por el número y captura dentro de `ITenantScope.Enter(tenantId)`, con el feature `forms` habilitado.
- **Destinatarios:** miembros de la organización con cuenta vinculada y permiso de completar.
- **Idioma:** empezar directamente con la regla nueva (solo el idioma original obligatorio).
- **Robustez:** ADR 0018 desde el principio. El guardado del borrador con revisión y 409, sin la recuperación de 30 días.

---

## Etapa 2 · Catálogos Manual y Filters

**Objetivo.** Listas compartidas entre formularios, escritas a mano (Manual) o tomadas de los datos del sistema con filtros (Filters), usadas por la pregunta «Elegir de un catálogo».

**Tableros:**
- **K1** lista;
- **K2** alta;
- **K3** Manual;
- **K4** Filters;
- **K6** la pregunta;
- **K7** enviar y responder.

K5 (consulta SQL) quedó afuera por el ADR 0019.

**Qué se construye**

- **Catálogo:**
  - nombre y descripción (es/en), origen fijo (otro origen es otro catálogo), estado Activo, «Dado de baja» o «No se puede usar» (con motivo), revisión y 409;
  - se da de baja y se reactiva desde su pantalla, con `isActive` en el PUT;
  - permisos `formcatalogs.read` y `formcatalogs.manage`.
- **Manual:**
  - cada ítem tiene texto, detalle y código; el español es obligatorio, el inglés opcional y el código no se muestra en WhatsApp;
  - las posiciones las asigna el servidor (clave única diferida en PostgreSQL); reordenar es mover por `itemId` dentro del mismo guardado;
  - dar de baja un ítem no lo borra;
  - **límites validados al guardar: 30 caracteres de texto y 300 de detalle por idioma.**
- **Filters:**
  - un SELECT fijo del lector del módulo sobre columnas permitidas del origen Usuarios (id, nombre, roles, activo; nunca correo ni códigos), con condiciones de la gramática común y siempre con parámetros;
  - el texto de la opción es siempre el nombre; el filtro por rol guarda el Id del rol;
  - vista previa «como si lo recibiera», con una persona de ejemplo, que marca quién no entra en los límites de WhatsApp.
- **Pregunta de catálogo (K6):**
  - `choice` con `source.catalog`: la versión guarda solo la referencia, y cambiar el catálogo crea otra identidad;
  - publicar bloquea referencias inexistentes o inactivas;
  - el Flow usa un Dropdown con `data-source` dinámico (perfil r5).
- **Enviar y responder (K7):**
  - al mandar, se preparan las opciones para ese destinatario, en su idioma, y la **oferta queda sellada en la sesión**; viaja en `flow_action_payload.data`;
  - **no se puede mandar** con más de 200 opciones o sin ninguna en una obligatoria; nunca se trunca;
  - al responder, solo se aceptan opciones de la oferta. Una opción Manual dada de baja rechaza **toda** la respuesta, sin número, con un aviso que nombra la pregunta (no el valor) y «Mandar de nuevo»;
  - se guarda la referencia con catálogo, revisión, Id y el texto y detalle históricos; Filters no vuelve a consultar el origen al capturar;
  - al operador le llega solo la cantidad de opciones por pregunta, nunca los nombres.

**Decisiones clave:**
- **Ítems sin inglés:** se muestra el español.
- **Rechazo completo** por opción dada de baja.
- **Nunca se trunca.**
- **Orígenes:** los agrega desarrollo, no la pantalla ni la IA.
- **Guardar es configuración interna:** revisión y 409.
- **ADR 0019:** sin SQL configurable por ahora.

**Cierre.** Piloto real con Meta:
- Manual y Usuarios: `PILK7-000001…000003`.
- Rechazo por baja y reenvío sin salto de numeración.
- 200 opciones con 136 caracteres de detalle cada una: `PIL200-000001`.

**En multitenant (F2):** el origen Filters son los miembros, roles y empresas **de la organización**, nunca de otra, siempre dentro del alcance RLS. Lo demás, igual.

---

## Etapa 2B · Fotos y documentos

**Objetivo.** Preguntas que piden archivos desde el celular, guardados privados.

**Tableros:**
- **F1** el editor;
- **F2** el celular;
- **F3** la respuesta llega con archivos;
- **F4** ver y descargar;
- **F5** límites y casos.

**Qué se construye**

- **Tipos «Foto» y «Documento»:**
  - **Foto:** «Cámara o galería» o «Solo cámara».
  - **Documento:** PDF, Excel, Word, JPEG y PNG.
  - Mínimo y máximo por pregunta; obligatoria es mínimo 1.
  - **Suma de máximos hasta 10** (límite de Meta: 10 archivos y 100 MiB por respuesta).
  - Tope por archivo de la instalación, hasta 25 MiB, **sellado al publicar**.
  - Sin almacenamiento, los tipos se ven pero no se eligen.
- **Compilador:** PhotoPicker y DocumentPicker, una pregunta de archivo por pantalla (IDs de pantalla de solo letras y guiones bajos, perfil r7) y el valor como propiedad de `complete`.
- **Captura con archivos:**
  - el webhook conserva `id`, `file_name`, `mime_type` y `sha256`;
  - el worker baja de la API de medios y verifica hash, tamaño y tipo real (con límites de dimensión antes de decodificar);
  - arma la miniatura, sube con clave determinística y confirma todo junto con la respuesta;
  - **si algo falla, nada se guarda a medias:** rechazo con motivo y «Mandar de nuevo»;
  - plazo de 90 segundos por captura y reintentos espaciados.
- **Acceso:**
  - miniatura y original solo por la Api, con `formrecords.read` y pertenencia;
  - `no-store` y `nosniff`; documentos siempre como descarga;
  - nombre de descarga con la extensión del tipo verificado;
  - cupo propio de 600 lecturas por minuto.
- **Limpieza de huérfanos** cada hora. Los archivos siguen a la respuesta, no a los 90 días del chat.
- **Front:**
  - en el editor, los tipos nuevos y el contador «Archivos: hasta N de 10»;
  - en el chat, la tarjeta «bajando», guardada (con miniaturas y «+N») o rechazada;
  - el visor en el diálogo común, con anterior, siguiente y descarga.

**Decisiones clave:**
- **Lecturas idempotentes con reintento simple:** bajar de Meta y subir con clave determinística son lecturas idempotentes, sin protocolo durable.
- **Sin CSV, video ni comprimidos.**
- **Antivirus antes de producción.**
- **Azurite** en desarrollo y Azure real en el piloto.

**Cierre.** Piloto con Meta y Azure reales: `FOTO2B-000001` con cámara, PDF y una pregunta opcional vacía. **Meta omite la propiedad de la opcional vacía.** El piloto encontró dos incompatibilidades (IDs de pantalla, ID de medio numérico) que ningún test con dobles había detectado.

**En multitenant (F4):** el adaptador de nube de E10, aislado por organización, y la retención de archivos atada a la respuesta de esa organización.

---

## Etapa 3 · Planilla, detalle y exportar a Excel

**Objetivo.** Ver y analizar las respuestas. **Esta etapa solo lee.**

**Tableros:**
- **T1** planilla, con la columna de fotos y documentos elegida el 2026-10-16;
- **T2** filtros;
- **T3** detalle, con galería;
- **T4** estados;
- **T5** exportar.

**Qué se construye**

- **Panel «Respuestas»:** un enlace por formulario publicado que el usuario puede ver, dado por el servidor (`GET /api/forms/navigation`), con «Ver todos». Nada queda fijo en la navegación del front.
- **Planilla:**
  - **Columnas fijas:** número, recibida, y cargó (persona y canal).
  - **Por pregunta:** una columna por pregunta (FieldId) de todas las versiones, en el orden de la versión activa y después las retiradas.
  - **Valores especiales:** «—» si no existía en esa versión; «Sin respuesta» si quedó vacía. Cero y `false` se distinguen.
  - **Archivos:** miniatura y cantidad, que abre el visor.
  - **Ancho y columnas:** se desplaza a lo ancho, y «Columnas» (que vive en la URL) oculta las que sobran.
- **Filtros:**
  - AND de condiciones por FieldId, con operadores por tipo: contains/equals, equals/gte/lte, in, answered/unanswered/notAsked;
  - más persona, versión y rango de recepción;
  - **texto, personas y condiciones van por POST en el cuerpo, nunca en la URL;**
  - los conteos por faceta se calculan con la misma definición de filtro, ignorando su propia dimensión, y conservan opciones históricas.
- **SQL:** filtrar y ordenar antes de contar y paginar; esquemas deserializados una vez por versión; orden por elección con un CASE parametrizado.
- **Detalle:** las preguntas de su versión, de dónde vino, historial, galería y anterior/siguiente con el mismo filtro y orden. Reemplazado después por D2 (ver [05](05-pantallas.md)).
- **Excel:**
  - `.xlsx` con los filtros aplicados y permiso `formrecords.export`;
  - **hasta 10.000** respuestas: una consulta de IDs ordenados e hidratación por lotes de 100, con un presupuesto de 30 segundos y una exportación a la vez por usuario;
  - números, fechas y booleanos como tipos de Excel; encabezados en el idioma de quien exporta;
  - de los archivos van la cantidad y los nombres.

**Decisiones clave:**
- **Sin migrar** las respuestas viejas.
- **«Estado»** llega con la aprobación, solo en formularios que la piden.
- **Permisos globales,** no por formulario.
- **Sin trabajos de fondo** para el Excel.

**Cierre.** Recorrido real con respuestas reales y el `.xlsx` abierto en Excel sin reparación. EXPLAIN sobre 10.001 respuestas.

**En multitenant (F5):** la planilla filtra además por empresa (decisión 1 de la hoja de ruta), y todas las lecturas van dentro de RLS.

---

## Etapa 4 · Reglas, aprobación, PDF y avisos

**Objetivo.** Qué pasa automáticamente cuando llega, se aprueba o se rechaza una respuesta.

**Tableros:**
- **G1** reglas;
- **G2** diseño del PDF, con «Incluir las fotos»;
- **G3** por aprobar;
- **G4** decidir;
- **G5** historial, con «Reintentar» y «Consultar»;
- **G6** lo que recibe el empleado.

**Qué se construye**

- **Reglas:**
  - **Guardado propio:** con revisiones inmutables (`BusinessFormRuleSets`) y su propio «Guardar reglas», revisión y 409; no crean versión ni republican.
  - **Cada regla:** evento (Recibida, Aprobada o Rechazada), **hasta 10 condiciones AND** con la gramática de la planilla y acciones ordenadas: pedir aprobación a un rol, armar el PDF, mandarle un mensaje a quien la cargó (con o sin el PDF) y avisar a las personas con un rol. Los textos llevan `{número}` y `{pregunta}`.
  - **Pedir aprobación es la última acción de su regla** y solo al recibir. **No hay PDF sin aprobar.**
  - Una condición sobre una pregunta que no existe en la versión cuenta como «sin respuesta».
- **Motor único de acciones:**
  - cada respuesta conserva la revisión de reglas con que llegó;
  - cada acción es una ejecución con clave respuesta + evento + regla + acción, lease y fence, con el efecto fuera de la transacción;
  - **lo confirmado nunca se repite** y lo incierto se consulta antes;
  - cada acción espera solo a la anterior de su regla; «No aplica» si la condición no se cumple;
  - la captura crea las ejecuciones en el mismo commit que la respuesta.
- **Aprobación:**
  - un nivel, asignado a un rol;
  - decide una cuenta activa con `formapprovals.decide` y que pertenece al rol, **nunca quien confirmó la respuesta**;
  - una sola tarea abierta por respuesta; decidir cierra la tarea y crea las acciones del evento en un commit; si otro decidió antes, 409;
  - **rechazar exige motivo** (hasta 500 caracteres);
  - «Por aprobar» va de la más vieja a la más nueva, con «Para mí» y «Todas».
- **PDF:**
  - diseño con revisiones propias (`BusinessFormPdfDesigns`): nombre original obligatorio, traducción opcional, qué preguntas incluye, quién aprobó, pie e «Incluir las fotos»;
  - al aprobar se fijan diseño, datos, etiquetas, número y decisión, y se genera un A4 con SkiaSharp y una fuente embebida;
  - se sube con clave por ejecución e idioma y escritura única: **es una copia fija**;
  - solo por la Api, con `formrecords.read`.
- **Avisos:**
  - por la **misma cola durable** de WhatsApp (`Origin=FormAction`), deduplicados por el Id de la ejecución;
  - ventana de 24 h verificada al aceptar y al despachar; fuera de ella, «No se pudo» (todavía no hay plantillas de Meta);
  - los documentos se suben a Meta antes de mandar.
- **Idioma:** si el idioma de quien recibe no está completo en los avisos o en el diseño, todo el evento sale en el original, y esa elección queda fijada.
- **Revisión humana de un aviso «Sin confirmar»** (agregada por el review): «salió» o «no salió», atribuida y durable.

**Decisiones clave:**
- **Un solo motor, sin segunda cola.**
- **La revisión de reglas** queda fijada al recibir.
- **El rol delimita** quién decide, pero no reemplaza al permiso.
- **Un nivel de aprobación.**

**Cierre.** Piloto en español con Meta y Azure reales: `VISITA-000001`. Llegó el aviso al recibir, la aprobó otra cuenta y el PDF con la foto se abrió en el celular. Las cuatro acciones quedaron en «Hecho». Pendientes: el inglés y la caída al original.

**En multitenant (F6):**
- **Por aprobar** se integra como `IPendingItemSource` (ADR 0040, «Para resolver») en lugar de un contador propio.
- **Roles** de la organización.
- **Avisos** por el número de la organización.

---

## Etapa 5 · Asistente, sugerencias y audios

**Objetivo.** Que el empleado cuente lo que hizo y reciba el formulario prellenado, sin que la IA guarde nunca nada.

**Tableros:**
- **H1** modo Asistente;
- **H2** sugerencia en atención manual;
- **H3** cambiar de modo;
- **H4** catálogo propuesto;
- **H5** límites;
- **H6** audios;
- **H7** formularios que faltan;
- **s1–s4** sugerencias entre formularios;
- **03** «Cuándo lo ofrece el asistente», en el editor.

**Qué se construye**
- **Motor único** (ADR 0020) con canales y capacidades tipadas de lectura o propuesta. Ejecuta el módulo dueño.
- **Modo Asistente:**
  - lee el mensaje, reconoce el formulario por su «Cuándo lo ofrece» y lo manda prellenado por la cola existente (`Origin` Assistant);
  - deja anotado por qué lo mandó y de dónde salió cada dato;
  - «Tomar atención manual» lo corta; pasar a Asistente pide `whatsapp.configureassistant`.
- **Atención manual:** sugerencia arriba del compositor, con un diálogo de prellenados que se pueden destildar. Manda el operador.
- **Límites:**
  - si duda, pregunta con botones;
  - una consulta o una charla no la contesta;
  - si alguien quiere registrar algo que no tiene formulario (o uno que no puede completar), le avisa y **anota el pedido** sin nombrar formularios ajenos;
  - hasta 2 ofrecimientos por día y conversación; solo cuentas vinculadas, activas y con permiso; dentro de las 24 h.
- **Sugerencias entre formularios:** origen, destino, «cuándo», condición opcional, modo Assistant o Always y mapeo de preguntas. Las respuestas guardan su origen.
- **Catálogo propuesto** desde el chat administrativo, solo Filters; queda inactivo hasta que lo aprueba una persona.
- **Audios:** se bajan como los archivos de la 2B y se transcriben con el mismo proveedor (OpenAI). La transcripción entra como texto y el operador ve el audio con su texto. Sin transcripción, el audio queda para el operador.
- **Formularios que faltan:** pedidos agrupados por tema, sin nombres ni mensajes. Ana crea el formulario o descarta el pedido (permiso `formtemplates.manage`).
- **Detalle D2:** se programa en esta etapa, antes del asistente.

**Decisiones clave:**
- **La IA nunca guarda ni confirma.**
- **Un solo botón,** «Abrir formulario».
- **Catálogos del asistente** solo Filters y aprobados por una persona.
- **Nada exige traducciones:** el asistente responde en el idioma de la conversación.
- **Modelos:** solo se habilitan con un perfil calificado; los casos de evaluación se corren antes.

**Cierre previsto.** Pruebas de arquitectura del ADR 0020; casos con Docker (tope diario, respuesta tardía tras tomar Manual, propuestas no permitidas, permiso revocado, bucles de sugerencias, IA no disponible); y un piloto real con la IA y Meta, con un audio transcripto y la visita en inglés con y sin traducción.

**En multitenant (F7):** el asistente de E8B, con el costo de la IA y la transcripción dentro del tope mensual de cada organización (ADR 0051). B2C viene después.
