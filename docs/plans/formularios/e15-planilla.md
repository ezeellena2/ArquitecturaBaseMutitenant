# E15 · «Formularios», planilla, detalle y Excel (resumen)

**Estado:** resumen de la séptima etapa del bloque (decisión 34). Corresponde a la etapa 3 de ArquitecturaBase, con la pantalla «Formularios» (MF1) y el detalle completo (DC0 y DC1) que programó allá después. Se planifica con `/plan-etapa` cuando le toque, después de la E14.

**Dónde está el detalle:**
- en el traspaso del 17/10: [04 §3](../2026-10-17-formularios-traspaso/04-etapas.md), [05 · D2](../2026-10-17-formularios-traspaso/05-pantallas.md), anexo C §2 (etapa 3) y anexo B (E3);
- en el traspaso 2 de ArquitecturaBase: el anexo C §5 (G-01 a G-08), §6 (L-01 a L-04), §10 (C1-05) y §11 (C2-07), y el anexo D (MF1, DC0, DC1 ajustado, CA5, CE5 y CE6).

**Esta etapa solo lee.**

## Qué se construye

- **Back:**
  - **«Formularios» (MF1):**
    - una tabla con los formularios publicados no eliminados: el nombre de su versión activa, la cantidad de respuestas (de `LastRecordNumber`) y la última respuesta (número, cuándo y estado, sin valores);
    - filtros «Activos · Dados de baja», con sus cantidades calculadas antes de paginar;
    - orden: por la última respuesta. La E16 suma «Por aprobar», que pasa primero;
    - una sola consulta paginada, sin consultas por fila; la autorización se comprueba otra vez antes de devolver;
    - pide `formrecords.read`. La E16 suma quien solo aprueba.
  - **Planilla de un formulario:**
    - columnas fijas: número, recibida, persona y canal, y las empresas de quien respondió (decisión 24);
    - una columna por pregunta, unidas por `FieldId` entre versiones: primero el orden de la versión activa y después las retiradas;
    - «—» si la pregunta no existía en esa versión, y «Sin respuesta» si quedó vacía; cero y `false` se distinguen;
    - **las subcolumnas de los campos sellados de catálogo** (E13b): las de papel «Dato» se ven, y las de «Filtro» se prenden en «Columnas». Son de solo lectura: no se ordenan, ni filtran, ni cuentan en las facetas;
    - **«elegir varias»** muestra las opciones en el orden en que se eligieron, separadas por coma, y su columna no se ordena.
  - **Filtros:**
    - condiciones unidas con «y», con la gramática de la E13 por tipo: texto `contains`/`equals`; número y fecha `equals`/`gte`/`lte`; elegir una y sí/no `in`; **elegir varias `anyOf` («alguna de»)**; y en todos, `answered`/`unanswered`/`notAsked`;
    - `unanswered` incluye la opcional vacía y la pregunta que no existía en esa versión; `notAsked` distingue la segunda. La misma regla en memoria y en SQL;
    - además, persona, versión, rango de recepción (UTC, con fin exclusivo) y empresa;
    - la búsqueda mira solo los textos largos;
    - textos, personas y condiciones viajan en el cuerpo: la planilla usa `POST …/search` con el mismo contrato `<X>Query` de [paginado-y-busqueda](../../rules/paginado-y-busqueda.md). Esa variante para listados con filtros estructurados se suma a la ficha, con su test, al planificar la etapa: vale para todo el repo.
  - **Conteos por faceta:** cada uno ignora solo su propia dimensión y conserva las opciones históricas. En «elegir varias», una respuesta cuenta en cada opción que eligió.
  - **Excel (`.xlsx`):**
    - con los filtros aplicados y `formrecords.export`;
    - hasta 10.000 respuestas: una consulta de ids ordenados e hidratación en lotes de 100, con un presupuesto de 30 s y una exportación a la vez por persona, con un candado de PostgreSQL como los `I<X>Lock` (`AdvisoryLockExtensions`), nunca uno por proceso;
    - números, fechas y sí/no van como tipos de Excel; de los archivos, la cantidad y los nombres;
    - **«elegir varias»** va en una celda, unido con «; ». En un catálogo tipado, se suma la columna «pregunta · valor», con los valores canónicos;
    - los campos sellados: todos, ninguno o los elegidos.
  - **El detalle:**
    - **la vista rápida (FRM-Respuesta, D2 allá),** al costado de la planilla: los datos de la respuesta con las preguntas de su versión, su origen y su historial;
    - **la pantalla completa (DC0, con el ajuste de DC1):** en `/org/formularios/{formId}/respuestas/{recordId}/completa`, con las secciones «Respuesta» y «Actividad», a todo el ancho y plegables. Arriba, el número, el estado, ↑ ↓ en el orden de la planilla, «Abrir conversación» y «Descargar ▾». La E16 suma aprobar, rechazar y el PDF;
    - **ubicar una respuesta:** `POST …/records/{recordId}/navigation` devuelve la anterior, la siguiente, la posición y el total, con el mismo filtro y orden que la planilla, en SQL (`ROW_NUMBER`, `LAG` y `LEAD`). Lo ausente, lo excluido y lo ajeno dan el mismo 404;
    - **la dirección compartible** lleva solo el orden, la pregunta de orden y las versiones: nunca la búsqueda, las personas ni las condiciones. «Volver» regresa a la misma fila, página, columnas, filtros y orden.
