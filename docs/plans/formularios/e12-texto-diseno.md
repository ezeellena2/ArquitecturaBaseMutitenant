# E12 · Texto para el chat de Diseño

Se pega tal cual en el chat de Diseño, que es el único que toca el lienzo.

---

Te paso los tableros de una etapa nueva para el lienzo del multitenant. No hay que programar nada: solo tableros, como siempre.

**Contexto.** Sumamos «Formularios por WhatsApp» como etapas 12 a 17, después de la E10. La E12 es la primera:
- una organización arma formularios con preguntas tipadas, en español e inglés;
- los publica en WhatsApp como Flows de Meta;
- un operador los manda desde un chat de la bandeja Internos;
- el empleado los completa dentro de WhatsApp;
- la respuesta queda numerada (`VIS-000123`) y aparece como una tarjeta en el chat.

**Leé primero,** en el repo del back `ArquitecturaBaseMutitenant`:
1. **La spec:** `docs/plans/formularios/e12-formularios.md`. Sobre todo:
   - la §9, con los tableros y sus estados;
   - la §3, con los datos que existen;
   - la §4, con las reglas y los errores;
   - la §5, con lo que devuelve cada ruta;
   - la §13, con las decisiones ya tomadas.
2. **La hoja de ruta:** `docs/plans/2026-10-12-formularios-dinamicos-hoja-de-ruta.md`, §1 y §5.
3. **ArquitecturaBase, solo como referencia de contenido, no de estilo.** Ya diseñó esto para su modelo de negocio:
   - en `ArquitecturaBaseFront`, el documento `docs/design/formularios-whatsapp.md`. La copia local está atrasada: hacé `git fetch` y leelo con `git show origin/main:docs/design/formularios-whatsapp.md`;
   - la página «Formularios por WhatsApp» de su lienzo: https://claude.ai/artifact/HPbmDPLnr8JZ9TxevtTqJJ.

   De ahí tomá qué estados y casos existen. El dibujo sale de nuestro lienzo y nuestros criterios.

**Cómo trabajamos** (lo de siempre):
- **Una pantalla por vez:** explicás qué es y qué hace, mostrás 2 o 3 opciones con una recomendada y esperás que el usuario elija.
- **Primero la compu y después el teléfono.**
- **Las piezas y los criterios de nuestro lienzo:**
  - listados como «Usuarios», fichas con pestañas y lo corto en diálogo;
  - estados con color y palabra corta, el detalle en ⓘ, sin texto explicativo y ⋮ en las filas;
  - el teléfono como app: barra de abajo, título en la barra, hojas desde abajo y «+» flotante.
- **Textos:** «Organización», nunca «tenant», y con voseo. Los textos que dibujes se vuelven los de la app.
- **Al decidir y al cerrar:** cada decisión suma su fila en «Criterio · Decisiones por pantalla». Al terminar, publicás la versión nueva del lienzo y regenerás la guía.

**Dónde van:**
- **Página nueva:** «Organización · Formularios».
- **Prefijo de los tableros:** `FRM-`, como `WA-` en WhatsApp, para no confundirlos con `Criterio-Formularios`.
- **Lo del chat:** son variantes de los `WA-*` que ya existen.

**Datos de ejemplo** (los de siempre del lienzo: Grupo Delta, Lucía Fernández y un empleado del tablero «Usuarios»):
- **Formularios:**
  - «Visita a cliente» (VIS), publicado, versión 3;
  - «Entrega de pedido» (ENT), publicado, versión 1;
  - «Reclamo» (REC), borrador;
  - «Control de vehículo» (VEH), no se pudo publicar.
- **Preguntas de «Visita a cliente»:**
  - Cliente: texto corto, obligatoria;
  - Fecha de la visita: fecha;
  - ¿Se concretó la venta?: sí/no;
  - Monto estimado: número con 2 decimales, con la ayuda «Usá punto para los decimales»;
  - Motivo: lista con Seguimiento, Venta nueva y Reclamo;
  - Observaciones: texto largo, opcional.
- **Una respuesta:** VIS-000123, con esos seis valores. Que se vea la diferencia entre «No», «0» y «Sin respuesta».

