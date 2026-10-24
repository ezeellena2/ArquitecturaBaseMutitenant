# E19 · Documentos por WhatsApp (resumen)

**Estado:** resumen de la última etapa del bloque (decisiones 34 y 41). En ArquitecturaBase está decidido, con su ADR 0022 y sus tableros, y sin programar. **Antes de planificar** se prueba con Meta si un Flow acepta un archivo precargado en una pregunta de documento: es el mayor riesgo técnico, y el plan de la etapa parte de ese resultado.

**Dónde está el detalle:** en el traspaso 2 de ArquitecturaBase, el anexo F (F3), el anexo D (DOC1 a DOC7-A) y el capítulo 07 («E14» y «E17»); allá, la ADR 0022 (lectura de documentos con IA).

## Qué se construye

- **Back:**
  - **Qué se lee:** lo que manda un empleado: un miembro activo, con teléfono verificado y `formrecords.create`, en la bandeja Internos. Lo que manda un cliente o un número desconocido queda en el chat para un operador, sin leerse. Con las decisiones 49 y 51, leer documentos de clientes se revalida con el usuario en el paso 8.

    | Lo que llega | Cómo se lee |
    | --- | --- |
    | Foto o captura (JPEG, PNG) | la lee la IA |
    | PDF | la IA lee todas sus páginas, con un tope |
    | Word (`.docx`) | el servidor saca el texto y las tablas, sin ejecutar nada, y la IA los lee |
    | Excel (`.xlsx`) | el servidor lee las hojas con los topes de la importación de catálogos (E13). Varias filas del mismo tipo proponen hasta 3 cargas por mensaje (el tope de acciones de la E18), y las demás quedan propuestas en la bandeja |
    | Ubicación y contacto | sin IA: completan las preguntas elegidas en el editor |

    No se leen los formatos viejos (`.doc`, `.xls`), los archivos con macros ni los que tienen contraseña: se pide un PDF o una versión sin macros ni contraseña. El tipo se verifica por el contenido, nunca por la extensión, como los archivos de la E14.
  - **El circuito:**
    1. **recibir:** el archivo se baja con el token de la organización y se guarda como copia privada en `IFileStore`, como los audios de la E18;
    2. **clasificar** entre los formularios con «Lectura de documentos» que esa persona puede completar:
       - seguro: propone ese formulario;
       - con duda: pregunta «¿Qué es este documento?» con botones para 2 o 3 formularios y «Otro»;
       - sin reconocerlo: ofrece la lista que esa persona puede completar, hasta 10 filas con «Otro». «Otro» lo deja «Sin clasificar» en la bandeja y lo anota en «Formularios que faltan» (E18);
    3. **extraer** los datos según las preguntas del formulario elegido, con su seguridad por dato;
    4. **validar sin IA:** las validaciones fijas, el cruce con los catálogos (por ejemplo, el CUIT como campo de un catálogo de la E13b) y los duplicados (el mismo archivo, o la misma operación, monto y fecha);
    5. **proponer** el formulario prellenado por WhatsApp, con el documento adjunto en su pregunta de archivo. Lo que no se pudo leer queda vacío, y lo dudoso, marcado;
    6. **confirmar o corregir** lo hace la persona. Al confirmar se vuelven a revisar las validaciones fijas y los duplicados con lo corregido. **La IA nunca carga sola;**
    7. **organizar:** queda como una respuesta más, con sus reglas, su aprobación y su PDF.
  - **La «Lectura de documentos» de un formulario,** en su borrador, que se sella al publicar:
    - «Cómo reconocerlo», qué pregunta recibe cada dato estándar, las validaciones fijas, el cruce con catálogos y la regla de duplicados;
    - un formulario que salió de una plantilla con lectura (E17) trae lo suyo, y las validaciones del estándar van con candado;
    - cambiarle el nombre a una pregunta no corta la lectura, y un dato sin pregunta no se lee.
  - **Las validaciones fijas** son un conjunto cerrado en el código, y cada formulario elige cuáles usa:
    - CUIT y CUIL reusan el validador de `platform.TaxIdTypes` (`ar-cuit-mod11`);
    - CBU y CVU suman sus validadores de dígitos verificadores;
    - la tabla de entidades del BCRA (prefijo → banco o billetera) es un dato de referencia global, según [datos de referencia](../../rules/datos-de-referencia.md). Nunca una lista en el código.
  - **La bandeja «Documentos recibidos»:**
    - una planilla como la de respuestas, con el documento en un panel al costado, junto a sus datos;
    - filtros: formulario, quién lo mandó, cuándo llegó, estado (propuesto, sin clasificar, confirmado o descartado) y «Empresa ▾» (decisión 24). Todo vive en la dirección, salvo la búsqueda;
    - búsqueda por los datos (monto con o sin puntos, CUIT u operación), también en lo que no se cargó, sobre jsonb con índice GIN;
    - revisar: corregir, cambiar el formulario reconocido (vuelve a leer), pasar al formulario y descartar con confirmación. La respuesta dice de qué documento salió y quién la revisó.
  - **«Armar formulario con la IA»:** sobre un documento «Sin clasificar», o desde el aviso «Llegaron N documentos parecidos» (con tres o más). La IA propone nombre, prefijo, texto del botón, «Cómo reconocerlo» y una pregunta por dato, con su tipo; si se parece a un estándar de la biblioteca, lo sugiere antes. Se crea un borrador con la lectura prendida; una persona lo revisa y lo publica. Si falla, no queda nada a medias.
  - **El tablero por formulario:** una pestaña «Tablero», junto a las respuestas del formulario en «Formularios» (MF1), armada sola desde los tipos de las preguntas: cantidad por período, totales y promedios de los números (un monto, por moneda), el reparto por opción y por persona, y el estado. Se ajusta con la gramática de la planilla, y los ajustes son de cada persona. Tiene «Empresa ▾» (decisión 24).
  - **Cómo mejora con el uso:** cada corrección queda registrada (el dato propuesto, el confirmado y quién lo corrigió) y se suma como ejemplo a «Cómo reconocerlo». Se mide la precisión por formulario y por dato. No se entrena ningún modelo.
  - **Lo que va al proveedor de IA:** solo el documento preparado y las preguntas del formulario candidato. Se registra qué se mandó (hash y tamaño), sin el contenido. Lo leído es un dato, nunca una instrucción.
  - **Costos:** la lectura entra en el tope mensual de IA de la organización (ADR 0051), con un tope por persona y por día (41). Al llegar a un tope, el documento queda en la bandeja sin leerse.
  - **Calificación:** el perfil de lectura se califica antes de habilitarlo, con casos ficticios: comprobantes de varios bancos, fotos movidas, documentos que no son de ningún tipo, instrucciones escondidas en el documento, Word y Excel.
