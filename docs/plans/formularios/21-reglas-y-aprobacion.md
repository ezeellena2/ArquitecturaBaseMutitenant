# E21 · Reglas, aprobación, PDF y avisos (resumen)

**Estado:** pendiente · resumen de la etapa 21 del bloque (decisiones 34, 78, 80, 82 y 91 a 93). Corresponde a la etapa 4 de ArquitecturaBase, implementada y probada allá con un piloto real, y a la etapa 3 de su carga desde la app, corregir y anular, decidida allá y sin programar. Se planifica con `/plan-etapa` cuando le toque, después de la E20.

**Dónde está el detalle:**
- en el traspaso del 17/10: [04 §4](../2026-10-17-formularios-traspaso/04-etapas.md), anexo C §2 (etapa 4), anexo B (E4) y anexo D (G1–G6);
- en el traspaso 2 de ArquitecturaBase: el anexo C §5 (G-01, G-03, G-05, G-06 y G-08) y §6 (L-05 a L-08), y el capítulo 07 («E21»);
- corregir y anular: en ArquitecturaBase, `docs/plans/2026-10-29-cargar-formularios-desde-la-app.md`, decisiones 6 a 8 y «Corregir y anular», y los tableros CW10-A y CW11-A de su lienzo.

## Qué se construye

