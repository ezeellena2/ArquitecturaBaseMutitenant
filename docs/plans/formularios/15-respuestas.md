# E15 · Respuestas: planilla, detalle, Excel y tablero (resumen)

**Estado:** resumen de la etapa 15 del bloque (decisiones 34, 57, 78 y 79). Corresponde a la etapa 3 de ArquitecturaBase, con el detalle completo (DC0 y DC1) que programó allá después. Suma a la lista «Formularios» de la E14 lo de las respuestas, y a cada formulario sus pestañas «Respuestas» y «Tablero». Se planifica con `/plan-etapa` cuando le toque, después de la E14.

**Dónde está el detalle:**
- en el traspaso del 17/10: [04 §3](../2026-10-17-formularios-traspaso/04-etapas.md), [05 · D2](../2026-10-17-formularios-traspaso/05-pantallas.md), anexo C §2 (etapa 3) y anexo B (E3);
- en el traspaso 2 de ArquitecturaBase: el anexo C §5 (G-01 a G-08), §6 (L-01 a L-04), §10 (C1-05) y §11 (C2-07), y el anexo D (MF1, DC0, DC1 ajustado, CA5, CA6, CE5 y CE6).

**Esta etapa solo lee,** salvo los ajustes del tablero de cada persona.

## Qué se construye

- **Back:**
  - **La lista «Formularios» (79, MF1):**
    - suma a cada formulario tres columnas, un dato en cada una: la cantidad de respuestas (de `LastRecordNumber`), el número de la última respuesta y cuándo llegó, con fecha y hora; sin valores. Se ordena por la última respuesta;
    - una sola consulta paginada, sin consultas por fila; la autorización se comprueba otra vez antes de devolver;
    - las columnas de respuestas se ven con `formrecords.read`.
  - **Planilla de un formulario,** en su pestaña «Respuestas»:
    - columnas fijas: número, recibida, persona y canal, y las empresas de quien respondió (decisión 24). La persona es un empleado o un cliente, con la marca «Cliente» (49); un cliente no tiene empresas;
    - una columna por pregunta, unidas por `FieldId` entre versiones: primero el orden de la versión activa y después las retiradas;
    - «—» si la pregunta no existía en esa versión, y «Sin respuesta» si quedó vacía; cero y `false` se distinguen;
    - **las subcolumnas de los campos sellados de catálogo** (E14): se ven, y «Columnas» las prende o las apaga. Son de solo lectura: no se ordenan, ni filtran, ni cuentan en las facetas;
    - **«elegir varias»** muestra las opciones en el orden en que se eligieron, separadas por coma, y su columna no se ordena.
  - **El nombre de un cliente y su conversación** salen de WhatsApp por un puerto neutral del núcleo: `IStructuredContactSource` (E12) suma leer los nombres de varios contactos por id, y un puerto nuevo, `IStructuredRecordOriginSource`, devuelve la conversación y la bandeja de cada respuesta por su `SourceKey`. Forms nunca lee tablas de WhatsApp; sin WhatsApp, la persona dice «Cliente» y no hay «Abrir conversación».
  - **Filtros:**
    - condiciones unidas con «y», con la gramática de la E12 (C5) por tipo: texto `contains`/`equals`; número y fecha `equals`/`gte`/`lte`; elegir una y sí/no `in`; **elegir varias `anyOf` («alguna de»)**; y en todos, `answered`/`unanswered`/`notAsked`;
    - `unanswered` incluye la opcional vacía y la pregunta que no existía en esa versión; `notAsked` distingue la segunda. La misma regla en memoria y en SQL;
    - además, persona, versión, rango de recepción (UTC, con fin exclusivo) y empresa;
    - la búsqueda mira solo los textos largos;
    - textos, personas y condiciones viajan en el cuerpo: la planilla usa `POST …/search` con el mismo contrato `<X>Query` de [paginado-y-busqueda](../../rules/paginado-y-busqueda.md). Esa variante para listados con filtros estructurados se suma a la ficha, con su test, al planificar la etapa: vale para todo el repo.
  - **Conteos por faceta:** cada uno ignora solo su propia dimensión y conserva las opciones históricas. En «elegir varias», una respuesta cuenta en cada opción que eligió.
  - **Excel (`.xlsx`):**
    - con los filtros aplicados y `formrecords.export`;
    - hasta 10.000 respuestas: una consulta de ids ordenados e hidratación en lotes de 100, con un presupuesto de 30 s y una exportación a la vez por persona, con la barrera que no espera de la E12 (C11), nunca una por proceso;
    - con ClosedXML detrás del puerto del módulo, como los catálogos (Excel, P12);
    - números, fechas y sí/no van como tipos de Excel;
    - **«elegir varias»** va en una celda, unido con «; ». En un catálogo tipado, se suma la columna «pregunta · valor», con los valores canónicos;
    - los campos sellados: todos, ninguno o los elegidos.
  - **El tablero por formulario** (decisión 57): la pestaña «Tablero» de cada formulario, armada sola desde los tipos de las preguntas:
    - cantidad de respuestas por día, semana o mes; totales, promedios, mínimo y máximo de los números (un monto, por moneda); y el reparto por opción y por persona;
    - se ajusta sumando o quitando gráficos y filtrando con la gramática de la planilla. Los ajustes son de cada persona, como las columnas;
    - tiene «Empresa ▾» (decisión 24), y lo ve quien tiene `formrecords.read`;
    - ningún gráfico va escrito en el código: salen de los tipos de las preguntas.
  - **El detalle:**
    - **la vista rápida (FRM-Respuesta, D2 allá),** al costado de la planilla: los datos de la respuesta con las preguntas de su versión, los campos sellados de cada opción de catálogo, su origen y su historial;
    - **la pantalla completa (DC0, con el ajuste de DC1):** en `/org/formularios/{formId}/respuestas/{recordId}/completa`, con las secciones «Respuesta» y «Actividad», a todo el ancho y plegables. Arriba, el número, ↑ ↓ en el orden de la planilla, «Abrir conversación» y «Descargar ▾»;
    - **ubicar una respuesta:** `POST …/records/{recordId}/navigation` devuelve la anterior, la siguiente, la posición y el total, con el mismo filtro y orden que la planilla, en SQL (`ROW_NUMBER`, `LAG` y `LEAD`). Lo ausente, lo excluido y lo ajeno dan el mismo 404;
    - **la dirección compartible** lleva solo el orden, la pregunta de orden y las versiones: nunca la búsqueda, las personas ni las condiciones. «Volver» regresa a la misma fila, página, columnas, filtros y orden.