- **Front:**
  - «Formularios» (MF1) en el panel de «Gestión de formularios» (34);
  - la planilla de cada formulario, con «Columnas» (que vive en la URL), «Más filtros» en un panel lateral y el desplazamiento a lo ancho;
  - **la vista rápida al costado:** cabecera con el número y el estado, ↑ ↓ (también con el teclado), «Abrir en pantalla completa» y ✕; pestañas Respuesta y Fotos (Actividad y PDF llegan con la E16);
  - **la pantalla completa;** en el teléfono, tocar una fila la abre directo, sin vista rápida;
  - el diálogo de exportar.
- **Tableros:**
  - **Diseño dibuja nuevos** (evaluación, §5.2), con nuestras piezas y la decisión 43:
    - `MF1` · Una tabla de formularios, con el panel de «Gestión de formularios»;
    - `DC0` · De la vista rápida a la pantalla completa, con el ajuste de DC1 (Respuesta y Actividad);
    - `CE5` · Las respuestas, con las subcolumnas de los campos sellados;
    - `CE6` · El circuito de punta a punta de los catálogos.
  - **Diseño ajusta** `FRM-Respuesta`: «Abrir en pantalla completa» en la cabecera. El panel de todos los FRM de la compu cambia con MF1.
  - **Se quedan** los aprobados en el lienzo v131: `FRM-Respuestas` (la planilla), `FRM-Respuestas-Filtros`, `FRM-Respuestas-Exportar`, `M-FRM-Respuestas` y `M-FRM-Respuesta`. `CA5` (de la E13) muestra «elegir varias» en la planilla, el detalle y el Excel.
  - **Mandan sobre este resumen en:**
    - **la navegación (34):** el menú principal dice «Gestión de formularios», y su panel tiene Formularios, Diseñar formularios, Catálogos y Formularios que faltan, sin «Respuestas» ni «Por aprobar». Desde «Formularios» se entra a cada formulario, con sus respuestas y sus acciones;
    - **la empresa** va como columna «Empresas» y filtro «Empresa ▾» (decisión 24);
    - **«Ya hay una exportación en curso»** es el candado de PostgreSQL de una exportación por persona; **«No se pudo actualizar»** es el error al volver a leer, que conserva lo que se ve;
    - **el botón de exportar** no aparece sin `formrecords.export`, porque no es algo que la persona pueda resolver.

## Decisiones tomadas (no se vuelven a preguntar)

- **La navegación de ArquitecturaBase** (34): MF1 con su detalle rápido y completo.
- **Sin migrar:** las respuestas de las etapas anteriores aparecen tal cual.
- **Exportar:** `.xlsx` con permiso propio, hasta 10.000 respuestas (con más, el diálogo pide acotar) y sin trabajos de fondo.
- **Historia:** filtrar por una opción retirada conserva la historia.
- **Permisos:** un 403 o un permiso revocado no exponen datos ni conteos.
- **«Columnas»:** vive en la URL y no se guarda en el servidor.
- **«Estado»:** llega con la aprobación (E16), solo en formularios que la piden.

## Fuera de esta etapa

- Cargar respuestas desde la web, importarlas desde Excel y corregirlas.
- Exportar los archivos o exportar en segundo plano.
- Permisos por formulario.
- «Por aprobar», el estado, la aprobación, el PDF y los avisos (E16).
- El tablero por formulario (E19).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más un **recorrido real**:
- respuestas capturadas por Meta en las etapas anteriores, con fotos, un catálogo con campos, «elegir varias» y una opcional vacía, en MF1, la planilla, los filtros, la vista rápida y la pantalla completa;
- un enlace a una respuesta abierto en otra pestaña, sin la búsqueda ni los filtros privados;
- el `.xlsx` abierto en Excel sin reparación;
- EXPLAIN sobre unas 10.000 respuestas, en MF1, la planilla, los conteos, la navegación y el Excel.

No necesita una prueba nueva con Meta: lee lo que ya llegó.

## Requisitos previos

- La E13c y la E14 cerradas.
- ClosedXML ya está en el repo desde la E11, así que la exportación usa la misma biblioteca.

## Ajustes ya decididos

- **Idioma** (35): las preguntas y las opciones se ven como se escribieron. Los encabezados fijos y los textos del sistema van en el idioma de quien mira.
- **Pantallas:** se copian de ArquitecturaBase (decisión 18), con los botones de la decisión 43.

## En el multitenant

- **Datos de la organización:** todas las lecturas van dentro de RLS. No hay tablas nuevas.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** `formrecords.read` y `formrecords.export`, con la migración de roles de sistema.
- **La empresa:** un filtro «Empresa ▾» y una columna «Empresas» que se puede ocultar (decisión 24).
- **Ni WhatsApp (E11) ni el almacenamiento (E10):** solo se leen las miniaturas por las rutas de la E14.
- **«Por aprobar»:** llega con la E16, en MF1 y en «Para resolver».
