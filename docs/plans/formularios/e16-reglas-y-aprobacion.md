# E16 · Reglas, aprobación, PDF y avisos (resumen)

**Estado:** resumen de la octava etapa del bloque (decisión 34). Corresponde a la etapa 4 de ArquitecturaBase, implementada y probada allá con un piloto real. Se planifica con `/plan-etapa` cuando le toque, después de la E15.

**Dónde está el detalle:**
- en el traspaso del 17/10: [04 §4](../2026-10-17-formularios-traspaso/04-etapas.md), anexo C §2 (etapa 4), anexo B (E4) y anexo D (G1–G6);
- en el traspaso 2 de ArquitecturaBase: el anexo C §5 (G-01, G-03, G-05, G-06 y G-08) y §6 (L-05 a L-08), y el capítulo 07 («E16»).

## Qué se construye

- **Back:**
  - **Reglas** «Cuando llega una respuesta»:
    - cada guardado crea una revisión inmutable y mueve el puntero activo, con su versión y 409. Guardar no crea versión ni republica, y guardar una lista vacía deshabilita las reglas;
    - **cada regla:** un evento (Recibida, Aprobada o Rechazada), hasta 10 condiciones unidas con «y» (la gramática de la E15, con «alguna de» para «elegir varias») y hasta 10 acciones en orden;
    - **las acciones:** pedir aprobación a un rol, armar el PDF, mandar un mensaje a quien la cargó (con el PDF o sin él) y avisar a las personas de un rol;
    - los textos usan `{número}` y `{pregunta}`, y son un solo texto, como los escribe la persona (35);
    - una condición sobre una pregunta que no existía en esa versión cuenta como «sin respuesta».
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
  - **«Por aprobar» en «Formularios» (MF1),** con la misma fuente que «Para resolver» (el `IPendingItemSource` de ADR 0040):
    - MF1 suma la columna «Por aprobar» y el filtro «Con algo por aprobar», y los formularios que esperan una decisión van primero;
    - cuenta solo las tareas que esa cuenta puede decidir con su rol vigente, sin las respuestas propias;
    - **por permiso:** con solo `formrecords.read`, los conteos de aprobación son `null`; con solo `formapprovals.decide`, ve solo los formularios con tareas que puede decidir, y las respuestas y la última respuesta son `null`; con los dos, «Con algo por aprobar» incluye los dados de baja;
    - **dentro de un formulario,** «Por aprobar · N» va arriba de la planilla, de la más vieja a la más nueva. «Revisar» abre la vista rápida con la tarea, y ↑ ↓ recorren esa cola con `GET …/approvals/{taskId}/continuation`, que calcula el lote en SQL, sin valores;
    - **leer y decidir son independientes:** un 403 de decidir retira las tareas sin retirar la planilla, y un 403 de leer retira valores, archivos y PDF y deja la metadata de la tarea si decidir sigue vigente.
  - **PDF:**
    - un diseño con revisiones propias: nombre (hasta 160), preguntas, quién aprobó, pie (hasta 1000) e «Incluir las fotos». Cada texto es uno solo (35);
    - **muestra los campos «Dato»** de las opciones de catálogo elegidas (E13b), junto a su pregunta, y en un catálogo tipado, «texto · valor»;
    - al aprobar, se fija una **copia fija** A4, una por ejecución, que se escribe una sola vez. El contenido va como está escrito, y los textos del sistema («Aprobada por», «Página 1 de 1»), en el idioma de la cuenta de quien cargó la respuesta (42);
    - solo se ve por la Api, con `formrecords.read`.
  - **Avisos por WhatsApp:**
    - Forms los pide por un **puerto neutral del núcleo**, como el envío de la E12, y nunca referencia WhatsApp (lo controla `ModuleIsolationTests`). WhatsApp los encola en su misma cola de salida, marcados como «acción de formulario» y sin duplicados por ejecución;
    - la ventana de 24 horas se mira al aceptar y al despachar;
    - un documento se sube a Meta antes de mandarlo;
    - un aviso «Sin confirmar» lo revisa una persona: «salió» o «no salió».
- **Front:**
  - la sección «Cuando llega una respuesta» en el editor, con «Guardar reglas» y su historial;
  - la página del diseño del PDF, en dos columnas, con la vista previa;
  - «Por aprobar» en MF1 y arriba de la planilla de cada formulario;
  - en la vista rápida y en la pantalla completa: Aprobar y Rechazar mientras espera, y Actividad (con «Consultar», «Reintentar» y «Resolver el envío») y PDF.