- **Front:**
  - en la lista «Formularios», las columnas de respuestas;
  - las pestañas «Respuestas» y «Tablero» de cada formulario, junto a «Diseño» (79); cada persona ve las que sus permisos le dan;
  - la planilla, con «Columnas» (que vive en la URL), «Más filtros» en un panel lateral y el desplazamiento a lo ancho;
  - **la vista rápida al costado:** cabecera con el número, ↑ ↓ (también con el teclado), «Abrir en pantalla completa» y ✕, y la pestaña Respuesta. Es una pieza nueva, y su excepción a las filas que no se expanden se escribe en la ficha de tablas del front;
  - **la pantalla completa;** en el teléfono, tocar una fila la abre directo, sin vista rápida;
  - el diálogo de exportar.
- **Tableros:**
  - **Diseño dibuja nuevos** (evaluación, §5.2), con nuestras piezas y la decisión 43, mostrando solo lo que existe hasta esta etapa:
    - `MF1` · la lista «Formularios» con las columnas de respuestas, y el formulario con sus pestañas «Diseño», «Respuestas» y «Tablero»;
    - `DC0` · De la vista rápida a la pantalla completa, con el ajuste de DC1 (Respuesta y Actividad);
    - `CA5` · «Elegir varias» en la planilla, el detalle y el Excel;
    - `CE5` · Las respuestas, con las subcolumnas de los campos sellados;
    - `CA6` y `CE6` · El circuito de punta a punta de los catálogos: de pegar los proveedores a verlos en la planilla;
    - `DOC7-A` · El tablero, con tarjetas armadas solas (57).
  - **Diseño ajusta** `FRM-Respuesta`: «Abrir en pantalla completa» en la cabecera, sin fotos, estado, Actividad de aprobación ni PDF.
  - **Se quedan** los aprobados en el lienzo v131: `FRM-Respuestas` (la planilla), `FRM-Respuestas-Filtros`, `FRM-Respuestas-Exportar`, `M-FRM-Respuestas` y `M-FRM-Respuesta`, sin lo que llega con etapas posteriores.
  - **Mandan sobre este resumen en:**
    - **las fichas del front** (pedido del usuario del 2026-10-25): un dato por celda, en un renglón; las fechas completas, con fecha y hora en la zona de quien mira, nunca «hoy», «ayer» ni «hace…»; y sin rótulos que expliquen;
    - **la navegación (34 y 79):** el menú principal dice «Gestión de formularios», y su panel tiene Formularios y Catálogos, sin «Respuestas» ni «Por aprobar». Desde «Formularios» se entra a cada formulario, con sus pestañas;
    - **la empresa** va como columna «Empresas» y filtro «Empresa ▾» (decisión 24);
    - **«Ya hay una exportación en curso»** es la barrera de una exportación por persona; **«No se pudo actualizar»** es el error al volver a leer, que conserva lo que se ve;
    - **el botón de exportar** no aparece sin `formrecords.export`, porque no es algo que la persona pueda resolver.

