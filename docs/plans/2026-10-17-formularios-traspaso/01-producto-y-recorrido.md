# 01 · Producto y recorrido

## Qué es

Un administrador arma un formulario una vez, en la web. Los empleados lo completan desde WhatsApp, sin entrar a la web. Cada envío queda guardado como una fila numerada de una planilla. Según las reglas del formulario, se pide aprobación, se arma un PDF y se avisa por WhatsApp. Un asistente con IA puede reconocer lo que el empleado cuenta y ofrecerle el formulario ya completado, pero nunca guarda nada solo.

No hay circuitos rígidos: cada formulario es independiente. Lo que se encadena es opcional y explícito, como las reglas al recibir, aprobar o rechazar, o la sugerencia de otro formulario.

## Quién lo usa

Los nombres son los de los tableros y del recorrido guiado; sirven para contar la misma historia en todos lados.

| Persona | Dónde trabaja | Qué hace |
| --- | --- | --- |
| **Ana**, administradora | Web | Arma formularios y catálogos, publica, define reglas y el diseño del PDF, elige qué chats atiende el asistente y ve los formularios que faltan. |
| **Operador** | Web, chat de WhatsApp | Manda un formulario a un contacto desde la conversación y ve llegar la respuesta. En atención manual recibe las sugerencias del asistente. |
| **Juan**, empleado | Su celular, WhatsApp | Abre el formulario dentro del chat, lo completa (con fotos y documentos si se piden) y lo confirma. Le llegan el aviso, el número y el PDF. |
| **Carla**, supervisora | Web | Ve la planilla, decide en «Por aprobar» y exporta a Excel. |
| **El sistema** | — | Publica en Meta, manda, captura y numera; baja y guarda archivos; corre las reglas; arma el PDF; manda los avisos. |
| **El asistente (IA)** | — | Lee el mensaje o el audio transcripto, reconoce el formulario, propone valores y pregunta si duda. Solo lee y propone: ejecuta siempre el módulo dueño. |

## El recorrido de una visita, de punta a punta

El ejemplo de todos los tableros es **Registrar visita**, con estas preguntas:
- Cliente, una elección de catálogo;
- Fecha de la visita;
- Observaciones;
- Importe cobrado, con decimales;
- Fotos de la obra, solo con la cámara;
- Remito firmado, un documento.

Las respuestas se numeran `VIS-000001`, `VIS-000002`…

### 1. Ana arma el formulario (Etapa 1, 2 y 2B)

1. En **Diseñar formularios** crea «Registrar visita» y elige el prefijo de numeración `VIS`. El prefijo se puede cambiar hasta la primera publicación.
2. **Agrega preguntas tipadas:** texto corto o largo, número, número con decimales, fecha, sí/no, elección (con opciones escritas o de un catálogo), foto y documento. Cada una puede ser obligatoria u opcional y llevar una ayuda.
3. **Catálogos (Etapa 2):** «Cliente» sale de un catálogo compartido, escrito a mano o tomado de los datos del sistema con filtros.
4. **Textos:** escribe en el idioma original del formulario. Hasta la Etapa 4 publicar exigía todos los textos también en el otro idioma; desde el 2026-10-16, lo nuevo exige solo el original, y lo existente cambia después de la Etapa 5 (ver [07](07-pendientes.md)).
5. **Guarda borradores:** el guardado tiene revisión y responde 409 si otra persona guardó antes.

### 2. Publica (Etapa 1)

1. **«Publicar en WhatsApp»** muestra qué incluye y qué va a pasar: la versión queda fija, se manda a WhatsApp y después se puede mandar desde el chat.
2. **El sistema compila el formulario a un WhatsApp Flow** (Flow JSON 7.3, con un perfil de compilación versionado: hoy r7) y lo publica en el WABA. WhatsApp lo valida; si una pregunta no se puede mostrar, se dice cuál. El estado va de «Publicando» a «Publicado» o «No se pudo», con el motivo.
3. **Cada publicación es una versión nueva e inmutable.** Las respuestas quedan atadas a la versión con la que llegaron. Cambiar el formulario nunca rompe lo ya cargado.

### 3. Lo manda (Etapa 1, 2)

1. **Desde el chat de WhatsApp de la web,** el operador abre la conversación de Juan y toca «Enviar formulario».
2. **El diálogo** muestra los formularios que esa cuenta puede completar, con un mensaje opcional y las opciones preparadas de cada catálogo. Bloquea el envío si:
   - un catálogo no se puede usar;
   - tiene más de 200 opciones;
   - una pregunta obligatoria no tiene opciones.
3. **Solo se puede mandar dentro de las 24 horas** desde el último mensaje de Juan (regla de WhatsApp). Y solo a una cuenta vinculada, activa y con permiso para completarlo.
4. **El envío queda en la cola durable de WhatsApp** con su sesión: las opciones ofrecidas quedan selladas en esa sesión.

### 4. Juan responde (Etapa 1, 2B)