- **Tableros:**
  - **Diseño redibuja** (evaluación, §5.1), sin «Cambios sin guardar», sin «Revisión N» junto a Guardar, sin pares en inglés y sin textos junto a los botones (43):
    - `FRM-Editor-Reglas` y `M-FRM-Reglas`;
    - `FRM-Editor-Reglas-Plantilla` y `M-FRM-Reglas-Plantilla` (las reglas que llegan de una plantilla, E17);
    - `FRM-Editor-Sugerencias` (lo usa la E18);
    - `FRM-Pdf`, con la vista previa en un solo idioma y los campos «Dato»;
    - `M-FRM-Respuesta-Aprobacion`.
  - **Se quedan** los aprobados en el lienzo v132: `FRM-Respuesta-Aprobacion`, `WA-Chats-Avisos`, `M-WA-Conversacion-Avisos` y «Por aprobar» en «Para resolver» de `Inicio-Org` y `M-Inicio-Org`.
  - **Pasan al Archivo** `FRM-Por-Aprobar` y `M-FRM-Por-Aprobar`: «Por aprobar» vive en MF1 (34).
  - **Mandan sobre este resumen en:**
    - **«Por aprobar» está en dos lugares:** MF1, con su columna y su filtro, y un grupo de «Para resolver» en Inicio, con la misma fuente. «Ver todos» abre MF1 con «Con algo por aprobar»;
    - **quien tiene `formapprovals.decide` y no `formrecords.read`** decide en la vista rápida sin los datos de la respuesta: el permiso de decidir lee solo la tarea, y no tiene pantalla completa;
    - **«Resolver el envío»** es la revisión humana de un aviso «Sin confirmar» («salió» o «no salió»), con `formrules.manage`;
    - **la planilla de la E15 suma la columna Estado y su filtro** (Recibida, Por aprobar, Aprobada o Rechazada), para los formularios que piden aprobación, compartido por la lista, los conteos y el Excel. La fila indica si hay una copia PDF confirmada;
    - **el diseño del PDF** guarda con «Guardar diseño», aparte de las reglas, y su vista previa no guarda nada;
    - **«Reglas de la revisión N»** se puede ver en Actividad, porque es un historial (43).

## Decisiones tomadas (no se vuelven a preguntar)

- **Orden de las acciones:**
  - «Pedir aprobación» es la última acción de su regla, y solo al recibir;
  - el PDF se arma solo al aprobar: **nunca hay PDF de algo sin aprobar**;
  - adjuntar el PDF exige que en la misma regla haya una acción anterior que lo genere.
- **Copia fija:** cambiar reglas, catálogos o el diseño no altera lo ya emitido. Los PDF duran lo que dura la respuesta (decisión 23).
- **Un solo motor,** sin una segunda cola de salida.
- **El rol** delimita quién decide, pero nunca reemplaza al permiso.
- **«Reintentar»** retoma solo lo que no salió; lo «Sin confirmar» se consulta primero.
- **«Incluir las fotos»:** marcado, van chicas debajo de su pregunta; sin marcar, el PDF dice cuántas hay.

## Fuera de esta etapa

- Varios niveles o varios aprobadores.
- Acciones de otros módulos y compensaciones.
- Cargar desde la web, importar y corregir respuestas.
- El asistente y las sugerencias (E18).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta y el almacenamiento de la E10**:
1. llega un aviso al recibir;
2. otra cuenta con el rol aprueba desde MF1 y desde «Para resolver»;
3. el PDF con la foto y los campos «Dato» se abre en el celular, y las acciones quedan en «Hecho»;
4. una cuenta en inglés recibe el aviso y el PDF con el contenido como está escrito y los textos del sistema en inglés.

## Requisitos previos

- La E15 cerrada, y la E10 (almacenamiento).
- **Sin plantillas de Meta nuevas:** fuera de las 24 horas, el aviso queda «No se pudo» (decisión 26).

## Ajustes ya decididos

- **Idioma** (35): reglas, mensajes y diseño son un solo texto, como los escribe la persona. Los textos del sistema que recibe una persona van en el idioma de su cuenta (42).
- **PDF:** se reusa PDFsharp-MigraDoc, que ya está en el asistente; no se suma SkiaSharp.
- **Quién aprueba:** cualquiera de la organización con el rol de la regla y `formapprovals.decide`, salvo quien cargó la respuesta (decisión 25).
- **Avisos fuera de las 24 horas:** quedan «No se pudo», a la vista, y el PDF queda guardado. El operador puede mandar una plantilla desde el chat con lo que ya trae la E11. El envío automático con plantilla queda para después (decisión 26).
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** revisiones de reglas, ejecuciones, tareas, diseños y PDF emitidos son `ITenantOwned`, con RLS.
- **Workers:** el de acciones descubre trabajo en un registro técnico global sin datos de negocio y entra con `ExecutionActor`, que usa `ITenantScope` por dentro (T4 y T5 de la E12).
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:**
  - `formrules.manage` (reglas, diseño y revisión de avisos) y `formapprovals.decide`, con la migración de roles de sistema;
  - los roles son los de la organización.
- **WhatsApp (E11):** los avisos salen por el número de la organización.
- **Almacenamiento (E10):** los PDF van en `IFileStore`.
- **«Por aprobar» dentro de «Para resolver»:** un `IPendingItemSource` del módulo (ADR 0040), la misma fuente que MF1.