- **Back:**
  - **Reglas** «Cuando llega una respuesta»:
    - cada guardado crea una revisión inmutable y mueve el puntero activo, con su versión y 409. Guardar no crea versión ni republica, y guardar una lista vacía deshabilita las reglas;
    - **cada regla:** un evento (Recibida, Aprobada o Rechazada), hasta 10 condiciones unidas con «y» (la gramática de la E15, con «alguna de» para «elegir varias» y los cálculos de la E19) y hasta 10 acciones en orden;
    - **las acciones:** pedir aprobación a un rol, armar el PDF, mandar un mensaje a quien la cargó (con el PDF o sin él), avisar a las personas de un rol, **avisar a una persona** (62), **mandar por correo** (62) y **mandar el PDF a un cliente de la organización** (47);
    - los textos usan `{número}` y `{pregunta}`, y son un solo texto, como los escribe la persona (35);
    - una condición sobre una pregunta que no existía en esa versión, o que no se mostró (E19), cuenta como «sin respuesta»;
    - una respuesta cargada desde la web (E20) pasa por las mismas reglas que una por WhatsApp.
  - **Motor único de acciones:**
    - cada respuesta conserva la revisión de reglas con la que llegó;
    - cada acción es una ejecución con clave respuesta + evento + regla + acción, con lease y fence, y su efecto va fuera de la transacción;
    - lo confirmado nunca se repite, y lo incierto se consulta antes;
    - cada acción espera solo a la anterior de su regla; si la condición no se cumple, queda «No aplica»;
    - la captura crea sus ejecuciones en el mismo commit que la respuesta.
  - **Aprobación de un nivel:**
    - decide una cuenta activa con `formapprovals.decide` y el rol de la regla, **nunca quien cargó la respuesta**;
    - hay una sola tarea abierta por respuesta; decidir es un solo commit, y si otro decidió antes, 409;
    - rechazar exige un motivo de hasta 500 caracteres.
  - **Corregir y anular una respuesta** (91 a 93, que reemplazan la 58). Una respuesta confirmada sigue sin modificarse: puede quedar reemplazada por su corrección, o anulada. Vale para lo que llegó por WhatsApp y para lo cargado desde la web:
    - **quién:** quien la cargó, mientras no esté aprobada (siempre, si el formulario no pide aprobación). Con el permiso nuevo `formrecords.correct` («Corregir y anular respuestas»), cualquiera y en cualquier momento. Quien solo ve respuestas, no. Un cliente responde por WhatsApp y no corrige ni anula: lo hace la organización, con el permiso. El permiso va en el catálogo, en `Permissions.resx` y su `.en`, y en el seed de los roles de sistema, con su migración;
    - **corregir:** en la vista rápida, la pantalla completa y lo propio, se abre lo cargado en la versión activa, con el formulario de la E20, lo de antes a la vista y el motivo obligatorio (hasta 500 caracteres);
    - **una corrección es una carga más:** pasa por la misma captura, con sus validaciones y copias, y vuelve a hacer los cálculos y las condiciones de la E19. Los archivos que no se tocan se conservan. Tampoco la aprueba quien la corrigió;
    - **el número:** la corrección reemplaza a la anterior con el mismo número, que sigue siendo único entre las respuestas vigentes (cambia el índice de la E14). La anterior queda en el historial tal como se avisó, aprobó o imprimió, enlazada a su corrección y con su PDF fijo. La respuesta queda marcada «Corregida»;
    - **las reglas vuelven a correr,** las de «Recibida», con los valores nuevos. Si el formulario pide aprobación, vuelve a «Por aprobar», aunque ya estuviera aprobada o rechazada. Cada corrección es una respuesta nueva para el motor, así que sus ejecuciones no chocan con las de la anterior;
    - **anular:** al final del ⋮, con motivo. Queda «Anulada», con quién, cuándo y por qué, y cierra la tarea de aprobación abierta. No se borra ni se deshace;
    - **lo anulado sale de las lecturas:** la planilla, las facetas, la navegación, los conteos de MF1 y el Excel lo excluyen por defecto. La planilla lo muestra con el filtro «Anulada», y el Excel, con «Incluir las anuladas». «Respuestas» de MF1 deja de salir de `LastRecordNumber` y pasa a contar las vigentes. Una anulada sigue siendo una respuesta: el formulario no se puede borrar (E17);
    - **en el chat,** la tarjeta muestra la marca. La copia de «Mis mensajes» de un cliente no cambia: es lo que él mandó;
    - una UoW por acción, con la versión de la respuesta (`IVersioned`) y 409 si otro la corrigió, la anuló o la decidió antes.
  - **Copiar un formulario** (74, E17) suma desde esta etapa el diseño del PDF y las reglas vigentes del original, como su primera revisión.
  - **«Por aprobar» en «Formularios» (MF1),** con la misma fuente que «Para resolver» (el `IPendingItemSource` de ADR 0040):
    - MF1 suma la columna «Por aprobar» y el filtro «Con algo por aprobar», y los formularios que esperan una decisión van primero;
    - cuenta solo las tareas que esa cuenta puede decidir con su rol vigente, sin las respuestas propias;
    - **por permiso:** con solo `formrecords.read`, los conteos de aprobación son `null`; con solo `formapprovals.decide`, ve solo los formularios con tareas que puede decidir, y las respuestas y la última respuesta son `null`; con los dos, «Con algo por aprobar» incluye los dados de baja;
    - **dentro de un formulario,** «Por aprobar · N» va arriba de la planilla, de la más vieja a la más nueva. «Revisar» abre la vista rápida con la tarea, y ↑ ↓ recorren esa cola con `GET …/approvals/{taskId}/continuation`, que calcula el lote en SQL, sin valores;
    - **leer y decidir son independientes:** un 403 de decidir retira las tareas sin retirar la planilla, y un 403 de leer retira valores, archivos y PDF y deja la metadata de la tarea si decidir sigue vigente.
  - **PDF:**
    - un diseño con revisiones propias: nombre (hasta 160), preguntas, quién aprobó, pie (hasta 1000) e «Incluir las fotos». Cada texto es uno solo (35);
    - **un diseño de base bien hecho, con personalización acotada** (59): el logo, un color, un encabezado con los datos de la organización, las preguntas agrupadas en secciones con título y en el orden que se elija, mostrar u ocultar cada pregunta, y un lugar para firmas. No hay un editor libre;
    - **de dónde sale cada cosa** (82): el logo y los datos del encabezado, de la página pública de la organización; el color se elige en el diseño del PDF de cada formulario, y arranca con el del sistema de entrada. Una organización sin página pública arma el PDF sin logo y con su nombre;
    - **muestra los campos «Dato»** de las opciones de catálogo elegidas (E13 y E16), junto a su pregunta, y en un catálogo tipado, el texto y el valor, cada uno en su lugar;
    - **muestra lo de las etapas anteriores:** las fotos (E18), la tabla de líneas, los cálculos y «No se mostró» (E19);
    - al aprobar, se fija una **copia fija** A4, una por ejecución, que se escribe una sola vez. El contenido va como está escrito, y los textos del sistema («Aprobada por», «Página 1 de 1»), en el idioma de la cuenta de quien cargó la respuesta; si es un cliente sin cuenta, en el de la organización (42);
    - solo se ve por la Api, con `formrecords.read`.
  - **Avisos por WhatsApp:**
    - Forms los pide por un **puerto neutral del núcleo**, como el envío de la E14, y nunca referencia WhatsApp (lo controla `ModuleIsolationTests`). WhatsApp los encola en su misma cola de salida, marcados como «acción de formulario» y sin duplicados por ejecución. Si no se sabe si salió, la cola lo reintenta sola, como a los demás mensajes (80);
    - la ventana de 24 horas se mira al aceptar y al despachar;
    - un documento se sube a Meta antes de mandarlo;
    - **el PDF a un cliente** (47 y 55): va al chat del contacto elegido en una pregunta de un catálogo «De los contactos» (E12), que la regla nombra. El mensaje cae en el chat de ese contacto, en la bandeja Clientes. Sin contacto o con el contacto borrado, queda «No se pudo», a la vista;
    - **fuera de las 24 horas, todos los avisos de WhatsApp** (a quien cargó, a un rol, a una persona y a un cliente) van con una plantilla aprobada de la organización (E11), que elige la regla (61). Sin plantilla aprobada, o con la plantilla rechazada, queda «No se pudo», a la vista;
    - **las plantillas de la E11 suman un encabezado con documento,** para mandar el PDF, y **la regla llena sus variables:** cada variable de la plantilla se ata a `{número}`, al nombre del formulario o a una pregunta. Hoy una plantilla tiene solo cuerpo (`WhatsAppTemplateBase`), así que el alta de plantillas del módulo WhatsApp suma el encabezado y su envío a Meta;
    - **avisar a una persona** (62): la regla elige un miembro de la organización, por id. Se valida al reservar la ejecución: si dejó de ser miembro, queda «No se pudo»;
    - **mandar por correo** (62): a quien cargó, a un rol o a una persona, al correo de su cuenta; a una dirección fija escrita en la regla; o al correo de una pregunta del formulario. Lleva el texto de la regla y, si se elige, el PDF adjunto. Sale por el envío de correos del núcleo, que suma los adjuntos (hoy no los tiene), como operación durable. Un cliente sin correo no lo recibe, y queda «No se pudo». Las direcciones se validan con la ficha de [correos](../../rules/emails.md), y nunca van completas a los logs;
    - **la pregunta «Correo»:** un tipo de pregunta nuevo, validado con la misma ficha, para que el formulario pida un correo (62). Toca todo lo que ya existe: el editor y su vista previa, el Flow (un `TextInput` de correo) y el verificador (E14), la planilla, sus filtros (`contains` y `equals`), el detalle, el Excel y el tablero (E15), las condiciones (E19), la carga desde la web (E20, donde ocupa dos columnas, como un texto corto de hasta 80) y el PDF;
    - **quien cargó puede ser un cliente** (49): mandarle un mensaje o el PDF sigue la regla de la 47, con una plantilla de la organización fuera de las 24 horas;
    - **quien cargó desde la web (E20) puede no tener dónde recibir un mensaje** (89): «Mandarle un mensaje a quien la cargó» queda «No aplica», no «No se pudo». Lo decide el canal, por un puerto neutral.
  - **Lo propio de quien solo carga** (85): la planilla recortada de la E20 suma, en sus respuestas, el estado, el PDF y el motivo de rechazo. El PDF se lee con la misma regla que lo propio, filtrada por el servidor.