1. En su WhatsApp ve el mensaje con el botón **«Abrir formulario»**.
2. **Completa las pantallas:**
   - una pregunta de archivo por pantalla;
   - las fotos con la cámara si así se pidió;
   - el remito desde los documentos del teléfono;
   - el resto de las preguntas.
3. **Toca el botón final, por ejemplo «Confirmar visita».** Hasta acá no se guardó nada.

### 5. Llega la respuesta (Etapa 1, 2, 2B)

1. **El webhook recibe la respuesta del Flow** (`nfm_reply`) y un worker la captura.
2. **Captura:**
   - valida contra la versión y contra la oferta sellada del catálogo;
   - baja cada archivo de Meta y verifica hash, tamaño y tipo real;
   - arma las miniaturas;
   - guarda la respuesta completa en una sola transacción, con su número `VIS-000024`.

   Si algo falla de forma definitiva, la respuesta queda rechazada con motivo, sin número, y el operador puede mandar el formulario de nuevo.
3. **En el chat** aparece una tarjeta con los valores, las miniaturas y «Guardada como VIS-000024». Sin permiso para ver respuestas, no se ven valores ni archivos.

### 6. Corren las reglas (Etapa 4)

Las reglas de «Registrar visita» se leen como frases:
- **Cuando se recibe una respuesta:** pedir aprobación a una persona con el rol Supervisor, y mandarle a quien la cargó «Recibimos VIS-000024. Te avisamos cuando la aprueben.».
- **Cuando se aprueba:** armar el PDF «Comprobante de visita» y mandarle el PDF a quien la cargó.
- **Cuando se rechaza:** mandarle el motivo.

Cada acción es una ejecución durable, con estados (Esperando, En curso, Hecho, No se pudo, Sin confirmar, No aplica). Nunca se repite lo que ya salió confirmado.

### 7. Carla decide (Etapa 4)

1. **«Por aprobar»** muestra cuántas respuestas esperan, solo a quien tiene el rol pedido y el permiso de decidir.
2. **Abre la visita:** ve lo que respondió Juan, de dónde vino y cómo va a quedar el comprobante.
3. **Decide:**
   - **Aprobar:** con confirmación.
   - **Rechazar:** con motivo obligatorio.
   - **Límites:** nadie aprueba lo que cargó. Si otra persona decidió antes, avisa con 409.

### 8. El PDF y el aviso (Etapa 4)

1. **El sistema arma el PDF** con los datos, las etiquetas, el número y el diseño fijados en ese momento, en el idioma de quien lo recibe (o en el original si falta la traducción), e incluye las fotos si el diseño lo pide.
2. **Lo guarda privado** en el almacenamiento, como una copia fija que nunca cambia.
3. **Se lo manda a Juan por WhatsApp,** dentro de las 24 horas.

### 9. La planilla (Etapa 3)

1. **Planilla:** en el panel, **«Respuestas»** tiene un enlace por formulario publicado. Cada formulario es una planilla: una fila por respuesta y una columna por pregunta de todas las versiones.
   - «—» si la pregunta no existía en esa versión.
   - «Sin respuesta» si era opcional y quedó vacía.
   - Las fotos, con miniatura y cantidad.
2. **Filtros por tipo:** con conteos calculados en la base y búsqueda privada, que viaja en el cuerpo del pedido y nunca en la URL.
3. **Detalle de una respuesta:** con las preguntas de su versión, de dónde vino, galería, historial y anterior/siguiente. El 2026-10-17 se eligió reemplazarlo por un panel al costado de la planilla (ver [07](07-pendientes.md)).
4. **Exportar a Excel:** con los filtros aplicados, hasta 10.000 respuestas y con permiso propio.

### 10. El asistente (Etapa 5)

1. **Ana configura:**
   - en cada formulario, **«Cuándo lo ofrece el asistente»**, con sus palabras;
   - qué conversaciones están en modo Asistente.
2. **Juan cuenta lo que hizo,** por escrito o en un audio, que se transcribe: «Recién salgo de Pérez Hnos., quedó todo bien. Me pagaron 125.500.».
3. **En modo Asistente,** el asistente reconoce la visita y le manda el formulario prellenado, anotando por qué y de dónde salió cada dato. En atención manual no le escribe a Juan: le sugiere al operador qué mandar.
4. **Límites:**
   - si duda, pregunta con botones;
   - una consulta o una charla no la contesta y queda para un operador;
   - si Juan quiere registrar algo que no tiene formulario (o uno que él no puede completar), le avisa y anota el pedido.
5. **Juan revisa y confirma.** El asistente nunca guarda solo.
6. **Ana ve los pedidos** en «Formularios que faltan», agrupados por tema y sin datos de más.

El lienzo tiene este recorrido completo, en ocho capítulos y paso por paso, en el tablero «Recorrido» (elegido el 2026-10-17). Cada paso indica su etapa y qué hacen la persona, el sistema y la IA.