## Decisiones tomadas (no se vuelven a preguntar)

- **La navegación de ArquitecturaBase** (34), con una sola lista «Formularios» y pestañas por formulario (79).
- **Sin migrar:** las respuestas de las etapas anteriores aparecen tal cual.
- **Exportar:** `.xlsx` con permiso propio, hasta 10.000 respuestas (con más, el diálogo pide acotar) y sin trabajos de fondo.
- **Historia:** filtrar por una opción retirada conserva la historia.
- **Permisos:** un 403 o un permiso revocado no exponen datos ni conteos.
- **«Columnas»:** vive en la URL y no se guarda en el servidor. Los ajustes del tablero (57) sí se guardan, por persona y formulario.

## Fuera de esta etapa

- Importar respuestas desde Excel.
- Exportar los archivos o exportar en segundo plano.
- Permisos por formulario.
- Lo que suman las etapas siguientes a estas pantallas: el papel «Filtro» de un campo (E16), las fotos y los documentos (E18), las líneas, los cálculos y «no se mostró» (E19), «Cargar respuesta» y el canal «web» (E20), y el estado, «Por aprobar», la aprobación, el PDF, los avisos y corregir (E21).

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más un **recorrido real**:
- respuestas capturadas por Meta en la E14, con un catálogo con campos, «elegir varias» y una opcional vacía, en la lista, la planilla, los filtros, la vista rápida, la pantalla completa y el tablero;
- un cliente con su nombre y «Abrir conversación»;
- un enlace a una respuesta abierto en otra pestaña, sin la búsqueda ni los filtros privados;
- el `.xlsx` abierto en Excel sin reparación;
- EXPLAIN sobre unas 10.000 respuestas, en la lista, la planilla, los conteos, la navegación y el Excel.

No necesita una prueba nueva con Meta: lee lo que ya llegó.

## Requisitos previos

La E14 cerrada.

## Ajustes ya decididos

- **Idioma** (35): las preguntas y las opciones se ven como se escribieron. Los encabezados fijos y los textos del sistema van en el idioma de quien mira.
- **Pantallas:** se copian de ArquitecturaBase (decisión 18), con los botones de la decisión 43.

## En el multitenant

- **Datos de la organización:** todas las lecturas van dentro de RLS. La única tabla nueva guarda los ajustes del tablero de cada persona (`FormDashboardSettings`, `ITenantOwned` con RLS, por miembro y formulario).
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Permisos de organización:** suma `formrecords.export`, con la migración de roles de sistema; `formrecords.read` es de la E14.
- **La empresa:** un filtro «Empresa ▾» y una columna «Empresas» que se puede ocultar (decisión 24).
- **WhatsApp (E11):** solo por los puertos neutrales, para el nombre del cliente y su conversación.