- **Front:**
  - la sección «Cuando llega una respuesta» en el editor, con «Guardar reglas» y su historial;
  - la página del diseño del PDF, en dos columnas, con la vista previa;
  - «Por aprobar» en MF1 y arriba de la planilla de cada formulario;
  - en la vista rápida y en la pantalla completa: Aprobar y Rechazar mientras espera, Corregir y Anular, y Actividad (con «Reintentar» para lo que no salió) y PDF;
  - en el tablero de la E15, el reparto por estado.
- **Tableros:**
  - **Diseño redibuja** (evaluación, §5.1), sin «Cambios sin guardar», sin «Revisión N» junto a Guardar, sin pares en inglés y sin textos junto a los botones (43):
    - `FRM-Editor-Reglas` y `M-FRM-Reglas`, como una pestaña más del formulario (79);
    - `FRM-Pdf`, con la vista previa en un solo idioma, los campos «Dato», las líneas y los cálculos. **Diseño muestra dos o tres estilos de PDF** para que el usuario elija uno (59), con el logo y los datos de la página pública, el color elegido, secciones y firmas (82);
    - en `FRM-Editor-Reglas`, las acciones «Avisar a una persona» y «Mandar por correo», y la plantilla para fuera de las 24 horas (61 y 62);
    - `M-FRM-Respuesta-Aprobacion`;
    - y dibuja nuevos, con sus teléfonos:
      - `FRM-Respuesta-Corregir` (91 y 93; CW10-A de ArquitecturaBase): la corrección con lo de antes a la vista y el motivo, «Corregida» en la fila y el detalle, la versión anterior, una aprobada que vuelve a «Por aprobar», y quien la cargó;
      - `FRM-Respuesta-Anular` (92; CW11-A): «Anular» al final del ⋮, el motivo, la fila que sale de la vista, el filtro «Anulada» y el Excel con «Incluir las anuladas»;
      - `FRM-Respuestas-Propias-21`: lo propio con el estado, el PDF y el motivo de rechazo (85; CW6-C).
  - **Se quedan** los aprobados en el lienzo v132: `FRM-Respuesta-Aprobacion`, `WA-Chats-Avisos`, `M-WA-Conversacion-Avisos` y «Por aprobar» en «Para resolver» de `Inicio-Org` y `M-Inicio-Org`.
  - **Pasan al Archivo** `FRM-Por-Aprobar` y `M-FRM-Por-Aprobar`: «Por aprobar» vive en MF1 (34).
  - **Mandan sobre este resumen en:**
    - **«Por aprobar» está en dos lugares:** MF1, con su columna y su filtro, y un grupo de «Para resolver» en Inicio, con la misma fuente. «Ver todos» abre MF1 con «Con algo por aprobar»;
    - **quien tiene `formapprovals.decide` y no `formrecords.read`** decide en la vista rápida sin los datos de la respuesta: el permiso de decidir lee solo la tarea, y no tiene pantalla completa;
    - **la planilla de la E15 suma la columna Estado y su filtro** (Recibida, Por aprobar, Aprobada o Rechazada), para los formularios que piden aprobación, compartido por la lista, los conteos y el Excel. «Anulada» es una opción del filtro en todos los formularios (92). La fila indica si hay una copia PDF confirmada;
    - **el diseño del PDF** guarda con «Guardar diseño», aparte de las reglas, y su vista previa no guarda nada;
    - **«Reglas de la revisión N»** se queda en Actividad, porque es un historial, y ahí van los números de revisión (43; ajuste de la Guía del 2026-10-24).

