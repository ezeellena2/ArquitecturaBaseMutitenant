# E16 · Reglas, aprobación, PDF y avisos (resumen)

**Estado:** resumen del 2026-10-17, armado desde el [traspaso](../2026-10-17-formularios-traspaso/README.md). Corresponde a la etapa 4 de ArquitecturaBase:
- está implementada y verificada localmente;
- el piloto real en español del 2026-10-17 anduvo: aviso al recibir, aprobación por otra cuenta y PDF con foto en el celular;
- quedaron pendientes el inglés y la caída al original.

Se planifica con `/plan-etapa` cuando le toque, después de la E15.

**Dónde está el detalle:**
- en el traspaso: [04 §4](../2026-10-17-formularios-traspaso/04-etapas.md), anexo C §2 (etapa 4), anexo B (E4) y anexo D (G1–G6);
- en ArquitecturaBase: `docs/features/formularios.md` y `docs/features/whatsapp.md` en `origin/main`, y el resumen de la etapa en el historial (`98fa775c:docs/plans/formularios/etapa-4-reglas-aprobacion-pdf.md`).

## Qué se construye

- **Back:**
  - **Reglas** «Cuando llega una respuesta»:
    - cada guardado crea una revisión inmutable y mueve el puntero activo, con su versión y 409. Guardar no crea versión ni republica, y guardar una lista vacía deshabilita las reglas;
    - **cada regla:** un evento (Recibida, Aprobada o Rechazada), hasta 10 condiciones unidas con «y» (la gramática de la E15) y hasta 10 acciones en orden;
    - **las acciones:** pedir aprobación a un rol, armar el PDF, mandar un mensaje a quien la cargó (con el PDF o sin él) y avisar a las personas de un rol;
    - los textos usan `{número}` y `{pregunta}`;
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
  - **PDF:**
    - un diseño con revisiones propias: nombre (hasta 160), preguntas, quién aprobó, pie (hasta 1000) e «Incluir las fotos»;
    - al aprobar, se fija una **copia fija** A4, una por ejecución y por idioma, que se escribe una sola vez;
    - solo se ve por la Api, con `formrecords.read`.
  - **Avisos por WhatsApp:**
    - Forms los pide por un **puerto neutral del núcleo**, como el envío de la E12, y nunca referencia WhatsApp (lo controla `ModuleIsolationTests`). WhatsApp los encola en su misma cola de salida, marcados como «acción de formulario» y sin duplicados por ejecución;
    - la ventana de 24 horas se mira al aceptar y al despachar;
    - un documento se sube a Meta antes de mandarlo;
    - un aviso «Sin confirmar» lo revisa una persona: «salió» o «no salió».
- **Front:**
  - la sección «Cuando llega una respuesta» en el editor, con «Guardar reglas» y su historial;
  - la página del diseño del PDF, en dos columnas, con la vista previa y su selector de idioma;
  - «Por aprobar», con «Para mí» y «Todas»;
  - en D2: Aprobar y Rechazar en el momento «Por aprobar», y las pestañas Actividad (con «Consultar» y «Reintentar») y PDF.
- **Tablero:** G1 a G6 de ArquitecturaBase (decisión 18).

## Decisiones tomadas (no se vuelven a preguntar)

- **Orden de las acciones:**
  - «Pedir aprobación» es la última acción de su regla, y solo al recibir;
  - el PDF se arma solo al aprobar: **nunca hay PDF de algo sin aprobar**;
  - adjuntar el PDF exige que en la misma regla haya una acción anterior que lo genere.
- **Copia fija:** cambiar reglas, catálogos o el diseño no altera lo ya emitido. Los PDF duran lo que dura la respuesta.
- **Un solo motor,** sin una segunda cola de salida.
- **El rol** delimita quién decide, pero nunca reemplaza al permiso.
- **«Reintentar»** retoma solo lo que no salió; lo «Sin confirmar» se consulta primero.
- **«Incluir las fotos»:** marcado, van chicas debajo de su pregunta; sin marcar, el PDF dice cuántas hay.

## Fuera de esta etapa

- Varios niveles o varios aprobadores.
- Acciones de otros módulos y compensaciones.
- Cargar desde la web, importar y corregir respuestas.
- El asistente y las sugerencias.

## Cómo se cierra

La puerta de la etapa, más una **prueba real con Meta y el almacenamiento de la E10**:
1. llega un aviso al recibir;
2. otra cuenta con el rol aprueba desde «Para resolver»;
3. el PDF con la foto se abre en el celular, y las acciones quedan en «Hecho»;
4. **también lo que ArquitecturaBase no cerró:** el recorrido completo en inglés, y con el inglés incompleto, todo en el original.

## Requisitos previos

- La E12 a la E15 cerradas, y la E10 (almacenamiento).
- **Sin plantillas de Meta nuevas:** fuera de las 24 horas, el aviso queda «No se pudo» (decisión 26).

## Ajustes ya decididos

- **Idioma:** reglas, mensajes y diseño exigen solo el original. Para cada evento, si el idioma de quien recibe no está completo en todos los avisos de esa revisión y en el diseño del PDF, todo sale en el original, y esa elección queda fijada en la ejecución.
- **PDF:** se reusa PDFsharp-MigraDoc, que ya está en el asistente; no se suma SkiaSharp.
- **Quién aprueba:** cualquiera de la organización con el rol de la regla y `formapprovals.decide`, salvo quien cargó la respuesta (decisión 25).
- **Avisos fuera de las 24 horas:** quedan «No se pudo», a la vista, y el PDF queda guardado. El operador puede mandar una plantilla desde el chat con lo que ya trae la E11. El envío automático con plantilla queda para después (decisión 26).

## En el multitenant

- **Datos de la organización:** revisiones de reglas, ejecuciones, tareas, diseños y PDF emitidos son `ITenantOwned`, con RLS.
- **Workers:** el de acciones descubre trabajo en un registro técnico global sin datos de negocio y entra con `ExecutionActor`, que usa `ITenantScope` por dentro (T4 y T5 de la E12).
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:**
  - `formrules.manage` (reglas, diseño y revisión de avisos) y `formapprovals.decide`, con la migración de roles de sistema;
  - los roles son los de la organización.
- **WhatsApp (E11):** los avisos salen por el número de la organización.
- **Almacenamiento (E10):** los PDF van en `IFileStore`.
- **«Por aprobar» dentro de «Para resolver»:** un `IPendingItemSource` del módulo (ADR 0040). «Ver todos» abre el listado filtrado, con «Para mí» y «Todas».