**Tableros, en este orden** (el detalle y los estados están en la §9 de la spec):
1. **`FRM-Listado`:**
   - nombre, prefijo, estado y última publicación, con ⋮ y «Nuevo formulario»;
   - filtro Todos, Publicados y Borradores;
   - vacío, sin resultados y error.
2. **`FRM-Nuevo`:** un diálogo con el nombre. Al crear, se propone el prefijo.
3. **`FRM-Ficha` › Preguntas:** la lista de preguntas, «Agregar pregunta», subir y bajar, y la vista previa del teléfono al costado.
4. **`FRM-Pregunta`:** un diálogo con el tipo, el texto y la ayuda, si es obligatoria, y según el tipo, el largo máximo, los decimales (de 0 a 6) o las opciones. Los seis tipos.
5. **`FRM-Ficha` › Inglés:** los textos en inglés al lado del original, con lo que falta marcado. Se puede guardar incompleto; para publicar, tiene que estar todo.
6. **`FRM-Prefijo`:** propuesto, editado, tomado y bloqueado (después de publicar no cambia).
7. **`FRM-Publicar`:** el diálogo de comprobación:
   - sin problemas;
   - con problemas por pregunta (por ejemplo, un texto que no entra en WhatsApp);
   - borrador sin guardar;
   - otra publicación en curso;
   - WhatsApp no disponible.
8. **`FRM-Ficha` › Publicación (`FRM-Publicacion`):**
   - la versión activa y la vista previa oficial de Meta;
   - estados: Publicando, Sin confirmar, Publicado · v3 y No se pudo publicar (con el motivo por pregunta).
9. **`WA-Chats-Formulario`:** en un chat de Internos, el botón «Formulario» al lado de donde se escribe abre el diálogo para elegir el formulario, el texto del mensaje (hasta 1024 caracteres) y cómo le va a llegar. El botón aparece apagado, con su motivo en ⓘ, cuando:
   - la ventana de 24 horas está cerrada;
   - la persona no es empleada;
   - no hay formularios para esa persona;
   - hay un envío pendiente.
10. **`WA-Chats-Respuesta`:** la tarjeta en el chat. Primero el mensaje enviado con su botón «Abrir formulario», y después la respuesta. Estados de la respuesta:
    - esperando;
    - recibida con número y valores;
    - recibida solo con número, para quien no puede ver respuestas;
    - rechazada;
    - vencida.

    En la lista de chats, la vista previa dice «Respuesta del formulario».
11. **Ajustes:**
    - «Rol» suma el área Formularios con cinco permisos: Ver formularios, Diseñar formularios, Publicar formularios, Responder formularios y Ver respuestas;
    - la pestaña «Módulos» de la ficha de una organización, en Plataforma, suma «Formularios»;
    - el menú de la organización suma «Formularios».

Después vienen los teléfonos: `M-FRM-Listado`, `M-FRM-Ficha`, `M-FRM-Pregunta`, `M-WA-Chats-Formulario` y `M-WA-Conversacion-Respuesta`.

**Elecciones que hay que mostrar con opciones:**
- **Dónde va «Formularios» en el menú de la organización.** Recomiendo el menú principal, porque en la E15 suma «Respuestas», que es de uso diario. La alternativa es Administración.
- **Qué se hace en el teléfono:** editar un formulario completo como app, o solo verlo, publicarlo y abrir la vista previa. Mostrá las dos.
- **Cómo se muestra el estado de la publicación:** una palabra corta con ⓘ, o los pasos (Aceptada → Enviada a WhatsApp → WhatsApp la revisa → Lista para mandar).

**Textos que ya fija la spec:**
- «Abrir formulario» (el botón del mensaje en WhatsApp, hasta 20 caracteres);
- «Respuesta del formulario»;
- «Usá punto para los decimales»;
- los estados de un formulario: Borrador, Publicando, Publicado · vN, Sin confirmar y No se pudo publicar.

**No se dibuja ahora:** catálogos, fotos y documentos, la planilla de respuestas, las reglas y la biblioteca de plantillas. Son las etapas 13 a 17.

**Al terminar,** avisá la versión del lienzo y la lista de tableros aprobados, para anotarlos en la spec.