## Decisiones tomadas (no se vuelven a preguntar)

- **Orden de las acciones:**
  - «Pedir aprobación» es la última acción de su regla, y solo al recibir;
  - el PDF se arma solo al aprobar: **nunca hay PDF de algo sin aprobar**;
  - adjuntar el PDF exige que en la misma regla haya una acción anterior que lo genere.
- **Copia fija:** cambiar reglas, catálogos o el diseño no altera lo ya emitido. Los PDF duran lo que dura la respuesta (decisión 23).
- **Un solo motor,** sin una segunda cola de salida.
- **El rol** delimita quién decide, pero nunca reemplaza al permiso.
- **«Reintentar»** retoma solo lo que no salió; lo que la cola de WhatsApp no sabe si salió lo reintenta ella sola (80).
- **«Incluir las fotos»:** marcado, van chicas debajo de su pregunta; sin marcar, el PDF dice cuántas hay.

## Fuera de esta etapa

- Varios niveles o varios aprobadores.
- Acciones de otros módulos y compensaciones.
- Importar respuestas desde Excel.
- El asistente y las sugerencias (E23).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta y el almacenamiento de la E10**:
1. llega un aviso al recibir;
2. otra cuenta con el rol aprueba desde MF1 y desde «Para resolver»;
3. el PDF con el logo de la página pública, la foto, los campos «Dato», los renglones y el total se abre en el celular, y las acciones quedan en «Hecho»;
4. una cuenta en inglés recibe el aviso y el PDF con el contenido como está escrito y los textos del sistema en inglés;
5. un aviso a un empleado fuera de las 24 horas llega con la plantilla de la organización;
6. un correo con el PDF adjunto llega a una dirección fija y al correo escrito en el formulario;
7. corregir una respuesta aprobada: conserva el número, vuelve a «Por aprobar», el PDF anterior queda en el historial y al aprobar sale uno nuevo;
8. anular una respuesta con una aprobación abierta: la tarea se cierra, sale de la planilla, los conteos y el Excel, y aparece con el filtro «Anulada».