- **Front:**
  - en el chat, «Por qué lo propuso» o «Por qué preguntó» debajo del documento, como en la E18;
  - la sección «Lectura de documentos» en el editor;
  - la bandeja, «Armar formulario con la IA» y el tablero.
- **Tableros que dibuja Diseño** (evaluación, §5.2), con nuestras piezas y la decisión 43:
  - `DOC2` · Los tres casos en el celular, y los otros formatos;
  - `DOC3` · El formulario prellenado: lo dudoso con «Revisá», lo no leído vacío y el documento adjunto;
  - `DOC4` · Los casos difíciles: foto movida, PDF con contraseña, repetido, CBU mal leído, formulario que no se le puede mandar, formatos viejos y número sin cuenta vinculada;
  - `DOC5-A` · La bandeja, una planilla con panel;
  - `DOC6` · «Armar formulario con la IA»;
  - `DOC7-A` · El tablero, con tarjetas armadas solas.

  `DOC1` (la biblioteca y la lectura) va con la E17.

## Decisiones tomadas (no se vuelven a preguntar)

- **Retención** (41): un documento usado sigue a su respuesta, que dura lo que la organización (23). Uno descartado o sin clasificar se borra a los 90 días, con su lectura.
- **Costos** (41): el proveedor de la plataforma, dentro del tope mensual de IA de la organización, con un tope por persona y por día.
- **Permisos** (ajuste de la Guía del 2026-10-24, con la fila 24 de la evaluación: el único permiso nuevo del bloque es `formpersondata.manage`):
  - ver la bandeja y el tablero pide `formrecords.read`;
  - revisar (corregir, pasar al formulario y descartar) pide además «Atender internos»;
  - «Armar formulario con la IA» pide además `forms.manage`.
- **La empresa** (ajuste de la Guía del 2026-10-24): columna y filtro «Empresa ▾» en la bandeja y en el tablero, como en la planilla (24).
- **La biblioteca** es la de la E17 (40).
- **Genérico:** ningún tipo de documento va en el código. Un estándar nuevo es una plantilla que carga un operador.

## Fuera de esta etapa

- La carga automática («Cargar solo si es seguro»): después, con datos medidos.
- Varias fotos de un mismo documento, o varios documentos en un mensaje.
- Leer lo que manda un número desconocido, y B2C.
- Los tableros que cruzan formularios o suman datos externos.
- Los validadores de otros estándares, como el QR de una factura.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`. Antes, la calificación del perfil de lectura. Después, una **prueba real con Meta**, con comprobantes de varios bancos y billeteras y la plantilla «Comprobante de transferencia» cargada por un operador en la biblioteca:
- reconocimiento seguro, con duda y sin reconocer;
- un CBU con el dígito verificador mal leído, que no pasa;
- un duplicado y una foto movida;
- la bandeja, las correcciones registradas y el tablero.

## Requisitos previos

- La E13c (el cruce por CUIT), la E17 (los estándares) y la E18 (las ráfagas, las acciones y derivar), cerradas.
- La prueba con Meta del archivo precargado en un Flow.

## Ajustes ya decididos

- **Idioma:** el contenido no tiene idioma (35). Los mensajes del sistema a la persona van en el idioma de su cuenta (42).
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** documentos, lecturas, correcciones, precisión y ajustes del tablero son `ITenantOwned`, con RLS. Cada ruta nueva lleva su test de aislamiento.
- **Workers:** la bajada y la lectura son operaciones durables (efecto externo), descubiertas en un registro técnico global sin datos de negocio, y entran con `ExecutionActor`. El borrado de los 90 días recorre las organizaciones con `TenantJobRunner`.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **Tope por persona y por día:** una opción del módulo en `Application/Configuration/Forms`, con `ValidateOnStart`, igual para todas las organizaciones.
- **WhatsApp (E11):** la bajada usa la WABA y el token de la organización.
- **Almacenamiento (E10):** las copias privadas van en `IFileStore`, con claves opacas, como en la E14, y el antivirus de la lista de lanzamiento.
- **Baja de la cuenta:** el participante del módulo borra los documentos sin usar de esa persona; los usados siguen a su respuesta.
