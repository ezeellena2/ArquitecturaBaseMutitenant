# E15 · Planilla, detalle y Excel (resumen)

**Estado:** resumen del 2026-10-17, armado desde el [traspaso](../2026-10-17-formularios-traspaso/README.md). Corresponde a la etapa 3 de ArquitecturaBase, que cerró el 2026-10-16, con el detalle D2 que se eligió allá el 2026-10-17 en lugar de la página de detalle. Se planifica con `/plan-etapa` cuando le toque, después de la E14.

**Dónde está el detalle:**
- en el traspaso: [04 §3](../2026-10-17-formularios-traspaso/04-etapas.md), [05 · D2](../2026-10-17-formularios-traspaso/05-pantallas.md), anexo C §2 (etapa 3) y anexo B (E3);
- en ArquitecturaBase: `docs/features/formularios.md` en `origin/main`, y el resumen de la etapa en el historial (`577d1c3:docs/plans/formularios/etapa-3-planilla.md`).

**Esta etapa solo lee.**

## Qué se construye

- **Back:**
  - **Navegación:** los formularios publicados que la persona puede ver, paginados y ordenados en SQL. Nada queda fijo en el front.
  - **Planilla:**
    - columnas fijas: número, recibida, persona y canal, y las empresas de quien respondió (decisión 24);
    - una columna por pregunta, unidas por `FieldId` entre versiones: primero el orden de la versión activa y después las retiradas;
    - «—» si la pregunta no existía en esa versión, y «Sin respuesta» si quedó vacía; cero y `false` se distinguen.
  - **Filtros:**
    - condiciones unidas con «y», con la gramática de la E13 por tipo: texto `contains`/`equals`; número y fecha `equals`/`gte`/`lte`; elección y sí/no `in`; y en todos, `answered`/`unanswered`/`notAsked`;
    - además, persona, versión, rango de recepción (UTC, con fin exclusivo) y empresa;
    - la búsqueda mira solo los textos largos;
    - textos, personas y condiciones viajan en el cuerpo: la planilla usa `POST …/search` con el mismo contrato `<X>Query` de [paginado-y-busqueda](../../rules/paginado-y-busqueda.md) (página, tamaño, orden, búsqueda y filtros). Esa variante para listados con filtros estructurados se suma a la ficha, con su test, al planificar la E15: vale para todo el repo, no es una excepción del módulo.
  - **Conteos por faceta:** cada uno ignora solo su propia dimensión y conserva las opciones históricas.
  - **Excel (`.xlsx`):**
    - con los filtros aplicados y `formrecords.export`;
    - hasta 10.000 respuestas: una consulta de ids ordenados e hidratación en lotes de 100, con un presupuesto de 30 s y una exportación a la vez por persona, con un candado de PostgreSQL como los `I<X>Lock` de `Application/Interfaces/Persistence` (`AdvisoryLockExtensions`), nunca uno por proceso;
    - números, fechas y sí/no van como tipos de Excel; de los archivos, la cantidad y los nombres.
  - **Detalle D2:** los datos de una respuesta con las preguntas de su versión, su origen y su historial.
- **Front:**
  - el panel «Respuestas»;
  - la planilla, con «Columnas» (que vive en la URL), «Más filtros» en un panel lateral y el desplazamiento a lo ancho;
  - **el panel D2 al costado:**
    - cabecera con el número y el estado, ↑ ↓ (también con el teclado) y ✕;
    - momentos del estado;
    - pestañas Respuesta y Fotos; las de Actividad y PDF llegan con la E16;
    - en el teléfono, a pantalla completa;
  - el diálogo de exportar.
- **Tableros:** T1, T2, T4 y T5 de ArquitecturaBase, y D2 en lugar de T3 (decisión 18), **aprobados en el lienzo v131** (2026-10-18, front `c6958ecc`):
  - **`FRM-Respuestas` (T1 y T4), la planilla:**
    - columnas fijas N°, Recibida, Cargó (con el canal) y Empresas; después, una por pregunta, con las retiradas al final;
    - «Sin respuesta» y «—» van en gris, y la planilla se desplaza a lo ancho con el N° fijo;
    - filtros: «Buscar en …» (solo en los textos largos), «Empresa ▾», «Recibida ▾», «Más filtros» y «Columnas · N oculta ▾»;
    - casos: cargando, sin respuestas, sin coincidencias, error, no se pudo actualizar, sin permiso, muchos formularios y sin permiso para exportar.
  - **`FRM-Respuestas-Filtros` (T2):** panel al costado, con conteos por opción, incluidas la opción dada de baja y «No existía en esa versión».
  - **`FRM-Respuesta` (D2), al costado de la planilla:**
    - ↑ ↓ y ✕, con los atajos en el tooltip, «Abrir conversación» y el momento Recibida;
    - pestañas Respuesta y Fotos; Actividad y PDF llegan con la E16;
    - estados: la primera de la lista, una versión anterior, cargando y «No encontramos esta respuesta».
  - **`FRM-Respuestas-Exportar` (T5):**
    - el botón va en la banda, y aparece solo con `formrecords.export`;
    - se elige entre todas las preguntas o solo las columnas visibles, y lo de los archivos va en ⓘ;
    - con más de 10.000, Exportar se apaga; también están «Ya hay una exportación en curso» y «No se pudo exportar».
  - **Teléfono:**
    - `M-FRM-Respuestas` (Más › Formularios › Respuestas): la lista de formularios, las respuestas, y los filtros y exportar en hojas;
    - `M-FRM-Respuesta`: a pantalla completa, con «5 de 24».
  - **Mandan sobre este resumen en:**
    - **el panel:** «Respuestas» es un grupo del panel, como «Gestión de usuarios», y en el teléfono «Formularios» pasa a «Diseñar formularios»;
    - **la empresa** va como columna «Empresas» y filtro «Empresa ▾» (decisión 24);
    - **«Ya hay una exportación en curso»** es el candado de PostgreSQL de una exportación por persona; **«No se pudo actualizar»** es el error al volver a leer, que conserva lo que se ve;
    - **el botón de exportar** no aparece sin `formrecords.export`, porque no es algo que la persona pueda resolver.

## Decisiones tomadas (no se vuelven a preguntar)

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
- Estado, aprobación, PDF y avisos (E16).

## Cómo se cierra

La puerta de la etapa, más un **recorrido real**:
- respuestas capturadas por Meta en las E12 a E14, con fotos, un catálogo y una opcional vacía, en la planilla, los filtros y D2;
- el `.xlsx` abierto en Excel sin reparación;
- EXPLAIN sobre unas 10.000 respuestas.

No necesita una prueba nueva con Meta: lee lo que ya llegó.

## Requisitos previos

- La E12, la E13 y la E14 cerradas.
- ClosedXML ya está en el repo desde la E11 (importar contactos), así que la exportación usa la misma biblioteca (P12).

## Ajustes ya decididos

- **Idioma:** los encabezados van en el idioma de quien mira. Si una pregunta no tiene ese idioma, va en el original de su versión.
- **Pantallas:** se copian de ArquitecturaBase, con D2 (decisión 18).

## En el multitenant

- **Datos de la organización:** todas las lecturas van dentro de RLS. No hay tablas nuevas.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** `formrecords.read` y `formrecords.export`, con la migración de roles de sistema.
- **La empresa:** un filtro «Empresa ▾» y una columna «Empresas» que se puede ocultar (decisión 24).
- **Ni WhatsApp (E11) ni el almacenamiento (E10):** solo se leen las miniaturas por las rutas de la E14.
- **«Por aprobar»:** llega con la E16.