## Requisitos previos

- La E20 cerrada, y la E10 (almacenamiento).
- **Antes de planificar,** se verifica con Meta que una plantilla de la organización (E11) acepte un documento en el encabezado (47 y 61).

## Ajustes ya decididos

- **Idioma** (35): reglas, mensajes y diseño son un solo texto, como los escribe la persona. Los textos del sistema que recibe una persona van en el idioma de su cuenta (42).
- **PDF:** se reusa PDFsharp-MigraDoc, que ya está en el asistente; no se suma SkiaSharp.
- **Quién aprueba:** cualquiera de la organización con el rol de la regla y `formapprovals.decide`, salvo quien cargó la respuesta (decisión 25).
- **Avisos fuera de las 24 horas:** van con una plantilla aprobada de la organización, a empleados y a clientes (61, que reemplaza la 26). Sin plantilla, quedan «No se pudo», a la vista, y el PDF queda guardado.
- **Aprobación de un nivel:** varios niveles quedan para después del bloque (60).
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** revisiones de reglas, ejecuciones, tareas, diseños y PDF emitidos son `ITenantOwned`, con RLS.
- **Workers:** el de acciones descubre trabajo en un registro técnico global sin datos de negocio y entra a la organización como la captura de la E14 (T5): sin `ExecutionActor`, porque quien respondió puede ser un cliente sin cuenta.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:**
  - `formrules.manage` (reglas y diseño), `formapprovals.decide` y `formrecords.correct` (93), con la migración de roles de sistema;
  - los roles son los de la organización.
- **WhatsApp (E11):** los avisos salen por el número de la organización.
- **Almacenamiento (E10):** los PDF van en `IFileStore`.
- **«Por aprobar» dentro de «Para resolver»:** un `IPendingItemSource` del módulo (ADR 0040), la misma fuente que MF1.
